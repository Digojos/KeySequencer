using System.IO;
using System.Text.Json;
using KeySequencer.Core.Domain;

namespace KeySequencer.Core.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ConfigDirectory { get; } = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "configs");

    /// <summary>Writes <paramref name="config"/> to an exact path - the caller (a native "Save
    /// As" dialog in the UI) is responsible for the filename/location, this just serializes.</summary>
    public void Save(string filePath, SavedConfig config)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, JsonSerializer.Serialize(config, JsonOptions));
    }

    public SavedConfig Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<SavedConfig>(json, JsonOptions) ?? new SavedConfig();
    }
}
