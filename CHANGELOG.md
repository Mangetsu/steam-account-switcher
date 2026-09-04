# Changelog

All notable changes to EnigmaLauncher will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions follow [Semantic Versioning](https://semver.org/).

---

## [2.1.0] — 2026-09-04

- Refined the main header layout, added game search, and added per-game monitor numbers to the
  display settings icon.

## [2.0.0] — 2026-07-06

### Added
- Header button to start or open Steam without launching a game.
- Game-card action to open the game in Steam Library without launching it, automatically switching
  to the selected card's owner account first when needed.
- About dialog with version, runtime, install path, license, and GitHub link.
- Custom shortcut action that prompts for a destination folder before creating the `.lnk`.
- `CLAUDE.md` as the single source of truth for AI agents, with Codex and GitHub Copilot redirect files.
- **Global display switcher** — a "Primary screen:" pill in the main window header (next to the account
  switcher) lists all active monitors; clicking one calls `ChangeDisplaySettingsEx` to make it the
  Windows primary display without leaving the app.
- **Per-game display settings** — each game card exposes a small 🖵 icon button (visible on hover)
  that opens an inline popup with two controls:
  - *Target screen* — a monitor picked from the live `DisplayManager.GetMonitors()` list.
  - *Switch method* — **None** (default, no override); **Set as primary** (swap Windows primary
    before launch — for exclusive-fullscreen games that always open on the primary monitor);
    **Move game window** (wait 5 s after launch then `SetWindowPos` the foreground window to the
    target monitor — best-effort for windowed/borderless games, including many "fullscreen" games
    that are actually borderless-windowed under the hood); or **Set primary, then revert** (same
    as *Set as primary* before launch, then switches primary back to whatever it was a configurable
    number of seconds — 1–60, default 8, via a +/- stepper that appears next to the method picker
    when this method is selected — after launch). The revert variant exists because the taskbar's
    full system-tray icons (volume, network, language, action center) only ever show on the
    primary monitor, even with "show taskbar on all displays" enabled, so a permanent switch loses
    tray access while the game runs; reverting shortly after launch gives it back on the main
    screen while the game keeps running on the target monitor. Riskier than a permanent switch for
    exclusive-fullscreen games if the delay is too short — see `docs/architecture.md` for the
    tradeoffs.
  Settings are persisted per-game in `data\settings.json`.
- `Settings/` layer — `AppSettings`, `GameDisplaySettings` (`DisplaySwitchMethod` enum), and
  `SettingsStore` (read/write `data\settings.json` via `System.Text.Json`).
- `Display/` layer — `MonitorInfo` model and `DisplayManager` (pure Win32 P/Invoke: `EnumDisplayDevices`,
  `EnumDisplaySettings`, `ChangeDisplaySettingsEx`, `SetWindowPos`; no WinForms dependency).
- **Refresh now clears the downloaded artwork cache** (`data\cache\`) before rescanning, so any
  bad/corrupt cover art gets re-fetched clean from Steam's local cache or the CDN on retry.
  Per-game display settings and the migration flag in `settings.json`, plus generated shortcut
  icons in `data\icons\`, are untouched.

### Changed
- Account switching now starts Steam only once after applying the target account, removing the
  intermediate launch, second kill, and relaunch cycle.
- **Rebranded from SteamSwitcher to EnigmaLauncher.** Install path is now
  `%LOCALAPPDATA%\EnigmaLauncher\`. Existing v1.0.0 installations are migrated automatically on
  first launch: the old `SteamSwitcher.exe` process is stopped if still running, `data\cache\`
  and `data\icons\` are copied over, and known Desktop/Start Menu shortcuts are rewritten to the
  new exe. The user is then prompted with a Yes/No dialog to delete the old
  `%LOCALAPPDATA%\SteamSwitcher\` install folder — choosing yes also removes any leftover Desktop
  shortcut still pointing into it; choosing no leaves the old folder and its shortcuts untouched.
- `build.bat` now also stops any running `SteamSwitcher` (v1.0.0) process before installing,
  alongside the existing `EnigmaLauncher` stop step.
- Store abstraction layer added (`Stores/IGameStore`, `IAccountStore`, `GameInfo`, `AccountInfo`,
  `StoreRegistry`) so additional game stores (Epic, GOG, Xbox) can be plugged in later.
  Steam remains the only active store; the library and switching behaviour is unchanged.
- License changed from MIT to PolyForm Noncommercial 1.0.0; non-commercial use remains free,
  commercial use requires a separate paid license.
- Game library deduplication now keeps distinct cards for the same AppID when ownership differs
  by Steam account.
- Game scanning now uses per-user Steam ticket data to detect additional remembered accounts
  for an installed AppID when the manifest only reports one `LastOwner`, avoiding cloud-only
  false positives.
- Game launch and generated shortcuts now preserve the selected owner with `--owner <steamid64>`.
- Multi-owner game shortcuts now use the account display name in the `.lnk` filename instead of
  the SteamID64.
- Build output now keeps the install root tidy: `EnigmaLauncher.exe` at root, runtime files
  under `app\`, and app data under `data\`.
- Game-card action buttons now have icon-leading labels and distinct colors.
- `build.bat` now stops any running EnigmaLauncher process before publishing and launches the
  app after a successful build.
- The custom shortcut folder picker now opens on the Desktop by default.

### Fixed
- Main window default height increased so two rows of game cards fit without showing the vertical
  scrollbar by default.
- About dialog was too short and non-resizable, clipping its content (runtime/install rows).
  It is now resizable and taller by default so all content fits.
- Per-game display settings ComboBoxes (target screen, switch method) used the default Windows
  ComboBox chrome, which rendered a near-white dropdown with low-contrast text over the app's
  dark theme. Added a themed `ComboBox`/`ComboBoxItem` style so the popup list and selection are
  readable against the dark background.
- Game card's per-game display-settings icon (🖥) was too small and low-contrast to notice.
  It is now larger, uses the app's accent blue, and has a higher base opacity.
- Game card hover overlay now shows the full game name above the action buttons, since the
  compact name label at the bottom of the card is truncated on a fixed 200px-wide card.
- Game cards no longer settle for Steam's low-res landscape `header.jpg` (460x215) when that's
  the only artwork Steam has cached locally — it was stretched into the card's tall portrait
  slot and looked cropped and pixelated. Portrait art (`library_600x900.jpg`) is now downloaded
  from the Steam CDN first; the local `header.jpg` is only used as a last-resort fallback when
  the CDN is unreachable or has nothing for that app.
- Fixed the underlying cause of stale/wrong local artwork: Steam nests each image type in its
  own hash-named subdirectory under `librarycache\<appid>\`, but the local-file search checked
  subdirectories one at a time (all filenames in the first subdir, then the next), so whichever
  subdir the filesystem enumerated first won even if a better-quality file (e.g.
  `library_600x900.jpg`) lived in another subdir. The search now resolves one filename across
  every subdirectory before falling back to the next filename.
- Global and per-game "Set as primary" display switching was completely non-functional, in three
  separate ways, all now fixed in `DisplayManager.SetPrimary()`:
  - The final `ChangeDisplaySettingsEx` call meant to commit the queued per-monitor position
    changes passed a non-null-but-empty `DEVMODE`, when Windows requires a true `NULL` devmode
    pointer to apply a batched `CDS_NORESET` change set — so switching silently did nothing.
    Added a second P/Invoke overload that accepts `IntPtr` so a real `NULL` can be passed.
  - Per-monitor position updates were applied in raw device-enumeration order, which can fail
    with `DISP_CHANGE_FAILED` trying to move the *current* primary monitor off (0, 0) before the
    new target has been moved onto it. The target device's position is now applied first.
  - The per-monitor `DEVMODE` only declared `DM_POSITION` in `dmFields`; some drivers reject that
    outright and require the full mode (`DM_BITSPERPEL | DM_PELSWIDTH | DM_PELSHEIGHT |
    DM_DISPLAYFREQUENCY`) declared alongside the position, even though those values aren't
    changing. All four are now declared, with values read straight from the existing mode.

  `SetPrimary()` must also run on the calling (UI) thread — dispatching it through `Task.Run`
  makes `ChangeDisplaySettingsEx` fail every time.
- Per-game display popup: picking a target monitor while *Switch method* was still left at
  **None** silently did nothing, since **None** explicitly means "no override" regardless of which
  monitor is selected. Picking a real monitor now nudges the method to **Set as primary** if it
  was still on **None**, instead of leaving a configuration that has no effect.
- Game cards could launch the game with stale settings while the display-settings popup was open:
  `MouseDoubleClick` is subscribed on the whole card and bubbles, and a `Popup`'s content is
  connected to its host through the logical tree even though it renders in a separate visual
  tree — so two quick clicks on the revert-delay stepper's +/- buttons, or two quick combo-box
  selections, registered as a double-click on the card underneath and launched the game. The
  double-click handler now ignores clicks while the display-settings popup is open.

---

## [1.0.0] — 2026-05-24

> **Note:** released as *SteamSwitcher* v1.0.0. Rebranded to *EnigmaLauncher* starting v2.0.0.

### Added
- Unified game library grid showing installed games across all Steam accounts
- Automatic account detection via `HKCU\SOFTWARE\Valve\Steam\ActiveProcess\ActiveUser`
- Silent account switching: registry + `loginusers.vdf` + `config.vdf` patching, double-start Steam pattern
- `Timestamp` field patching in `loginusers.vdf` to resolve tie-break ordering between accounts
- Rich game artwork loaded from Steam's local `appcache/librarycache` cache with CDN fallback
- Per-account colour-coded badges (deterministic colour from account name hash)
- Account filter bar to view games owned by a specific account
- Hover overlay on game cards with **Play** and **Create Shortcut** actions
- Desktop `.lnk` shortcut creation with extracted game icon (ICO via `System.Drawing`)
- `--launch <appid>` CLI mode used by shortcuts: checks current account, switches if needed, launches game
- `LaunchWindow` progress dialog with live status updates and retry on error
- Account switcher dropdown in the main window header — switch accounts without launching a game
- `SteamOperations` factory: shared `LaunchGame` and `SwitchAccount` async operations consumed by both GUI and CLI flows
- Dark Steam-themed WPF UI (`#171A21` background, `#1B2838` header, Steam blue accents)
- `build.bat` one-click publish script (folder mode, no single-file bundler — avoids AV false positives)
