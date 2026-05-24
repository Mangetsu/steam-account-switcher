namespace SteamSwitcher.Steam;

public class SteamAccount
{
    public long SteamId64 { get; init; }
    public int SteamId3 { get; init; }   // lower 32 bits of SteamId64 - base
    public string AccountName { get; init; } = string.Empty;
    public string PersonaName { get; init; } = string.Empty;
    public bool RememberPassword { get; init; }
    public bool AllowAutoLogin { get; init; }
    public bool MostRecent { get; init; }

    private const long SteamId64Base = 76561197960265728L;

    public static int ToSteamId3(long steamId64) => (int)(steamId64 - SteamId64Base);
    public static long ToSteamId64(int steamId3) => steamId3 + SteamId64Base;

    // AllowAutoLogin only controls Steam's own auto-login UI, not programmatic switching.
    // As long as credentials are cached (RememberPassword), registry-based switching works.
    public bool CanAutoSwitch => RememberPassword;

    public override string ToString() => PersonaName.Length > 0 ? PersonaName : AccountName;
}
