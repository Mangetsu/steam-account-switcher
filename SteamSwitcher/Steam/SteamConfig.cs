using Microsoft.Win32;
using System.IO;

namespace SteamSwitcher.Steam;

public class SteamConfig
{
    public string SteamPath { get; }
    public string SteamExe { get; }
    public string LoginUsersVdf { get; }
    public string LibraryFoldersVdf { get; }
    public string AppCachePath { get; }

    private SteamConfig(string steamPath)
    {
        SteamPath = steamPath;
        SteamExe = Path.Combine(steamPath, "steam.exe");
        LoginUsersVdf = Path.Combine(steamPath, "config", "loginusers.vdf");
        LibraryFoldersVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        AppCachePath = Path.Combine(steamPath, "appcache", "librarycache");
    }

    public static SteamConfig FromRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam")
            ?? throw new InvalidOperationException("Steam registry key not found. Is Steam installed?");

        var rawPath = key.GetValue("SteamPath") as string
            ?? throw new InvalidOperationException("SteamPath registry value is missing.");

        // Steam stores paths with forward slashes on Windows
        var steamPath = rawPath.Replace('/', Path.DirectorySeparatorChar);

        if (!File.Exists(Path.Combine(steamPath, "steam.exe")))
            throw new FileNotFoundException($"steam.exe not found at '{steamPath}'. Check your Steam installation.");

        return new SteamConfig(steamPath);
    }

    public string GetCurrentAccountName()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
        return key?.GetValue("AutoLoginUser") as string ?? string.Empty;
    }

    public int GetActiveUserSteamId3()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam\ActiveProcess");
        if (key is null) return 0;
        var value = key.GetValue("ActiveUser");
        return value is int i ? i : 0;
    }

    public int GetActiveSteamPid()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam\ActiveProcess");
        if (key is null) return 0;
        var value = key.GetValue("pid");
        return value is int i ? i : 0;
    }

    public void SetAutoLogin(string accountName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam", writable: true)
            ?? throw new InvalidOperationException("Cannot open Steam registry key for writing.");

        key.SetValue("AutoLoginUser", accountName, RegistryValueKind.String);
        key.SetValue("RememberPassword", 1, RegistryValueKind.DWord);
    }

    /// <summary>
    /// Zeros out the stale ActiveProcess registry values left behind after killing Steam.
    /// If left non-zero, Steam compares ActiveUser against AutoLoginUser on startup,
    /// detects a mismatch, and shows the "Change account" dialog instead of silently logging in.
    /// </summary>
    public void ClearActiveProcess()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Valve\Steam\ActiveProcess", writable: true);
        if (key is null) return;
        try { key.SetValue("pid", 0, RegistryValueKind.DWord); } catch { }
        try { key.SetValue("ActiveUser", 0, RegistryValueKind.DWord); } catch { }
    }
}
