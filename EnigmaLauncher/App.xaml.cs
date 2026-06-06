using System.Windows;
using EnigmaLauncher.Stores;
using EnigmaLauncher.UI;

namespace EnigmaLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args;

        if (args.Length >= 2 && args[0] == "--launch")
        {
            // --launch <gameId> [--owner <accountId>]
            RunLaunchMode(args[1], ParseStringArg(args, "--owner"));
        }
        else
        {
            RunGuiMode();
        }
    }

    private static string? ParseStringArg(string[] args, string name)
    {
        for (var i = 2; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }

        return null;
    }

    private void RunLaunchMode(string gameId, string? ownerAccountId)
    {
        var registry = new StoreRegistry();

        // For launch mode, infer the store from the gameId format — Steam AppIds are numeric.
        // When additional stores are supported, a --store <storeId> arg can be introduced.
        var store = registry.Get("steam");
        if (store is null)
        {
            MessageBox.Show(
                "Steam not found. Run Steam at least once first.",
                "EnigmaLauncher — Steam Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Build a minimal GameInfo — BuildLaunchOperation only needs GameId and OwnerAccountId.
        var game = new GameInfo
        {
            StoreId        = "steam",
            GameId         = gameId,
            Name           = string.Empty,
            OwnerAccountId = ownerAccountId,
        };

        var launchWindow = new LaunchWindow(store.BuildLaunchOperation(game));
        launchWindow.Show();
        MainWindow = launchWindow;
    }

    private void RunGuiMode()
    {
        var registry = new StoreRegistry();

        if (registry.All.Count == 0)
        {
            MessageBox.Show(
                "No supported game stores found. Install Steam and run it at least once.",
                "EnigmaLauncher — No Stores Found",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var mainWindow = new MainWindow(registry);
        mainWindow.Show();
        MainWindow = mainWindow;
    }
}
