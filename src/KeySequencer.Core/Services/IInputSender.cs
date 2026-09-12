namespace KeySequencer.Core.Services;

public interface IInputSender
{
    /// <summary>Sends a full key press (down+up, held briefly in between) for a normalized key token, e.g. "Q", "F3" or "ALT+A".</summary>
    Task PressKeyAsync(string key);
}
