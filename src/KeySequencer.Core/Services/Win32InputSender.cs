using System.Runtime.InteropServices;

namespace KeySequencer.Core.Services;

/// <summary>Injects key presses into the system-wide input stream via SendInput.</summary>
public sealed class Win32InputSender : IInputSender
{
    // Games/apps commonly poll input once per frame (~16ms at 60fps); holding the key down for
    // a bit longer than that avoids a down+up pair landing in the same poll and being missed.
    private const int KeyHoldMs = 20;

    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;

    private const ushort VkControl = 0x11;
    private const ushort VkMenu = 0x12;
    private const ushort VkShift = 0x10;

    public async Task PressKeyAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        var tokens = key.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return;

        if (!VirtualKeyMapper.TryResolve(tokens[^1], out var mainVk, out var requiresShift)) return;

        var modifiers = new List<ushort>();
        foreach (var token in tokens[..^1])
        {
            var vk = token.ToUpperInvariant() switch
            {
                "CTRL" => VkControl,
                "ALT" => VkMenu,
                "SHIFT" => VkShift,
                _ => (ushort)0
            };
            if (vk != 0) modifiers.Add(vk);
        }
        if (requiresShift && !modifiers.Contains(VkShift)) modifiers.Add(VkShift);

        foreach (var vk in modifiers) SendKeyEvent(vk, keyUp: false);
        SendKeyEvent(mainVk, keyUp: false);
        await Task.Delay(KeyHoldMs).ConfigureAwait(false);
        SendKeyEvent(mainVk, keyUp: true);
        for (var i = modifiers.Count - 1; i >= 0; i--) SendKeyEvent(modifiers[i], keyUp: true);
    }

    private static void SendKeyEvent(ushort virtualKey, bool keyUp)
    {
        var input = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    dwFlags = keyUp ? KeyEventFKeyUp : 0
                }
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    // SendInput fails outright if cbSize doesn't match the real Win32 INPUT struct's size, which
    // is a union of MOUSEINPUT/KEYBDINPUT/HARDWAREINPUT - MOUSEINPUT is the largest variant. This
    // sender only ever populates `ki`, but MOUSEINPUT must stay declared here so Marshal.SizeOf
    // reports the correct (larger) union size; removing it would silently break every SendInput
    // call.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
