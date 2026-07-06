# Architecture

This document explains how EnigmaLauncher works under the hood — the store abstraction,
switching mechanism, file-patching strategy, and design decisions.

---

## Store abstraction

EnigmaLauncher uses a thin store abstraction layer so additional game libraries (Epic, GOG, Xbox)
can be added later without touching the UI or launch flow.

| Interface | Purpose |
|---|---|
| `IGameStore` | Scan installed games, resolve artwork, launch a game |
| `IAccountStore` | Extends `IGameStore`; adds account listing, current-account detection, and switch operations |
| `GameInfo` | Store-agnostic game model (`StoreId`, `GameId`, `Name`, `OwnerAccountId`, …) |
| `AccountInfo` | Store-agnostic account model (`StoreId`, `AccountId`, `DisplayName`, `CanAutoSwitch`, …) |
| `StoreRegistry` | Discovers and holds all registered stores; aggregates games and accounts across them |
| `SteamStore` | Implements `IAccountStore` and `IStoreClientActions`; delegates to the internal `Steam/` classes |

Steam is currently the only active store. The UI binds exclusively to store interfaces plus
`GameInfo`/`AccountInfo` — no Steam-specific types leak into the presentation layer.
Optional client navigation is exposed through `IStoreClientActions`, allowing the header to start
the client and game cards to open library details without coupling the UI to Steam classes.

---

## Account switching overview

Steam stores the "active" account in two places simultaneously:

| Location | Key / File | Purpose |
|---|---|---|
| Registry | `HKCU\SOFTWARE\Valve\Steam\AutoLoginUser` | Which account to auto-login as |
| Registry | `HKCU\SOFTWARE\Valve\Steam\RememberPassword` | Whether to skip the password prompt |
| Registry | `HKCU\SOFTWARE\Valve\Steam\ActiveProcess\ActiveUser` | Live SteamID3 of the running session |
| Registry | `HKCU\SOFTWARE\Valve\Steam\ActiveProcess\pid` | PID of the running steam.exe |
| File | `<Steam>\config\loginusers.vdf` | Per-account flags: MostRecent, AllowAutoLogin, Timestamp |
| File | `<Steam>\config\config.vdf` | Global flag: AlwaysShowUserChooser |

A switch requires **all of these** to agree. If even one is stale, Steam falls back to showing
an account-picker dialog instead of logging in silently.

---

## Single-start switching

After Steam exits, EnigmaLauncher patches the registry and VDF files, then starts Steam once with
`steam.exe -silent`.

The switch sequence is:

```
Kill Steam → patch files → start Steam with -silent → wait for sign-in
```

Steam reads the patched target account and auto-logs in via `-silent`. EnigmaLauncher then polls
`ActiveProcess\ActiveUser` until it is non-zero. There is no intermediate launch and second kill.

---

## VDF patching

### `loginusers.vdf`

For the target account we set:

| Field | Value | Reason |
|---|---|---|
| `MostRecent` | `"1"` | Marks this as the last-used account |
| `AllowAutoLogin` | `"1"` | Tells Steam to auto-login without a prompt |
| `WantsOfflineMode` | `"0"` | Don't start in offline mode |
| `Timestamp` | `<now>` | **Critical:** Steam uses this as the primary tie-break. If another account has a higher timestamp, Steam ignores MostRecent and shows the picker. |

All other accounts get `MostRecent` and `AllowAutoLogin` set to `"0"`.

The parser is a simple line-by-line scanner (`ApplyVdfPatch` in `AccountSwitcher.cs`) that detects
SteamID64 section headers (17-digit numbers in the `7656119xxxxxxxxxx` range) to track which account
each block belongs to, then rewrites the trailing quoted value on matching key lines.

### `config.vdf`

We patch `AlwaysShowUserChooser` to `"0"`. When set to `"1"`, Steam ignores `AutoLoginUser` entirely
and always shows the account picker. We only write the file if the key is present and its value
differs (detected as `found=true` after the scan).

---

## Game → account mapping

`.acf` (App Content File) manifests live in each Steam library's `steamapps/` folder:

```
steamapps/appmanifest_<appid>.acf
```

The `LastOwner` field contains the SteamID64 of the account that last installed or played the game.
`LibraryScanner` reads all `.acf` files, filters to installed games (`StateFlags & 4 != 0`),
deduplicates by `(AppID, LastOwner)`, and builds a `GameEntry` list.

