namespace EnigmaLauncher.Stores;

/// <summary>
/// Store-agnostic game record. All UI and shortcut logic binds to this type;
/// Steam-specific fields (AppId as int, SteamId64) are hidden inside SteamStore.
/// </summary>
public class GameInfo
{
    /// <summary>Identifies the store that owns this entry, e.g. "steam".</summary>
    public string StoreId { get; init; } = string.Empty;

    /// <summary>
    /// Store-specific game identifier. For Steam this is the numeric AppId as a string
    /// (e.g. "730"). For other stores it will be whatever key that store uses.
    /// </summary>
    public string GameId { get; init; } = string.Empty;

    public string  Name             { get; init; } = string.Empty;
    public string? LibraryPath      { get; init; }

    /// <summary>
    /// Store-specific account identifier for the account that owns/last-played this game.
    /// For Steam this is the SteamID64 as a string. Null if unknown.
    /// </summary>
    public string? OwnerAccountId   { get; init; }

    /// <summary>Display name of the owning account, resolved at scan time.</summary>
    public string? OwnerDisplayName { get; init; }

    public override string ToString() => $"[{StoreId}:{GameId}] {Name}";
}
