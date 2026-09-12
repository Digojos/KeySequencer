namespace KeySequencer.Core.Domain;

public sealed record HotkeyConfig(
    string Key,
    bool Ctrl = false,
    bool Alt = false);
