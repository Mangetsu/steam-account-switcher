using System.IO;
using ValveKeyValue;

namespace EnigmaLauncher.Steam;

public class LibraryScanner
{
    private static readonly HashSet<int> ExcludedAppIds = [228980]; // Steamworks Common Redistributables

    private readonly SteamConfig _config;

    public LibraryScanner(SteamConfig config) => _config = config;

    public List<GameEntry> Scan()
    {
        var libraryPaths = GetLibraryPaths();
        var userdataOwnersByAppId = GetUserdataOwnersByAppId();
        var games = new List<GameEntry>();

        foreach (var libraryPath in libraryPaths)
        {
            var steamAppsDir = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamAppsDir)) continue;

            foreach (var acfFile in Directory.GetFiles(steamAppsDir, "appmanifest_*.acf"))
            {
                try
                {
                    var entry = ParseAcf(acfFile, libraryPath);
                    if (entry is null) continue;

                    games.Add(entry);
                    if (!userdataOwnersByAppId.TryGetValue(entry.AppId, out var owners)) continue;

                    foreach (var owner in owners)
                    {
                        if (owner != entry.LastOwnerSteamId64)
                            games.Add(CloneWithOwner(entry, owner));
                    }
                }
                catch
                {
                    // Skip corrupt/unreadable manifests silently
                }
            }
        }

        // Keep one card per game/account pair. The same AppId can be owned by
        // multiple remembered accounts, and the user needs to choose which one launches.
        return [.. games
            .GroupBy(g => new { g.AppId, g.LastOwnerSteamId64 })
            .Select(g => g.First())
            .OrderBy(g => g.Name)
            .ThenBy(g => g.LastOwnerSteamId64)];
    }

    public GameEntry? FindGame(int appId, long? ownerSteamId64 = null)
    {
        return Scan().FirstOrDefault(g =>
            g.AppId == appId
            && (ownerSteamId64 is null || g.LastOwnerSteamId64 == ownerSteamId64));
    }

    private List<string> GetLibraryPaths()
    {
        var paths = new List<string> { _config.SteamPath };

        if (!File.Exists(_config.LibraryFoldersVdf)) return paths;

        try
        {
            using var stream = File.OpenRead(_config.LibraryFoldersVdf);
            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var data = kv.Deserialize(stream);

            foreach (var entry in data)
            {
                var rawPath = entry["path"]?.ToString();
                if (rawPath is null) continue;
                // Normalize slashes and resolve to absolute to ensure consistent comparison
                var normalized = Path.GetFullPath(rawPath.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(normalized) && !paths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    paths.Add(normalized);
            }
        }
        catch { }

        return paths;
    }

    private Dictionary<int, HashSet<long>> GetUserdataOwnersByAppId()
    {
        var result = new Dictionary<int, HashSet<long>>();
        var userdataDir = Path.Combine(_config.SteamPath, "userdata");
        if (!Directory.Exists(userdataDir)) return result;

        var knownOwners = GetKnownAccountIds();

        foreach (var userDir in Directory.EnumerateDirectories(userdataDir))
        {
            var userName = Path.GetFileName(userDir);
            if (!uint.TryParse(userName, out var steamId3)) continue;

            var steamId64 = 76561197960265728L + steamId3;
            if (knownOwners.Count > 0 && !knownOwners.Contains(steamId64)) continue;

            var localConfig = Path.Combine(userDir, "config", "localconfig.vdf");
            if (!File.Exists(localConfig)) continue;

            foreach (var appId in GetTicketedAppIds(localConfig))
            {
                if (ExcludedAppIds.Contains(appId)) continue;

                if (!result.TryGetValue(appId, out var owners))
                {
                    owners = [];
                    result[appId] = owners;
                }

                owners.Add(steamId64);
            }
        }

        return result;
    }

    private static HashSet<int> GetTicketedAppIds(string localConfigPath)
    {
        var appIds = new HashSet<int>();

        try
        {
            using var reader = File.OpenText(localConfigPath);
            while (reader.ReadLine() is { } line)
            {
                var sectionName = GetSectionName(line);
                if (sectionName is not ("apptickets" or "nettickets")) continue;

                AddTicketSectionAppIds(reader, appIds);
            }
        }
        catch
        {
            // Ignore malformed or locked userdata files; manifests still provide the base list.
        }

        return appIds;
    }

    private static string? GetSectionName(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3 || trimmed[0] != '"' || trimmed[^1] != '"') return null;

        return trimmed[1..^1];
    }

    private static void AddTicketSectionAppIds(StreamReader reader, HashSet<int> appIds)
    {
        var depth = 0;

        while (reader.ReadLine() is { } line)
        {
            if (line.Contains('{'))
            {
                depth++;
                continue;
            }

            if (line.Contains('}'))
            {
                depth--;
                if (depth <= 0) return;
                continue;
            }

            if (depth != 1) continue;

            var trimmed = line.TrimStart();
            if (trimmed.Length < 3 || trimmed[0] != '"') continue;

            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote <= 1) continue;

            if (int.TryParse(trimmed[1..endQuote], out var appId))
                appIds.Add(appId);
        }
    }

    private HashSet<long> GetKnownAccountIds()
    {
        try
        {
            return [.. new AccountManager(_config).LoadAccounts().Select(a => a.SteamId64)];
        }
        catch
        {
            return [];
        }
    }

    private static GameEntry CloneWithOwner(GameEntry entry, long ownerSteamId64)
    {
        return new GameEntry
        {
            AppId = entry.AppId,
            Name = entry.Name,
            InstallDir = entry.InstallDir,
            LastOwnerSteamId64 = ownerSteamId64,
            LibraryPath = entry.LibraryPath,
            StateFlags = entry.StateFlags
        };
    }

    private static GameEntry? ParseAcf(string acfPath, string libraryPath)
    {
        using var stream = File.OpenRead(acfPath);
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        var data = kv.Deserialize(stream);

        if (!int.TryParse(data["appid"]?.ToString(), out var appId)) return null;
        if (ExcludedAppIds.Contains(appId)) return null;

        var name = data["name"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)) return null;

        if (!int.TryParse(data["StateFlags"]?.ToString(), out var stateFlags)) return null;
        if ((stateFlags & 4) == 0) return null;

        long.TryParse(data["LastOwner"]?.ToString(), out var lastOwner);
        var installDir = data["installdir"]?.ToString() ?? string.Empty;

        return new GameEntry
        {
            AppId = appId,
            Name = name,
            InstallDir = installDir,
            LastOwnerSteamId64 = lastOwner,
            LibraryPath = libraryPath,
            StateFlags = stateFlags
        };
    }
}
