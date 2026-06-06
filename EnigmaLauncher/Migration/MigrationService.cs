using System.IO;
using System.Runtime.InteropServices;

namespace EnigmaLauncher.Migration;

/// <summary>
/// One-time migration from SteamSwitcher v1.0.0 to EnigmaLauncher.
///
/// What it does (all steps are idempotent):
///   1. Copies <c>data\cache\</c> and <c>data\icons\</c> from the old
///      <c>%LOCALAPPDATA%\SteamSwitcher\</c> folder to the new
///      <c>%LOCALAPPDATA%\EnigmaLauncher\</c> folder — only if the old
///      folder is present and the new folder doesn't already have the files.
///   2. Scans Desktop and Start Menu for <c>.lnk</c> shortcuts whose
///      <c>TargetPath</c> ends with <c>SteamSwitcher\SteamSwitcher.exe</c>
///      and rewrites them to point to the new <c>EnigmaLauncher.exe</c>
///      (keeping all arguments unchanged).
///   3. Marks the migration as done in <c>data\settings.json</c>.
///
/// The old folder is intentionally NOT deleted — it serves as a fallback
/// for any shortcuts or references we may have missed.
/// </summary>
public static class MigrationService
{
    // ── Entry point ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the migration if it has not already run and the old folder exists.
    /// Safe to call on startup every time — exits quickly once done.
    /// </summary>
    public static void RunIfNeeded(Settings.SettingsStore settings)
    {
        if (settings.Current.MigrationFromSteamSwitcherV1Done)
            return;

        var oldRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamSwitcher");

        if (!Directory.Exists(oldRoot))
        {
            // Old install never existed — mark as done so we never check again.
            settings.Current.MigrationFromSteamSwitcherV1Done = true;
            settings.Save();
            return;
        }

        MigrateCacheAndIcons(oldRoot);
        RewriteShortcuts(oldRoot);

        settings.Current.MigrationFromSteamSwitcherV1Done = true;
        settings.Save();
    }

    // ── Step 1: copy data directories ─────────────────────────────────────────

    private static void MigrateCacheAndIcons(string oldRoot)
    {
        CopyDirectory(
            Path.Combine(oldRoot, "data", "cache"),
            AppPaths.CacheDirectory);

        CopyDirectory(
            Path.Combine(oldRoot, "data", "icons"),
            AppPaths.IconsDirectory);
    }

    /// <summary>
    /// Recursively copies <paramref name="src"/> to <paramref name="dst"/>.
    /// Skips files that already exist in the destination.
    /// </summary>
    private static void CopyDirectory(string src, string dst)
    {
        if (!Directory.Exists(src)) return;

        Directory.CreateDirectory(dst);

        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(src, file);
            var target   = Path.Combine(dst, relative);

            if (File.Exists(target)) continue; // already migrated

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            try { File.Copy(file, target); }
            catch { /* best-effort — a missing cache file is not fatal */ }
        }
    }

    // ── Step 2: rewrite .lnk shortcuts ────────────────────────────────────────

    private static void RewriteShortcuts(string oldRoot)
    {
        var searchRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        };

        // Old exe path pattern (case-insensitive suffix match)
        const string oldExeSuffix  = @"SteamSwitcher\SteamSwitcher.exe";

        // New exe: the running process's own path
        var newExePath = System.Diagnostics.Process
            .GetCurrentProcess().MainModule?.FileName;

        if (string.IsNullOrEmpty(newExePath)) return;

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return;

        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            foreach (var root in searchRoots)
            {
                if (!Directory.Exists(root)) continue;

                foreach (var lnkPath in Directory.EnumerateFiles(root, "*.lnk",
                             SearchOption.AllDirectories))
                {
                    try
                    {
                        RewriteShortcut(shell, lnkPath, oldExeSuffix, newExePath);
                    }
                    catch { /* don't crash the migration if one shortcut fails */ }
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(shell);
        }
    }

    private static void RewriteShortcut(
        dynamic shell,
        string lnkPath,
        string oldExeSuffix,
        string newExePath)
    {
        dynamic sc = shell.CreateShortcut(lnkPath);
        try
        {
            var target = (string?)sc.TargetPath;
            if (string.IsNullOrEmpty(target)) return;
            if (!target.EndsWith(oldExeSuffix, StringComparison.OrdinalIgnoreCase)) return;

            sc.TargetPath       = newExePath;
            sc.WorkingDirectory = Path.GetDirectoryName(newExePath) ?? string.Empty;
            // sc.Arguments and sc.IconLocation are intentionally preserved
            sc.Save();
        }
        finally
        {
            Marshal.ReleaseComObject(sc);
        }
    }
}
