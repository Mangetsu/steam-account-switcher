using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using EnigmaLauncher.Stores;

namespace EnigmaLauncher.Shortcuts;

public class ShortcutCreator
{
    private readonly string _iconDir;

    public ShortcutCreator()
    {
        _iconDir = AppPaths.IconsDirectory;
        Directory.CreateDirectory(_iconDir);
    }

    public string CreateGameShortcut(GameInfo game, string? artworkPath)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        return CreateGameShortcut(game, artworkPath, desktopPath);
    }

    public string CreateGameShortcut(GameInfo game, string? artworkPath, string targetDirectory)
        => CreateGameShortcut(game, artworkPath, targetDirectory, fileNameSuffix: null);

    public string CreateGameShortcut(
        GameInfo game,
        string? artworkPath,
        string targetDirectory,
        string? fileNameSuffix)
    {
        if (!Directory.Exists(targetDirectory))
            throw new DirectoryNotFoundException($"Shortcut destination does not exist: {targetDirectory}");

        var exePath  = GetExePath();
        var safeName = SanitizeFileName(game.Name);
        var lnkPath  = GetAvailableShortcutPath(targetDirectory, safeName, game, fileNameSuffix);
        var iconPath = GetOrCreateIcon(game.GameId, artworkPath) ?? exePath;

        // Use WScript.Shell via late binding — works in .NET 8 without a COM reference
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is not available on this system.");

        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            shortcut.TargetPath       = exePath;
            shortcut.Arguments        = BuildLaunchArguments(game);
            shortcut.WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
            shortcut.IconLocation     = $"{iconPath},0";
            shortcut.Description      = $"Launch {game.Name} via EnigmaLauncher";
            shortcut.Save();
        }
        finally
        {
            Marshal.ReleaseComObject(shell);
        }

        return lnkPath;
    }

    private static string BuildLaunchArguments(GameInfo game)
    {
        var args = $"--launch {game.GameId}";
        if (!string.IsNullOrEmpty(game.OwnerAccountId))
            args += $" --owner {game.OwnerAccountId}";
        return args;
    }

    private static string GetAvailableShortcutPath(
        string targetDirectory,
        string safeName,
        GameInfo game,
        string? fileNameSuffix)
    {
        var suffix = SanitizeFileName(fileNameSuffix ?? string.Empty);
        var path = Path.Combine(targetDirectory, string.IsNullOrWhiteSpace(suffix)
            ? $"{safeName}.lnk"
            : $"{safeName} ({suffix}).lnk");

        if (!File.Exists(path)) return path;

        suffix = !string.IsNullOrEmpty(game.OwnerAccountId)
            ? suffix.Length > 0 ? suffix : game.OwnerAccountId
            : SanitizeFileName(Path.GetFileName(game.LibraryPath ?? string.Empty));

        path = Path.Combine(targetDirectory, $"{safeName} ({suffix}).lnk");
        if (!File.Exists(path)) return path;

        for (var i = 2; ; i++)
        {
            path = Path.Combine(targetDirectory, $"{safeName} ({suffix} {i}).lnk");
            if (!File.Exists(path)) return path;
        }
    }

    private string? GetOrCreateIcon(string gameId, string? artworkPath)
    {
        var iconPath = Path.Combine(_iconDir, $"{gameId}.ico");
        if (File.Exists(iconPath)) return iconPath;
        if (artworkPath is null || !File.Exists(artworkPath)) return null;

        try
        {
            using var bmp = new Bitmap(artworkPath);
            SaveAsIco(bmp, iconPath);
            return iconPath;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveAsIco(Bitmap source, string destPath)
    {
        int[] sizes = [256, 48, 32, 16];
        var pngImages = new List<byte[]>();
        foreach (var size in sizes)
        {
            using var resized = new Bitmap(source, size, size);
            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png);
            pngImages.Add(ms.ToArray());
        }

        using var fs = new FileStream(destPath, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // ICO file header
        bw.Write((short)0);                 // reserved
        bw.Write((short)1);                 // type: icon
        bw.Write((short)sizes.Length);

        // Directory entries: calculate offsets
        int offset = 6 + sizes.Length * 16;
        for (int i = 0; i < sizes.Length; i++)
        {
            int sz = sizes[i];
            bw.Write((byte)(sz >= 256 ? 0 : sz));
            bw.Write((byte)(sz >= 256 ? 0 : sz));
            bw.Write((byte)0);   // color count
            bw.Write((byte)0);   // reserved
            bw.Write((short)1);  // planes
            bw.Write((short)32); // bit depth
            bw.Write(pngImages[i].Length);
            bw.Write(offset);
            offset += pngImages[i].Length;
        }

        foreach (var png in pngImages)
            bw.Write(png);
    }

    private static string GetExePath()
    {
        return System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Cannot determine executable path.");
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
    }
}
