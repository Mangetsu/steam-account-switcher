using EnigmaLauncher.Stores.Steam;

namespace EnigmaLauncher.Stores;

/// <summary>
/// Discovers and holds all registered <see cref="IGameStore"/> instances.
/// Acts as the single entry point for the UI; hides all store-specific details.
/// </summary>
public class StoreRegistry
{
    private readonly List<IGameStore> _stores;

    public StoreRegistry()
    {
        _stores = [];

        // Register Steam if installed
        var steam = SteamStore.TryCreate();
        if (steam is not null)
            _stores.Add(steam);

        // Future stores (Epic, GOG, Xbox) will be registered here:
        // var epic = EpicStore.TryCreate();
        // if (epic is not null) _stores.Add(epic);
    }

    /// <summary>All registered stores, available or not.</summary>
    public IReadOnlyList<IGameStore> All => _stores;

    /// <summary>Stores that support multi-account switching.</summary>
    public IReadOnlyList<IAccountStore> AccountStores =>
        _stores.OfType<IAccountStore>().ToList();

    /// <summary>Returns the first store that matches <typeparamref name="T"/>, or null.</summary>
    public T? Get<T>() where T : class, IGameStore =>
        _stores.OfType<T>().FirstOrDefault();

    /// <summary>Returns the store with the given <paramref name="storeId"/>, or null.</summary>
    public IGameStore? Get(string storeId) =>
        _stores.FirstOrDefault(s => s.StoreId == storeId);

    /// <summary>
    /// Aggregates games from every available store.
    /// Call off the UI thread — this can be slow on large libraries.
    /// </summary>
    public IReadOnlyList<GameInfo> ScanAllGames() =>
        _stores
            .Where(s => s.IsAvailable)
            .SelectMany(s => s.ScanGames())
            .ToList();

    /// <summary>Aggregates accounts from every available account store.</summary>
    public IReadOnlyList<AccountInfo> GetAllAccounts() =>
        AccountStores
            .Where(s => s.IsAvailable)
            .SelectMany(s => s.GetAccounts())
            .ToList();

    /// <summary>
    /// Returns the currently active account across all account stores, or null.
    /// Stops at the first store that reports an active session.
    /// </summary>
    public AccountInfo? GetCurrentAccount() =>
        AccountStores
            .Where(s => s.IsAvailable)
            .Select(s => s.GetCurrentAccount())
            .FirstOrDefault(a => a is not null);
}
