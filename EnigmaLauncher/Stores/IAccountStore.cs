namespace EnigmaLauncher.Stores;

/// <summary>
/// Extension of <see cref="IGameStore"/> for stores that support multiple accounts
/// with non-interactive switching (Steam, potentially EA App in the future).
/// </summary>
public interface IAccountStore : IGameStore
{
    /// <summary>Returns all remembered accounts for this store.</summary>
    IReadOnlyList<AccountInfo> GetAccounts();

    /// <summary>
    /// Returns the account that is currently active (logged in), or null if
    /// no session is running or detection fails.
    /// </summary>
    AccountInfo? GetCurrentAccount();

    /// <summary>
    /// Finds the account whose <see cref="AccountInfo.AccountId"/> matches
    /// <paramref name="accountId"/>, or null if not found.
    /// </summary>
    AccountInfo? GetAccountById(string accountId);

    /// <summary>
    /// Produces a self-contained async operation that switches to
    /// <paramref name="target"/> without launching any game.
    /// </summary>
    Func<IProgress<string>?, Task> BuildSwitchOperation(AccountInfo target);

    /// <summary>Clears any in-memory account cache so the next call re-reads from disk.</summary>
    void InvalidateAccountCache();
}
