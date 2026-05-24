using System.IO;

namespace SteamSwitcher;

public static class AppPaths
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteamSwitcher",
        "data");

    public static string CacheDirectory => Path.Combine(DataDirectory, "cache");

    public static string IconsDirectory => Path.Combine(DataDirectory, "icons");
}