The same game can appear more than once when different remembered accounts own it. That is
intentional: each card represents a specific account launch choice. True duplicates for the same
game/account pair across multiple library scans are collapsed to the first matching manifest.

Steam's manifest only has one `LastOwner`, so the scanner also checks the `apptickets` and
`nettickets` sections in `<Steam>\userdata\<steamid3>\config\localconfig.vdf` for remembered
accounts with a stronger per-app account signal. If another remembered account has a ticket for
that AppID, the scanner adds a second account-specific `GameEntry` for the same installed
manifest. Cloud-only app stubs are ignored because Steam can create them for accounts that should
not receive a playable card.

---

## Game launch flow

```
Shortcut double-click
    ↓
EnigmaLauncher.exe --launch <appid> [--owner <steamid64>]
    ↓
LaunchWindow opens (floating progress dialog)
    ↓
SteamStore.BuildLaunchOperation(game)
    ├─ Find GameInfo for appId  (LibraryScanner.FindGame)
    ├─ Find owner account       (AccountManager.GetBySteamId64)
    ├─ Compare to current session (ActiveProcess\ActiveUser)
    │
    ├─ [Same account or no owner]
    │       steam://rungameid/<appid>   → game launches
    │
    └─ [Different account needed]
            AccountSwitcher.SwitchAndLaunchAsync
                ├─ Patch registry + VDF files
                ├─ Start Steam once with -silent
                ├─ Poll ActiveProcess\ActiveUser every 500 ms (45 s timeout)
                └─ steam://rungameid/<appid>   → game launches
```

The card's **Open in Steam Library** action follows the same owner comparison. When the selected
card belongs to another remembered account, `SwitchAndOpenLibraryAsync` completes the account
switch first; it then navigates with `steam://nav/games/details/<appid>` instead of sending a
`rungameid` command, so the game is not launched. The header **Steam** button starts
`steam.exe -silent` directly and does not alter the selected account.

---

## Artwork resolution

Priority order for each game:

1. **Local Steam cache (good quality)** — `<Steam>\appcache\librarycache\<appid>\library_600x900.jpg`,
   `library_capsule.jpg`, or `library_header.jpg`, checked flat and in hash subdirectories (Steam
   sometimes nests files)
