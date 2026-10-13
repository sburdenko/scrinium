using System.Text.Json;

namespace Scrinium.Core.Settings;

public sealed class SettingsService : ISettingsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public SettingsService(string filePath)
    {
        _filePath = filePath;
        Current = Load(filePath);
    }

    public AppSettings Current { get; private set; }

    public event Action<AppSettings>? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var next = change(Current);
        if (next == Current)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(next, JsonOptions));
        Current = next;
        Changed?.Invoke(next);
    }

    private static AppSettings Load(string filePath)
    {
        if (!File.Exists(filePath))
            return new AppSettings();

        var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath));
        return loaded ?? new AppSettings();
    }
}
