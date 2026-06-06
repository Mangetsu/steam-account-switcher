namespace EnigmaLauncher.Stores;

/// <summary>
/// Store-agnostic account record. Replaces SteamAccount in all UI and operation code.
/// Steam-specific fields (SteamId64, SteamId3, VDF flags) stay inside SteamStore.
/// </summary>
public class AccountInfo
{
    /// <summary>Identifies the store, e.g. "steam".</summary>
    public string StoreId { get; init; } = string.Empty;

    /// <summary>
    /// Store-specific account identifier. For Steam this is the SteamID64 as a string.
    /// </summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Login / internal account name (e.g. Steam account name).</summary>
    public string AccountName { get; init; } = string.Empty;

    /// <summary>Human-readable display name (persona name, gamertag, …).</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// True when the launcher can switch to this account non-interactively.
    /// For Steam this requires RememberPassword = 1.
    /// </summary>
    public bool CanAutoSwitch { get; init; }

    /// <summary>
    /// Hex colour string (#RRGGBB) used for the account badge in the UI.
    /// Derived deterministically from the account name so it is stable across restarts.
    /// </summary>
    public string BadgeColor { get; init; } = "#6B7280";

    public override string ToString() =>
        DisplayName.Length > 0 ? DisplayName : AccountName;
}
