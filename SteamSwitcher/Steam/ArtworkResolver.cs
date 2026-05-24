using System.IO;
using System.Net.Http;

namespace SteamSwitcher.Steam;

public class ArtworkResolver
{
    private static readonly string[] PreferredFiles =
    [
        "library_600x900.jpg",
        "library_capsule.jpg",
        "library_header.jpg",
        "header.jpg"
    ];

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private const string CdnUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/{0}/{1}";

    private readonly SteamConfig _config;
    private readonly string _downloadCacheDir;

    public ArtworkResolver(SteamConfig config)
    {
        _config = config;
        _downloadCacheDir = AppPaths.CacheDirectory;
        Directory.CreateDirectory(_downloadCacheDir);
    }

    public string? GetLocalArtworkPath(int appId)
    {
        var appCacheDir = Path.Combine(_config.AppCachePath, appId.ToString());
        if (!Directory.Exists(appCacheDir)) return null;

        // Flat layout: files directly in appCacheDir
        foreach (var filename in PreferredFiles)
        {
            var flat = Path.Combine(appCacheDir, filename);
            if (File.Exists(flat)) return flat;
        }

        // Subdir layout: files inside hash-named subdirectories
        foreach (var subDir in Directory.EnumerateDirectories(appCacheDir))
        {
            foreach (var filename in PreferredFiles)
            {
                var nested = Path.Combine(subDir, filename);
                if (File.Exists(nested)) return nested;
            }
        }

        return null;
    }

    public string? GetCachedDownloadPath(int appId)
    {
        var dir = Path.Combine(_downloadCacheDir, appId.ToString());
        foreach (var filename in PreferredFiles)
        {
            var path = Path.Combine(dir, filename);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    public async Task<string?> DownloadArtworkAsync(int appId)
    {
        var dir = Path.Combine(_downloadCacheDir, appId.ToString());
        Directory.CreateDirectory(dir);

        foreach (var filename in new[] { "library_600x900.jpg", "header.jpg" })
        {
            var url = string.Format(CdnUrl, appId, filename);
            var dest = Path.Combine(dir, filename);
            try
            {
                var bytes = await Http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(dest, bytes);
                return dest;
            }
            catch { }
        }

        return null;
    }

    public string? GetIconPath(int appId)
    {
        var iconPath = Path.Combine(AppPaths.IconsDirectory, $"{appId}.ico");
        return File.Exists(iconPath) ? iconPath : null;
    }
}
