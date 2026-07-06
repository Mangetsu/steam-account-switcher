using System.IO;
using System.Linq;
using System.Net.Http;

namespace EnigmaLauncher.Steam;

public class ArtworkResolver
{
    private static readonly string[] PreferredFiles =
    [
        "library_600x900.jpg",
        "library_capsule.jpg",
        "library_header.jpg",
        "header.jpg"
    ];

    // header.jpg is a low-res landscape (460x215) asset. Stretched into the card's tall
    // portrait slot it looks cropped and pixelated, so it's only used as a last-resort
    // fallback — never preferred over downloading proper portrait art from the CDN.
    private static readonly string[] GoodLocalFiles =
    [
        "library_600x900.jpg",
        "library_capsule.jpg",
        "library_header.jpg"
    ];

    private static readonly string[] FallbackLocalFiles = ["header.jpg"];

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

    public string? GetLocalArtworkPath(int appId) =>
        FindLocalFile(appId, GoodLocalFiles);

    public string? GetLocalFallbackArtworkPath(int appId) =>
        FindLocalFile(appId, FallbackLocalFiles);

    private string? FindLocalFile(int appId, string[] filenames)
    {
        var appCacheDir = Path.Combine(_config.AppCachePath, appId.ToString());
        if (!Directory.Exists(appCacheDir)) return null;

        // Steam nests each image type in its own hash-named subdirectory, so priority must be
        // resolved across ALL subdirs for one filename before falling back to the next filename —
        // otherwise whichever subdir the filesystem happens to enumerate first wins, regardless
        // of whether a better-quality file lives in another subdir.
        var subDirs = Directory.EnumerateDirectories(appCacheDir).ToList();

        foreach (var filename in filenames)
        {
            var flat = Path.Combine(appCacheDir, filename);
            if (File.Exists(flat)) return flat;

            foreach (var subDir in subDirs)
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

        // CDN unreachable or has nothing for this app — fall back to whatever Steam
        // itself cached locally, even if it's the low-res landscape header.
        return GetLocalFallbackArtworkPath(appId);
    }

    public string? GetIconPath(int appId)
    {
        var iconPath = Path.Combine(AppPaths.IconsDirectory, $"{appId}.ico");
        return File.Exists(iconPath) ? iconPath : null;
    }
}
