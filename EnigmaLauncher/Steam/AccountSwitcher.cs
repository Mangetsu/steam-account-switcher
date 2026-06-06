using System.Diagnostics;
using System.IO;
using System.Text;

namespace EnigmaLauncher.Steam;

public class AccountSwitcher
{
    private const int KillTimeoutSeconds = 8;
    private const int SteamReadyTimeoutSeconds = 45;

    // All Steam-related process names to kill (except steamservice.exe which runs as a
    // Windows Service in Session 0 — killing it requires admin rights and isn't needed).
    private static readonly string[] SteamProcessNames =
    [
        "steam",
        "steamwebhelper",
        "gameoverlayrenderer64",
        "gameoverlayrenderer",
        "steamerrorreporter64",
        "steamerrorreporter",
        "steamcrashhandler",
    ];

    private readonly SteamConfig _config;
    private readonly AccountManager _accounts;

    public AccountSwitcher(SteamConfig config, AccountManager accounts)
    {
        _config = config;
        _accounts = accounts;
    }

    public bool IsSwitchNeeded(SteamAccount target)
    {
        var current = _accounts.GetCurrentAccount();
        return current is null || current.SteamId64 != target.SteamId64;
    }

    // ── Public switch + launch API ─────────────────────────────────────────────

    /// <summary>
    /// Switches to <paramref name="target"/> and then launches <paramref name="appId"/>.
    /// </summary>
    public async Task SwitchAndLaunchAsync(SteamAccount target, int appId, IProgress<string>? status = null)
    {
        if (!await SwitchCoreAsync(target, status)) return;

        await Task.Delay(1500);
        status?.Report("Launching game!");
        LaunchGameUri(appId);
    }

    /// <summary>
    /// Switches to <paramref name="target"/> and leaves Steam running signed-in,
    /// without launching any game.
    /// </summary>
    public async Task SwitchOnlyAsync(SteamAccount target, IProgress<string>? status = null)
    {
        var ready = await SwitchCoreAsync(target, status);
        var name = target.PersonaName.Length > 0 ? target.PersonaName : target.AccountName;
        status?.Report(ready
            ? $"Signed in as {name}!"
            : "Steam took too long to sign in. Check your taskbar.");
    }

    // ── Shared switch core ─────────────────────────────────────────────────────

    /// <summary>
    /// Performs all account-switching steps (registry, file patches, double-start)
    /// up to and including waiting for Steam to be fully signed in.
    /// Returns <c>true</c> if Steam is ready, <c>false</c> on timeout.
    /// </summary>
    private async Task<bool> SwitchCoreAsync(SteamAccount target, IProgress<string>? status)
    {
        // 1. Write registry first — safe while Steam is still alive.
        status?.Report("Preparing account switch...");
        _config.SetAutoLogin(target.AccountName);

        // 2. Kill all Steam processes and let them flush files to disk.
        status?.Report("Closing Steam...");
        await KillSteamProcessesAsync();
        await Task.Delay(2500);

        // 3. Clear stale ActiveProcess registry + patch config/loginusers VDFs.
        //    Must happen after Steam has exited so it cannot overwrite our changes.
        status?.Report("Configuring login profile...");
        _config.ClearActiveProcess();
        PatchConfigVdf();
        PatchLoginUsersVdf(target.SteamId64);

        // 4. First Steam start — Steam reads our patches and writes its own internal
        //    state for the target account.  It may briefly show a chooser prompt;
        //    we kill it before the user can interact, then restart cleanly.
        status?.Report("Initialising account session...");
        LaunchSteamOnly();
        await WaitForSteamProcessAsync(10);
        await Task.Delay(4000);
        await KillSteamProcessesAsync();
        await Task.Delay(1000);

        // 5. Second Steam start — internal state now matches the target account,
        //    so Steam auto-logs in silently via -silent.
        status?.Report("Starting Steam...");
        LaunchSteamOnly();

        // 6. Wait for Steam to be fully signed in.
        status?.Report("Waiting for Steam to sign in...");
        var ready = await WaitForSteamReadyAsync(SteamReadyTimeoutSeconds, status);
        if (!ready)
            status?.Report("Steam took too long to sign in. Check your taskbar.");
        return ready;
    }

    /// <summary>
    /// Fast path: Steam is already running with the right account.
    /// Sends the game launch command via the steam:// URI so we don't start
    /// a second Steam process unnecessarily.
    /// </summary>
    public void LaunchDirect(int appId) => LaunchGameUri(appId);

