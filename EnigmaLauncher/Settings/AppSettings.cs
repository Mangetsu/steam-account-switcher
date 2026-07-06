using System.Collections.Generic;

namespace EnigmaLauncher.Settings;

/// <summary>
/// Root settings object persisted to <c>data\settings.json</c>.
/// All properties have safe defaults so the file is optional on first run.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Per-game display preferences keyed by <c>"storeId:gameId"</c>
    /// (e.g. <c>"steam:730"</c>).
    /// </summary>
    public Dictionary<string, GameDisplaySettings> GameDisplay { get; set; } = [];

    /// <summary>
    /// Global default display override (device name, e.g. <c>"\\.\DISPLAY2"</c>).
    /// Empty/null means no override — launch on whatever is current primary.
    /// </summary>
    public string? DefaultDisplayDevice { get; set; }

    /// <summary>
    /// Set to true once the one-time migration from SteamSwitcher v1.0.0 has run.
    /// </summary>
    public bool MigrationFromSteamSwitcherV1Done { get; set; }
}
