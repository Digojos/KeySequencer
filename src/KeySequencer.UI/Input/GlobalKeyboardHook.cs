using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeySequencer.UI.Input;

/// <summary>
/// Global low-level keyboard hook (WH_KEYBOARD_LL). The hook procedure runs on the thread that
/// installed it, which for a classic-desktop Avalonia app is the same thread pumping the Win32
/// message loop - so callers can safely touch UI state from <see cref="HotkeyPressed"/>.
/// </summary>
public sealed class GlobalKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _keyDown;

    public event Action? HotkeyPressed;

    public ushort TargetVirtualKey { get; set; }
    public bool RequiresCtrl { get; set; }
    public bool RequiresAlt { get; set; }

    /// <summary>Eat the trigger keypress so it doesn't also reach the focused app/game.</summary>
    public bool Suppress { get; set; } = true;

    public GlobalKeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(module.ModuleName), 0);
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero) return;

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    public void Dispose() => Stop();

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && TargetVirtualKey != 0)
        {
            var message = wParam.ToInt32();
            var vkCode = Marshal.ReadInt32(lParam); // vkCode is the first field of KBDLLHOOKSTRUCT

            if (message is WmKeyDown or WmSysKeyDown)
            {
                if (vkCode == TargetVirtualKey)
                {
                    var ctrlDown = (GetAsyncKeyState(VkControl) & 0x8000) != 0;
                    var altDown = (GetAsyncKeyState(VkMenu) & 0x8000) != 0;
                    if (ctrlDown == RequiresCtrl && altDown == RequiresAlt)
                    {
                        if (!_keyDown)
                        {
                            _keyDown = true;
                            HotkeyPressed?.Invoke();
                        }

                        if (Suppress) return (IntPtr)1;
                    }
                }
            }
            else if (message is WmKeyUp or WmSysKeyUp)
            {
                if (vkCode == TargetVirtualKey) _keyDown = false;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
