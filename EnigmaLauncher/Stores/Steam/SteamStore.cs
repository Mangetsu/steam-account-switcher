using EnigmaLauncher.Steam;

namespace EnigmaLauncher.Stores.Steam;

/// <summary>
/// Implements <see cref="IAccountStore"/> for Steam.
/// Acts as a thin adapter between the store abstraction layer and the
/// existing internal Steam classes (SteamConfig, AccountManager, …).
/// No Steam-specific types are exposed through the interface.
/// </summary>
public sealed class SteamStore : IAccountStore
{
    // ── Palette for deterministic account badge colours ────────────────────────
    private static readonly string[] BadgePalette =
    [
        "#2563EB", "#16A34A", "#DC2626", "#D97706", "#7C3AED",
        "#0891B2", "#BE185D", "#059669", "#EA580C", "#4338CA",
    ];

    private readonly SteamConfig    _config;
    private readonly AccountManager _accounts;
    private readonly LibraryScanner _scanner;
    private readonly ArtworkResolver _artwork;

    private SteamStore(SteamConfig config)
    {
        _config   = config;
        _accounts = new AccountManager(config);
        _scanner  = new LibraryScanner(config);
        _artwork  = new ArtworkResolver(config);
    }

    // ── Factory ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a <see cref="SteamStore"/> if Steam is installed and its registry
    /// key is readable, otherwise null.
    /// </summary>
    public static SteamStore? TryCreate()
    {
        try
        {
            var config = SteamConfig.FromRegistry();
            return new SteamStore(config);
        }
        catch
        {
            return null;
        }
    }

    // ── IGameStore ─────────────────────────────────────────────────────────────

    public string StoreId     => "steam";
    public string DisplayName => "Steam";
    public bool   IsAvailable => true; // TryCreate only returns non-null when Steam is present

    public IReadOnlyList<GameInfo> ScanGames() =>
        _scanner.Scan().Select(ToGameInfo).ToList();

    public string? GetArtworkPath(GameInfo game)
    {
        if (!int.TryParse(game.GameId, out var appId)) return null;
        return _artwork.GetLocalArtworkPath(appId)
            ?? _artwork.GetCachedDownloadPath(appId);
    }

    public async Task<string?> DownloadArtworkAsync(GameInfo game)
    {
        if (!int.TryParse(game.GameId, out var appId)) return null;
        return await _artwork.DownloadArtworkAsync(appId);
    }

    public Func<IProgress<string>?, Task> BuildLaunchOperation(GameInfo game)
    {
        if (!int.TryParse(game.GameId, out var appId))
            throw new InvalidOperationException($"Invalid Steam AppId '{game.GameId}'.");

        long? ownerSteamId64 = game.OwnerAccountId is not null
            && long.TryParse(game.OwnerAccountId, out var id) ? id : null;

        return SteamOperations.LaunchGame(_config, appId, ownerSteamId64);
    }

    // ── IAccountStore ──────────────────────────────────────────────────────────

    public IReadOnlyList<AccountInfo> GetAccounts() =>
        _accounts.LoadAccounts().Select(ToAccountInfo).ToList();

    public AccountInfo? GetCurrentAccount()
    {
        var account = _accounts.GetCurrentAccount();
        return account is null ? null : ToAccountInfo(account);
    }

    public AccountInfo? GetAccountById(string accountId)
    {
        if (!long.TryParse(accountId, out var steamId64)) return null;
        var account = _accounts.GetBySteamId64(steamId64);
        return account is null ? null : ToAccountInfo(account);
    }

    public Func<IProgress<string>?, Task> BuildSwitchOperation(AccountInfo target)
    {
        if (!long.TryParse(target.AccountId, out var steamId64))
            throw new InvalidOperationException($"Invalid Steam account ID '{target.AccountId}'.");

        var account = _accounts.GetBySteamId64(steamId64)
            ?? throw new InvalidOperationException(
                $"Account '{target.DisplayName}' not found in loginusers.vdf.");

        return SteamOperations.SwitchAccount(_config, account);
    }

    public void InvalidateAccountCache() => _accounts.InvalidateCache();

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private GameInfo ToGameInfo(GameEntry entry)
    {
        _accounts.TryGetCachedAccount(entry.LastOwnerSteamId64, out var owner);
        return new GameInfo
        {
            StoreId          = StoreId,
            GameId           = entry.AppId.ToString(),
            Name             = entry.Name,
            LibraryPath      = entry.LibraryPath,
            OwnerAccountId   = entry.LastOwnerSteamId64 != 0
                               ? entry.LastOwnerSteamId64.ToString() : null,
            OwnerDisplayName = owner?.ToString(),
        };
    }

    internal static AccountInfo ToAccountInfo(SteamAccount account) => new()
    {
        StoreId       = "steam",
        AccountId     = account.SteamId64.ToString(),
        AccountName   = account.AccountName,
        DisplayName   = account.PersonaName.Length > 0
                        ? account.PersonaName : account.AccountName,
        CanAutoSwitch = account.CanAutoSwitch,
        BadgeColor    = ComputeBadgeColor(account.AccountName),
    };

    /// <summary>
    /// Deterministic colour derived from the account name — stable across restarts
    /// and consistent whether the badge is rendered on a card or in the header.
    /// </summary>
    private static string ComputeBadgeColor(string accountName)
    {
        if (string.IsNullOrEmpty(accountName)) return "#6B7280";
        var hash = accountName.Aggregate(0, (h, c) => h * 31 + c);
        return BadgePalette[Math.Abs(hash) % BadgePalette.Length];
    }
}
