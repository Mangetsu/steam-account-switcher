namespace EnigmaLauncher.Steam;

/// <summary>
/// Factory that produces self-contained async operations for <see cref="UI.LaunchWindow"/>.
/// Centralises all "find game → check account → switch if needed → launch / switch"
/// business logic so that <c>LaunchWindow</c> and <c>MainWindow</c> share one code path
/// instead of duplicating it.
/// </summary>
public static class SteamOperations
{
    /// <summary>Starts Steam without launching a game.</summary>
    public static void StartClient(SteamConfig config)
    {
        var accounts = new AccountManager(config);
        new AccountSwitcher(config, accounts).StartClient();
    }

    /// <summary>
    /// Returns an operation that launches <paramref name="appId"/>,
    /// switching Steam accounts first if needed.
    /// </summary>
    public static Func<IProgress<string>?, Task> LaunchGame(SteamConfig config, int appId, long? ownerSteamId64 = null)
        => async progress =>
        {
            var accounts = new AccountManager(config);
            var scanner  = new LibraryScanner(config);
            var switcher = new AccountSwitcher(config, accounts);

            var game = scanner.FindGame(appId, ownerSteamId64)
                ?? throw new InvalidOperationException(
                       ownerSteamId64 is null
                           ? $"Game {appId} not found in any Steam library.\nMake sure the game is installed."
                           : $"Game {appId} was not found for the selected Steam account.\nMake sure the game is installed for that account.");

            progress?.Report($"Launching {game.Name}...");

            var owner = game.LastOwnerSteamId64 != 0
                ? accounts.GetBySteamId64(game.LastOwnerSteamId64)
                : null;

            if (owner is null || !switcher.IsSwitchNeeded(owner))
            {
                switcher.LaunchDirect(appId);
                progress?.Report($"Launching {game.Name}!");
                return;
            }

            if (!owner.CanAutoSwitch)
                throw new InvalidOperationException(
                    $"Cannot auto-switch to '{owner.PersonaName}'.\n" +
                    "Log in to that account in Steam with 'Remember me' checked.");

            await switcher.SwitchAndLaunchAsync(owner, appId, progress);
        };

    /// <summary>
    /// Returns an operation that opens a game in the Steam Library, switching
    /// accounts first when the selected game belongs to another account.
    /// </summary>
    public static Func<IProgress<string>?, Task> OpenGameInLibrary(
        SteamConfig config, int appId, long? ownerSteamId64 = null)
        => async progress =>
        {
            var accounts = new AccountManager(config);
            var scanner  = new LibraryScanner(config);
            var switcher = new AccountSwitcher(config, accounts);

            var game = scanner.FindGame(appId, ownerSteamId64)
                ?? throw new InvalidOperationException(
                    $"Game {appId} was not found for the selected Steam account.");

            var owner = game.LastOwnerSteamId64 != 0
                ? accounts.GetBySteamId64(game.LastOwnerSteamId64)
                : null;

            if (owner is null || !switcher.IsSwitchNeeded(owner))
            {
                switcher.OpenGameInLibraryDirect(appId);
                progress?.Report($"Opened {game.Name} in Steam Library!");
                return;
            }

            if (!owner.CanAutoSwitch)
                throw new InvalidOperationException(
                    $"Cannot auto-switch to '{owner.PersonaName}'.\n" +
                    "Log in to that account in Steam with 'Remember me' checked.");

            await switcher.SwitchAndOpenLibraryAsync(owner, appId, progress);
        };

    /// <summary>
    /// Returns an operation that switches the active Steam account to
    /// <paramref name="target"/> without launching any game.
    /// </summary>
    public static Func<IProgress<string>?, Task> SwitchAccount(SteamConfig config, SteamAccount target)
        => async progress =>
        {
            var accounts = new AccountManager(config);
            var switcher = new AccountSwitcher(config, accounts);
            await switcher.SwitchOnlyAsync(target, progress);
        };
}
