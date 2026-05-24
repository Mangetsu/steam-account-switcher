using System.IO;

namespace SteamSwitcher.Steam;

public class GameEntry
{
    public int AppId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string InstallDir { get; init; } = string.Empty;
    public long LastOwnerSteamId64 { get; init; }
    public string LibraryPath { get; init; } = string.Empty;
    public int StateFlags { get; init; }

    public bool IsFullyInstalled => (StateFlags & 4) != 0;

    public string FullInstallPath => Path.Combine(LibraryPath, "steamapps", "common", InstallDir);
}
