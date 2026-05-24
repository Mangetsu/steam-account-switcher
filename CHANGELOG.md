# Changelog

All notable changes to SteamSwitcher will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions follow [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

### Added
- About dialog with version, runtime, install path, license, and GitHub link.
- Custom shortcut action that prompts for a destination folder before creating the `.lnk`.
- `CLAUDE.md` as the single source of truth for AI agents, with Codex and GitHub Copilot redirect files.

### Changed
- License changed from MIT to PolyForm Noncommercial 1.0.0; non-commercial use remains free, commercial use requires a separate paid license.
- Game library deduplication now keeps distinct cards for the same AppID when ownership differs by Steam account.
- Game scanning now uses per-user Steam ticket data to detect additional remembered accounts for an installed AppID when the manifest only reports one `LastOwner`, avoiding cloud-only false positives.
- Game launch and generated shortcuts now preserve the selected owner with `--owner <steamid64>`.
- Multi-owner game shortcuts now use the account display name in the `.lnk` filename instead of the SteamID64.
- Build output now keeps the install root tidy: `SteamSwitcher.exe` at root, runtime files under `app\`, and app data under `data\`.
- Game-card action buttons now have icon-leading labels and distinct colors.
- `build.bat` now stops any running SteamSwitcher process before publishing and launches the app after a successful build.
- The custom shortcut folder picker now opens on the Desktop by default.

---

## [1.0.0] — 2026-05-24

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
