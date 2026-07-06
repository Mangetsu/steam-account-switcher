namespace EnigmaLauncher.Stores;

/// <summary>
/// Optional store capability for opening the desktop client and navigating to a
/// game's library page without launching the game.
/// </summary>
public interface IStoreClientActions
{
    /// <summary>Starts or focuses the store's desktop client.</summary>
    void StartClient();

    /// <summary>
    /// Builds an operation that opens <paramref name="game"/> in the store library,
    /// switching to its owning account first when required.
    /// </summary>
    Func<IProgress<string>?, Task> BuildOpenInLibraryOperation(GameInfo game);
}
