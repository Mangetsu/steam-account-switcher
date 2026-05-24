using System.IO;
using ValveKeyValue;

namespace SteamSwitcher.Steam;

public class AccountManager
{
    private readonly SteamConfig _config;
    private List<SteamAccount>? _accounts;

    public AccountManager(SteamConfig config) => _config = config;

    public List<SteamAccount> LoadAccounts()
    {
        if (_accounts is not null) return _accounts;

        if (!File.Exists(_config.LoginUsersVdf))
            throw new FileNotFoundException($"loginusers.vdf not found at '{_config.LoginUsersVdf}'.");

        using var stream = File.OpenRead(_config.LoginUsersVdf);
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        var data = kv.Deserialize(stream);

        var accounts = new List<SteamAccount>();
        foreach (var userNode in data)
        {
            if (!long.TryParse(userNode.Name, out var steamId64)) continue;

            var accountName = userNode["AccountName"]?.ToString() ?? string.Empty;
            var personaName = userNode["PersonaName"]?.ToString() ?? string.Empty;
            var rememberPw = userNode["RememberPassword"]?.ToString() == "1";
            var allowAutoLogin = userNode["AllowAutoLogin"]?.ToString() == "1";
            var mostRecent = userNode["MostRecent"]?.ToString() == "1";

            accounts.Add(new SteamAccount
            {
                SteamId64 = steamId64,
                SteamId3 = SteamAccount.ToSteamId3(steamId64),
                AccountName = accountName,
                PersonaName = personaName,
                RememberPassword = rememberPw,
                AllowAutoLogin = allowAutoLogin,
                MostRecent = mostRecent
            });
        }

        _accounts = accounts;
        return accounts;
    }

    public SteamAccount? GetCurrentAccount()
    {
        var accounts = LoadAccounts();

        // Primary: use live ActiveProcess\ActiveUser DWORD
        var activeId3 = _config.GetActiveUserSteamId3();
        if (activeId3 != 0)
        {
            var match = accounts.FirstOrDefault(a => a.SteamId3 == activeId3);
            if (match is not null) return match;
        }

        // Fallback: match AutoLoginUser string (Steam not running)
        var autoLoginName = _config.GetCurrentAccountName();
        if (!string.IsNullOrEmpty(autoLoginName))
            return accounts.FirstOrDefault(a =>
                string.Equals(a.AccountName, autoLoginName, StringComparison.OrdinalIgnoreCase));

        return null;
    }

    public SteamAccount? GetBySteamId64(long steamId64) =>
        LoadAccounts().FirstOrDefault(a => a.SteamId64 == steamId64);

    public void InvalidateCache() => _accounts = null;
}
