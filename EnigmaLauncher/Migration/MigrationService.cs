using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace EnigmaLauncher.Migration;

/// <summary>
/// One-time migration from SteamSwitcher v1.0.0 to EnigmaLauncher.
///
/// What it does:
///   1. Kills the old <c>SteamSwitcher.exe</c> process if it's still running.
///   2. Copies <c>data\cache\</c> and <c>data\icons\</c> from the old
///      <c>%LOCALAPPDATA%\SteamSwitcher\</c> folder to the new
///      <c>%LOCALAPPDATA%\EnigmaLauncher\</c> folder — only if the old
///      folder is present and the new folder doesn't already have the files.
///   3. Scans Desktop and Start Menu for <c>.lnk</c> shortcuts whose
///      <c>TargetPath</c> ends with <c>SteamSwitcher\SteamSwitcher.exe</c>
///      and rewrites them to point to the new <c>EnigmaLauncher.exe</c>
///      (keeping all arguments unchanged).
///   4. Prompts the user to delete the old install folder entirely. If they
///      agree, the folder is removed and any leftover Desktop shortcut still
///      pointing into it (one the step-3 scan didn't catch) is deleted too.
///      If they decline, the old folder is left exactly as-is.
///   5. Marks the migration as done in <c>data\settings.json</c> either way,
///      so the prompt only ever appears once.
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

        KillOldProcess();

        var newExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

        MigrateCacheAndIcons(oldRoot);
        RewriteShortcuts(oldRoot, newExePath);

        if (PromptDeleteOldInstall(oldRoot))
        {
            DeleteOldInstall(oldRoot);
            CleanupDanglingDesktopShortcuts(oldRoot);
        }

        settings.Current.MigrationFromSteamSwitcherV1Done = true;
        settings.Save();
    }

    // ── Step 1: stop the old app ───────────────────────────────────────────────

    private static void KillOldProcess()
    {
        try
        {
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("SteamSwitcher"))
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit(5000);
                }
                catch { /* best-effort — a stuck old process shouldn't block migration */ }
                finally { proc.Dispose(); }
            }
        }
        catch { /* GetProcessesByName can throw on some locked-down systems */ }
    }

    // ── Step 2: copy data directories ─────────────────────────────────────────

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

        var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var file in Directory.EnumerateFiles(src, "*", opts))
        {
            var relative = Path.GetRelativePath(src, file);
            var target   = Path.Combine(dst, relative);

            if (File.Exists(target)) continue; // already migrated

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            try { File.Copy(file, target); }
            catch { /* best-effort — a missing cache file is not fatal */ }
        }
    }

    // ── Step 3: rewrite .lnk shortcuts ────────────────────────────────────────

    private static void RewriteShortcuts(string oldRoot, string? newExePath)
    {
        if (string.IsNullOrEmpty(newExePath)) return;

        var searchRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        };

        // Old exe path pattern (case-insensitive suffix match)
        const string oldExeSuffix  = @"SteamSwitcher\SteamSwitcher.exe";

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return;

        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            // IgnoreInaccessible skips ACL-protected subdirectories (e.g. "Start Menu\Programmes")
            // without throwing UnauthorizedAccessException during iteration.
            var enumOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible    = true,
            };

            foreach (var root in searchRoots)
            {
                if (!Directory.Exists(root)) continue;

                foreach (var lnkPath in Directory.EnumerateFiles(root, "*.lnk", enumOptions))
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

    // ── Step 4: ask about deleting the old install ────────────────────────────

    private static bool PromptDeleteOldInstall(string oldRoot)
    {
        var result = MessageBox.Show(
            $"A previous SteamSwitcher install was found at:\n{oldRoot}\n\n" +
            "Your game cache and icons have been copied to EnigmaLauncher, and any shortcuts " +
            "pointing to the old app have been updated to launch the new one.\n\n" +
            "Delete the old install folder now?",
            "EnigmaLauncher — Remove Old SteamSwitcher Install",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    private static void DeleteOldInstall(string oldRoot)
    {
        try { Directory.Delete(oldRoot, recursive: true); }
        catch { /* best-effort — a locked file shouldn't abort migration */ }
    }

    /// <summary>
    /// Removes any Desktop <c>.lnk</c> still targeting a path under <paramref name="oldRoot"/>
    /// that step 3 didn't already rewrite (e.g. a shortcut to the old folder itself, or to a
    /// differently-named exe inside it) — now dangling since the folder is gone.
    /// </summary>
    private static void CleanupDanglingDesktopShortcuts(string oldRoot)
    {
        var desktops = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return;

        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            foreach (var dir in desktops)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var lnkPath in Directory.EnumerateFiles(dir, "*.lnk"))
                {
                    try
                    {
                        dynamic sc = shell.CreateShortcut(lnkPath);
                        try
                        {
                            var target = (string?)sc.TargetPath;
                            if (!string.IsNullOrEmpty(target) &&
                                target.StartsWith(oldRoot, StringComparison.OrdinalIgnoreCase))
                            {
                                File.Delete(lnkPath);
                            }
                        }
                        finally { Marshal.ReleaseComObject(sc); }
                    }
                    catch { /* a bad shortcut shouldn't block cleanup */ }
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(shell);
        }
    }
}
