namespace MicLinkWinUI.Infrastructure.Storage;

using System.Text.Json;

public sealed class LocalSettingsStore
{
    private readonly string _filePath;
    private Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public LocalSettingsStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MicLink");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "settings.json");
        LoadFromDisk();
    }

    public string? Get(string key) =>
        _values.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string value)
    {
        _values[key] = value;
        SaveToDisk();
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            _values = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                      ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch
        {
            _values = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void SaveToDisk()
    {
        var json = JsonSerializer.Serialize(_values);
        File.WriteAllText(_filePath, json);
    }
}
