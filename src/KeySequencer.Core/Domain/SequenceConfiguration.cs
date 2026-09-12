namespace KeySequencer.Core.Domain;

/// <summary>
/// A hotkey-triggered sequence: <see cref="Keys"/> is played back in list order (no separate
/// ordering step like the Dota combo app's slot/position model - there's only one queue).
/// </summary>
public sealed record SequenceConfiguration(
    IReadOnlyList<string> Keys,
    HotkeyConfig Hotkey,
    int PingMs);
