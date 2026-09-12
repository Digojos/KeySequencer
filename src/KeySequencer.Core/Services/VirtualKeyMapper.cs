using System.Runtime.InteropServices;

namespace KeySequencer.Core.Services;

public static class VirtualKeyMapper
{
    private static readonly Dictionary<string, ushort> NamedKeys = new()
    {
        ["SPACE"] = 0x20,
        ["DELETE"] = 0x2E,
        ["INSERT"] = 0x2D,
        ["HOME"] = 0x24,
        ["END"] = 0x23,
        ["PAGEUP"] = 0x21,
        ["PAGEDOWN"] = 0x22,
        ["F1"] = 0x70,
        ["F2"] = 0x71,
        ["F3"] = 0x72,
        ["F4"] = 0x73,
        ["F5"] = 0x74,
        ["F6"] = 0x75,
        ["F7"] = 0x76,
        ["F8"] = 0x77,
        ["F9"] = 0x78,
        ["F10"] = 0x79,
        ["F11"] = 0x7A,
        ["F12"] = 0x7B,
    };

    /// <summary>Resolves a single key token (no "+" modifiers) to its Win32 virtual-key code.</summary>
    public static bool TryResolve(string key, out ushort virtualKey, out bool requiresShift)
    {
        virtualKey = 0;
        requiresShift = false;

        if (string.IsNullOrWhiteSpace(key)) return false;

        var token = key.Trim().ToUpperInvariant();
        if (NamedKeys.TryGetValue(token, out var namedVk))
        {
            virtualKey = namedVk;
            return true;
        }

        if (token.Length == 1)
        {
            // VkKeyScan maps a printable character to its VK code + required shift state,
            // covering letters/digits/punctuation without hardcoding every layout-specific key.
            // Letter keys are normalized to uppercase elsewhere for display/comparison, but a
            // letter's VK code is identical regardless of case - only the resulting *character*
            // differs by shift state. Scanning the uppercase form would report "needs Shift" for
            // every single letter (since Shift is what makes the physical key type a capital),
            // which is irrelevant for a hotkey/sequence token. Scan the lowercase form instead
            // so letters resolve to their base (no-shift) state.
            var scanChar = char.IsLetter(token[0]) ? char.ToLowerInvariant(token[0]) : token[0];
            var scan = VkKeyScan(scanChar);
            if (scan != -1)
            {
                virtualKey = (ushort)(scan & 0xFF);
                requiresShift = (scan & 0x100) != 0;
                return true;
            }
        }

        return false;
    }

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);
}
