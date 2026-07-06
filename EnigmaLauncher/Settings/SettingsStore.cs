using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnigmaLauncher.Settings;

/// <summary>
/// Reads and writes <see cref="AppSettings"/> to <c>data\settings.json</c>.
/// All I/O is synchronous and intentionally simple — the file is tiny.
/// </summary>
public class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented            = true,
        DefaultIgnoreCondition   = JsonIgnoreCondition.WhenWritingNull,
        Converters               = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private AppSettings _current;

    public SettingsStore() : this(AppPaths.SettingsFilePath) { }

    public SettingsStore(string filePath)
    {
        _filePath = filePath;
        _current  = Load();
    }

    /// <summary>Returns the in-memory settings (never null).</summary>
    public AppSettings Current => _current;

    /// <summary>Persists the current settings to disk.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(_current, SerializerOptions);
        File.WriteAllText(_filePath, json);
    }

    /// <summary>
    /// Returns the display settings for <paramref name="storeId"/>:<paramref name="gameId"/>,
    /// creating an empty entry if none exists yet.
    /// </summary>
    public GameDisplaySettings GetOrCreateGameDisplay(string storeId, string gameId)
    {
        var key = MakeKey(storeId, gameId);
        if (!_current.GameDisplay.TryGetValue(key, out var settings))
        {
            settings = new GameDisplaySettings();
            _current.GameDisplay[key] = settings;
        }

        return settings;
    }

    /// <summary>Overwrites the display settings for a game and saves.</summary>
    public void SetGameDisplay(string storeId, string gameId, GameDisplaySettings settings)
    {
        _current.GameDisplay[MakeKey(storeId, gameId)] = settings;
        Save();
    }

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)
                ?? new AppSettings();
        }
        catch
        {
            // Corrupt / incompatible settings — start fresh rather than crashing.
            return new AppSettings();
        }
    }

    private static string MakeKey(string storeId, string gameId) => $"{storeId}:{gameId}";
}
