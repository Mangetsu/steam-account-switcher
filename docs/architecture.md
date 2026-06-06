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
| `SteamStore` | Implements `IAccountStore`; delegates to the internal `Steam/` classes |

Steam is currently the only active store. The UI binds exclusively to `IAccountStore` and
`GameInfo`/`AccountInfo` — no Steam-specific types leak into the presentation layer.

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

## The double-start pattern

A single `steam.exe -silent` after patching is not enough. On the first launch after a switch,
Steam sometimes shows a chooser prompt because it has internal state (not just VDF files) from
the previous session that conflicts with our patches.

The solution — discovered empirically — is a **two-pass start**:

```
Kill Steam → patch files → [First start] → wait ~4 s → kill again → [Second start] → wait for sign-in
```

**First start:** Steam reads our patches and writes its own internal auth state for the target
account (updating in-memory caches, re-reading loginusers.vdf, etc.). It may briefly show
a UI prompt. We kill it before the user can interact.

**Second start:** Steam's internal state now matches our patches. It auto-logs in silently via
`-silent`, suppressing all startup UI. We then poll `ActiveProcess\ActiveUser` until it's non-zero.

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
                ├─ Double-start Steam (first pass commit, second pass login)
                ├─ Poll ActiveProcess\ActiveUser every 500 ms (45 s timeout)
                └─ steam://rungameid/<appid>   → game launches
```

---

## Artwork resolution

Priority order for each game:

1. **Local Steam cache** — `<Steam>\appcache\librarycache\<appid>\library_600x900.jpg` (flat layout)
2. **Local Steam cache** — same filename in a hash subdirectory (Steam sometimes nests files)
3. **CDN download** — `https://cdn.akamai.steamstatic.com/steam/apps/<appid>/library_600x900.jpg`
   saved to `%LOCALAPPDATA%\EnigmaLauncher\data\cache\<appid>\`

Downloads happen on a background `Task.Run` per game and are applied via `Dispatcher.InvokeAsync`
without blocking the UI.

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

1. Copies `data\cache\` and `data\icons\` to the new location.
2. Scans Desktop and Start Menu for `.lnk` shortcuts pointing to the old `SteamSwitcher.exe`
   and rewrites them to target `EnigmaLauncher.exe`.
3. Records the migration as complete in `data\settings.json`.
4. The old `%LOCALAPPDATA%\SteamSwitcher\` folder is **not deleted** automatically; any shortcuts
   that weren't found by the scan can still run the old exe until the user cleans up manually.

---

## Key classes

| Class | Responsibility |
|---|---|
| `StoreRegistry` | Discovers and holds all `IGameStore` / `IAccountStore` instances; aggregates games and accounts |
| `SteamStore` | Implements `IAccountStore`; adapter between the UI/store layer and Steam internals |
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
