using System.Windows;
using EnigmaLauncher.Steam;
using EnigmaLauncher.UI;

namespace EnigmaLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args;

        if (args.Length >= 2 && args[0] == "--launch" && int.TryParse(args[1], out int appId))
        {
            RunLaunchMode(appId, ParseOwnerArg(args));
        }
        else
        {
            RunGuiMode();
        }
    }

    private static long? ParseOwnerArg(string[] args)
    {
        var value = ParseStringArg(args, "--owner");
        if (value is not null && long.TryParse(value, out var owner))
            return owner;

        return null;
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

    private void RunLaunchMode(int appId, long? ownerSteamId64)
    {
        SteamConfig config;
        try
        {
            config = SteamConfig.FromRegistry();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Steam not found. Run Steam at least once first.\n\nDetails: {ex.Message}",
                "EnigmaLauncher — Steam Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var launchWindow = new LaunchWindow(SteamOperations.LaunchGame(config, appId, ownerSteamId64));
        launchWindow.Show();
        MainWindow = launchWindow;
    }

    private void RunGuiMode()
    {
        SteamConfig? config = null;
        try
        {
            config = SteamConfig.FromRegistry();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Steam not found. Run Steam at least once first.\n\nDetails: {ex.Message}",
                "EnigmaLauncher — Steam Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var mainWindow = new MainWindow(config);
        mainWindow.Show();
        MainWindow = mainWindow;
    }
}
