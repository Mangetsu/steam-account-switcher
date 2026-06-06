using System.IO;

namespace EnigmaLauncher;

public static class AppPaths
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EnigmaLauncher",
        "data");

    public static string CacheDirectory => Path.Combine(DataDirectory, "cache");

    public static string IconsDirectory => Path.Combine(DataDirectory, "icons");

    public static string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");
}
