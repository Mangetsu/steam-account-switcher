namespace EnigmaLauncher.Stores;

/// <summary>
/// Minimal contract every game store must satisfy.
/// Covers discovery, artwork, and launching.
/// </summary>
public interface IGameStore
{
    /// <summary>Unique short identifier, e.g. "steam", "epic", "gog".</summary>
    string StoreId { get; }

    /// <summary>Human-readable name shown in the UI, e.g. "Steam".</summary>
    string DisplayName { get; }

    /// <summary>True when this store is detected on the current machine.</summary>
    bool IsAvailable { get; }

    /// <summary>Returns all installed games. May be expensive; call off the UI thread.</summary>
    IReadOnlyList<GameInfo> ScanGames();

    /// <summary>
    /// Returns a local file path to the best available artwork for <paramref name="game"/>,
    /// or null if no artwork is available yet (download will be triggered separately).
    /// </summary>
    string? GetArtworkPath(GameInfo game);

    /// <summary>
    /// Downloads artwork for <paramref name="game"/> to the local cache and returns its path,
    /// or null if the download failed or no artwork exists.
    /// </summary>
    Task<string?> DownloadArtworkAsync(GameInfo game);

    /// <summary>
    /// Produces a self-contained async operation that launches <paramref name="game"/>,
    /// switching accounts first if needed. Suitable for passing to <see cref="UI.LaunchWindow"/>.
    /// </summary>
    Func<IProgress<string>?, Task> BuildLaunchOperation(GameInfo game);
}
