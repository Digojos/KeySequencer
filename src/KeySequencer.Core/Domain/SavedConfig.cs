namespace KeySequencer.Core.Domain;

public sealed class SavedConfig
{
    public string Name { get; set; } = string.Empty;
    public HotkeyConfigDto Hotkey { get; set; } = new();
    public int Ping { get; set; }
    public List<string> Keys { get; set; } = [];
}

public sealed class HotkeyConfigDto
{
    public string Key { get; set; } = "SPACE";
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
}