2. **CDN download** — `https://cdn.cloudflare.steamstatic.com/steam/apps/<appid>/library_600x900.jpg`
   saved to `%LOCALAPPDATA%\EnigmaLauncher\data\cache\<appid>\`
3. **CDN download (fallback)** — same CDN, `header.jpg` (460×215 landscape)
4. **Local Steam cache (fallback)** — `<appid>\header.jpg`, used only when the CDN is unreachable
   or has nothing for that app

`header.jpg` is deliberately last: it's a low-res landscape asset, and stretching it into the
card's tall portrait slot looks cropped and pixelated. It's only ever used when nothing better
is available locally or from the CDN.

Downloads happen on a background `Task.Run` per game and are applied via `Dispatcher.InvokeAsync`
without blocking the UI.

Clicking **Refresh** in the header deletes everything under `data\cache\` (`MainWindow.ClearArtworkCache`)
before rescanning, so a bad or corrupt downloaded cover gets re-fetched clean on retry. Steam's own
local cache under `appcache\librarycache\` is untouched (it isn't ours to delete), as are
`data\icons\` and `data\settings.json` (per-game display settings, migration flag).

---

## Desktop shortcuts

Shortcuts are `.lnk` files created via the `WScript.Shell` COM object (dynamic late binding —
no COM reference needed):

```
Target:   %LOCALAPPDATA%\EnigmaLauncher\EnigmaLauncher.exe
Args:     --launch <appid> --owner <steamid64>
Icon:     %LOCALAPPDATA%\EnigmaLauncher\data\icons\<appid>.ico
Name:     <GameName>.lnk or <GameName> (<AccountName>).lnk for multi-owner games
```

The icon is extracted from the game's artwork by scaling it to 256×256 with `System.Drawing` and
writing a multi-size `.ico` file.

---

## Migration from SteamSwitcher v1.0.0

On first launch after upgrading from the original SteamSwitcher install
(`%LOCALAPPDATA%\SteamSwitcher\`), `MigrationService` runs automatically:

1. Kills the old `SteamSwitcher.exe` process if it's still running, so its files aren't locked
   for the steps below.
2. Copies `data\cache\` and `data\icons\` to the new location.
3. Scans Desktop and Start Menu for `.lnk` shortcuts pointing to the old `SteamSwitcher.exe`
   and rewrites them to target `EnigmaLauncher.exe`, preserving arguments.
4. Prompts the user with a Yes/No dialog to delete the old `%LOCALAPPDATA%\SteamSwitcher\`
   folder entirely. There is no redirect shortcut left behind — a `.lnk` in the old folder
   can't transparently stand in for the old `.exe` anyway, since a double-clicked shortcut's
   `TargetPath` is resolved and launched directly; it won't chain into another `.lnk`.
   - **Yes:** deletes the folder, then removes any Desktop `.lnk` still targeting a path under
     it that step 3 didn't already catch (e.g. a shortcut to the folder itself, or to a
     differently-named exe inside it) — otherwise it would now point at nothing.
   - **No:** the old folder and any of its shortcuts are left completely untouched.
5. Records the migration as complete in `data\settings.json` either way, so the prompt is
   only ever shown once.

---

## Display switching

### Global switcher

A "Primary screen" pill in the header enumerates active monitors via
`DisplayManager.GetMonitors()` (Win32 `EnumDisplayDevices` / `EnumDisplaySettings`).
Selecting a monitor calls `DisplayManager.SetPrimary(deviceName)`, which:

1. Reads each active monitor's current virtual-desktop position (`DEVMODE.dmPositionX/Y`).
2. Shifts all positions so the target lands at (0, 0) using `ChangeDisplaySettingsEx` with
   `CDS_UPDATEREGISTRY | CDS_NORESET` per monitor — the target device is applied first. Each call's
   `dmFields` declares `DM_POSITION` together with `DM_BITSPERPEL | DM_PELSWIDTH | DM_PELSHEIGHT |
   DM_DISPLAYFREQUENCY` (values unchanged, read from the existing mode) — some drivers reject a
   DEVMODE that only declares `DM_POSITION` with `DISP_CHANGE_FAILED` (return `-1`), expecting a
   fully-described mode even when only the position is actually changing.
3. Commits the batch with a final `ChangeDisplaySettingsEx(null, IntPtr.Zero, …, 0, …)` — this
   commit call requires a true `NULL` devmode pointer, so `DisplayManager` declares a second
   `ChangeDisplaySettingsEx` P/Invoke overload taking `IntPtr` (the primary `ref DEVMODE` overload
   cannot express `NULL`) and uses it only for this final call.

**Troubleshooting `DISP_CHANGE_FAILED` (-1) that persists across code fixes:** repeated failed
`ChangeDisplaySettingsEx` calls (from earlier bugs, crashed test runs, etc.) can leave stale,
inconsistent display-config state in the registry that keeps tripping this legacy API even once
the calling code is correct — `CDS_UPDATEREGISTRY` writes immediately regardless of `CDS_NORESET`.
Manually changing the primary display once via Windows Settings → System → Display (which uses
the modern CCD API, `QueryDisplayConfig`/`SetDisplayConfig`) forces a clean, consistent rewrite of
that state and has been observed to unstick this. If `SetPrimary` starts failing consistently,
try that before assuming the code regressed.

`SetPrimary` must be called on the UI thread — `ChangeDisplaySettingsEx` reliably returns
`DISP_CHANGE_FAILED` when invoked from a `Task.Run` thread-pool thread, even though the call
itself is fast on the UI thread. (An earlier revision routed it through `Task.Run` with a timeout
to guard against a driver hang; that turned out to both break the call and be unnecessary — the
full-DEVMODE fix above is what actually prevents the hang.) Both call sites
(`MainWindow.OnDisplaySwitchItemClick` and `ApplyDisplaySettings`) go through the shared
`MainWindow.SetPrimaryWithTimeoutAsync()` helper, which just calls it directly; the
`SetPrimaryThenRevert` delayed revert (`RevertPrimaryAfterDelayAsync`) uses a plain
`await Task.Delay(...)` (no `Task.Run`) so its continuation resumes on the UI thread too.

### Per-game display settings

Each game card stores a `GameDisplaySettings` entry in `data\settings.json` under the key
`"storeId:gameId"` (e.g. `"steam:730"`).  The record holds:

| Field | Type | Meaning |
|---|---|---|
| `TargetDevice` | `string?` | GDI device name, e.g. `"\\.\DISPLAY2"`. Null → no override. |
| `Method` | `DisplaySwitchMethod` | `None` / `SetPrimary` / `MoveWindow` / `SetPrimaryThenRevert` |

`MainWindow.ApplyDisplaySettings()` wraps the store's `BuildLaunchOperation()` lambda:

- **`SetPrimary`** — calls `DisplayManager.SetPrimary()` before launching the game.
- **`MoveWindow`** — fires a `Task.Run` fire-and-forget that waits 5 s then calls
  `DisplayManager.MoveWindowToMonitor()`, which reads the target monitor's virtual-desktop
  origin from `EnumDisplaySettings` and uses `SetWindowPos` on the current foreground window.
  Best-effort — no crash if the window can't be found.
- **`SetPrimaryThenRevert`** — same as `SetPrimary` before launch, but also records whichever
  monitor was primary beforehand and, `RevertDelaySeconds` after the launch operation completes,
  calls `SetPrimary()` again to switch back to it (fire-and-forget, best-effort). The delay
  (1–60 s, default 8) is configurable per-game via a stepper in the display-settings popup, since
  how long a game takes to create its fullscreen surface varies. Exists so the taskbar
  and notification/volume tray — which only live on the primary monitor by default — return to
  the user's main screen while a fullscreen game keeps running on the target monitor. Risky for
  exclusive-fullscreen games: if the revert lands before the game finishes creating its
  fullscreen swapchain, the game can end up rendering on the wrong monitor, get kicked out of
  exclusive mode, or flicker/black-screen.

  Windows' **Settings → Personalization → Taskbar → Multiple displays → Show taskbar on all
  displays** does *not* fully solve the underlying problem: it mirrors pinned apps and the clock
  onto every monitor, but the full system-tray icons (volume, network, language, action center)
  only ever render on whichever monitor is currently primary, confirmed by testing — even with
  that setting on. `SetPrimaryThenRevert` is the actual fix for getting tray access back while a
  fullscreen game runs on a non-primary monitor, not a risky workaround for a problem that has a
  safer native solution.

Both `SetPrimary` paths above go through `MainWindow.SetPrimaryWithTimeoutAsync()`, described above.

### Display-settings popup and double-click

`GameCard` subscribes to its own `MouseDoubleClick` (a bubbling routed event) to launch the game
on double-click anywhere on the card. A `Popup`'s content renders in a separate visual tree but
stays connected to its host through the *logical* tree, so routed events raised inside the
display-settings popup (the revert-delay stepper's +/- buttons, the monitor/method combo boxes)
still bubble into that handler. Two fast clicks on a stepper button, or two quick combo-box
selections, could be misread as a double-click on the card and launch the game with the
previously-saved settings. `GameCard.OnMouseDoubleClick` now ignores clicks while
`DisplaySettingsPopup.IsOpen` is true.

### Settings persistence

`SettingsStore` reads/writes `data\settings.json` using `System.Text.Json` with
`JsonStringEnumConverter` so method names are human-readable in the file.
On corrupt or missing file it starts with safe defaults.

---

## Key classes

| Class | Responsibility |
|---|---|
| `StoreRegistry` | Discovers and holds all `IGameStore` / `IAccountStore` instances; aggregates games and accounts |
| `SteamStore` | Implements `IAccountStore` and `IStoreClientActions`; adapter between the UI/store layer and Steam internals |
| `SteamConfig` | Registry reader; exposes all Steam paths and live session values |
| `AccountManager` | Parses `loginusers.vdf`, provides current-account detection with registry fallback |
| `LibraryScanner` | Parses `libraryfolders.vdf` + all `.acf` manifests; returns deduplicated `GameEntry` list |
| `AccountSwitcher` | Core switching logic: VDF patching, process kill/start, wait for ready |
| `ArtworkResolver` | Local cache lookup + CDN download |
| `ShortcutCreator` | `.lnk` file creation via WScript.Shell COM + ICO generation |
| `LaunchWindow` | Generic async-operation progress dialog; knows nothing about Steam |
| `MainWindow` | Game grid, filter bar, account switcher dropdown |
| `AboutWindow` | App metadata, install path, license, and repository mirror links |
| `MigrationService` | One-time migration from SteamSwitcher v1.0.0 install |
| `SettingsStore` | Reads/writes `data\settings.json`; stores per-game display preferences and app-window display |
| `DisplayManager` | Enumerates connected monitors; sets the Windows primary display |