    /// <summary>
    /// Starts Steam with no game argument so it silently auto-logs in.
    /// -silent suppresses all startup UI (including the account chooser) while still
    /// honouring AutoLoginUser.  UseShellExecute=true is required so Windows sets
    /// the correct working directory and service context (same as double-clicking Steam).
    /// </summary>
    private void LaunchSteamOnly()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName        = _config.SteamExe,
            Arguments       = "-silent",
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Sends a steam://rungameid/ URI to the already-running Steam client.
    /// UseShellExecute=true is required; it routes the URI through the Windows
    /// steam:// protocol handler which forwards it to the running Steam instance.
    /// </summary>
    private static void LaunchGameUri(int appId)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName        = $"steam://rungameid/{appId}",
            UseShellExecute = true
        });
    }

    // ── config.vdf patching ───────────────────────────────────────────────────

    /// <summary>
    /// Ensures <c>AlwaysShowUserChooser</c> is set to 0 in Steam's config.vdf.
    /// When this value is non-zero, Steam ignores AutoLoginUser and shows the
    /// "Who's playing?" account-picker regardless of any other settings.
    /// Must be called AFTER Steam has exited (otherwise Steam overwrites the file).
    /// </summary>
    private void PatchConfigVdf()
    {
        // config.vdf lives next to loginusers.vdf in <SteamPath>\config\
        var path = Path.Combine(Path.GetDirectoryName(_config.LoginUsersVdf)!, "config.vdf");
        if (!File.Exists(path)) return;

        var lines = File.ReadAllText(path, Encoding.UTF8).Split('\n');
        bool found = false;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("AlwaysShowUserChooser"))
            {
                lines[i] = ReplaceTrailingValue(lines[i], "0");
                found = true;
                break;
            }
        }

        // Key may be absent entirely — in that case we don't need to add it;
        // the default when missing is 0 (no picker).  Only write if we changed something.
        if (found)
            File.WriteAllText(path, string.Join('\n', lines), Encoding.UTF8);
    }

    // ── loginusers.vdf patching ────────────────────────────────────────────────

    private void PatchLoginUsersVdf(long targetSteamId64)
    {
        var path = _config.LoginUsersVdf;
        if (!File.Exists(path)) return;

        var original = File.ReadAllText(path, Encoding.UTF8);
        var patched  = ApplyVdfPatch(original, targetSteamId64);
        File.WriteAllText(path, patched, Encoding.UTF8);
    }

    private static string ApplyVdfPatch(string content, long targetSteamId64)
    {
        var lines = content.Split('\n');
        long currentUserId = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();

            // Detect a SteamID64 line — a line whose entire trimmed content is a quoted
            // 17-digit number in the SteamID64 range (76561190000000000–76561199999999999).
            if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length > 2)
            {
                var inner = trimmed[1..^1];
                if (long.TryParse(inner, out var id) && id > 76561190000000000L)
                    currentUserId = id;
            }

            bool isTarget = (currentUserId == targetSteamId64);

            if (trimmed.StartsWith("\"MostRecent\""))
                lines[i] = ReplaceTrailingValue(lines[i], isTarget ? "1" : "0");
            else if (trimmed.StartsWith("\"AllowAutoLogin\""))
                lines[i] = ReplaceTrailingValue(lines[i], isTarget ? "1" : "0");
            else if (trimmed.StartsWith("\"WantsOfflineMode\"") && isTarget)
                lines[i] = ReplaceTrailingValue(lines[i], "0");
            else if (trimmed.StartsWith("\"Timestamp\"") && isTarget)
                // Steam uses Timestamp (Unix seconds) as the primary "most recently used"
                // signal.  If another account has a higher timestamp our MostRecent=1 patch
                // loses the tie-break and Steam falls back to showing the account picker.
                // Stamping the target as "now" ensures it always wins.
                lines[i] = ReplaceTrailingValue(lines[i],
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Replaces the trailing quoted value on a VDF line.
    /// e.g.  '\t\t"MostRecent"\t\t"0"'  →  '\t\t"MostRecent"\t\t"1"'
    /// </summary>
    private static string ReplaceTrailingValue(string line, string newValue)
    {
        var lastClose  = line.LastIndexOf('"');
        if (lastClose < 0) return line;
        var lastOpen = line.LastIndexOf('"', lastClose - 1);
        if (lastOpen < 0 || lastOpen == lastClose) return line;
        return string.Concat(line.AsSpan(0, lastOpen), "\"", newValue, "\"");
    }

    // ── Process management ────────────────────────────────────────────────────

    private static async Task KillSteamProcessesAsync()
    {
        var targets = Process.GetProcesses()
            .Where(p =>
            {
                try { return SteamProcessNames.Contains(p.ProcessName.ToLowerInvariant()); }
                catch { return false; }
            })
            .ToList();

        // First pass: polite terminate
        foreach (var proc in targets)
            try { proc.Kill(); } catch { }

        // Wait for clean exit
        var deadline = DateTime.UtcNow.AddSeconds(KillTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(400);
            if (targets.All(p => { try { return p.HasExited; } catch { return true; } }))
                return;
        }

        // Force-kill survivors (including their child process trees)
        foreach (var proc in targets)
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }

        // Final settle
        await Task.Delay(500);
    }

    /// <summary>
    /// Waits until a <c>steam.exe</c> process is visible in the process list,
    /// or the timeout elapses. Used to confirm the first-pass launch has started
    /// before we kill it again.
    /// </summary>
    private static async Task WaitForSteamProcessAsync(int timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (Process.GetProcessesByName("steam").Length > 0)
                return;
            await Task.Delay(500);
        }
    }

    private async Task<bool> WaitForSteamReadyAsync(int timeoutSeconds, IProgress<string>? status)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        int ticks = 0;

        while (DateTime.UtcNow < deadline)
        {
            var pid        = _config.GetActiveSteamPid();
            var activeUser = _config.GetActiveUserSteamId3();

            if (pid != 0 && activeUser != 0)
                return true;

            await Task.Delay(500);
            ticks++;
            if (ticks % 4 == 0)
                status?.Report($"Waiting for Steam... ({ticks / 2}s / {timeoutSeconds}s)");
        }

        return false;
    }
}
