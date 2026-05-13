using System.IO;
using System.Text.Json;

namespace RamGuardian.App;

public sealed class AppStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsPath;

    public AppStateStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RamGuardian",
            "settings.json");
    }

    public AppState Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return AppState.Default;
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppState>(json, SerializerOptions) ?? AppState.Default;
        }
        catch
        {
            return AppState.Default;
        }
    }

    public void Save(AppState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(state, SerializerOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Keep the app resilient if local settings cannot be written.
        }
    }
}
