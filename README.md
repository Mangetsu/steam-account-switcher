<div align="center">

# SteamSwitcher

**Instantly switch Steam accounts and launch games with less friction.**

[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8)
[![License: PolyForm NC](https://img.shields.io/badge/license-PolyForm%20NC%201.0-blue)](LICENSE)
[![Steam](https://img.shields.io/badge/Steam-compatible-1b2838?logo=steam&logoColor=white)](https://store.steampowered.com)

![SteamSwitcher preview](docs/preview.png)

</div>

---

## What is this?

SteamSwitcher is a Windows app for PCs with multiple remembered Steam accounts. It scans installed
Steam games, detects which account owns each game, silently switches Steam to the right account when
possible, and launches the game.

## Features

- Unified game grid showing installed games across all Steam accounts
- Silent account switching using Steam registry and VDF state
- Game artwork from Steam's local cache with CDN fallback
- Account filter bar and header account switcher
- Per-game shortcuts with generated `.ico` files
- One-click **Create Desktop Shortcut** action
- **Create a shortcut** action that prompts for the destination folder
- About screen with version/runtime details and GitHub link
- Clean self-contained install layout under `%LOCALAPPDATA%\SteamSwitcher`

## Requirements

- Windows 10 or 11, 64-bit
- Steam installed
- Each account must have been signed into at least once with **Remember me** checked

## Installation

### Build from source

```cmd
git clone https://github.com/Mangetsu/steam-account-switcher.git
cd SteamSwitcher
build.bat
```

The app is published to `%LOCALAPPDATA%\SteamSwitcher\SteamSwitcher.exe`.
If SteamSwitcher is already running, `build.bat` stops it before replacing files, then launches the
freshly built app when the build finishes.

The install root stays tidy:

```text
%LOCALAPPDATA%\SteamSwitcher\
  SteamSwitcher.exe
  app\
  data\
```

### Download release

Download the latest zip from the [Releases](../../releases) page, extract it to
`%LOCALAPPDATA%\SteamSwitcher\`, and run `SteamSwitcher.exe`.

## Usage

Run `SteamSwitcher.exe` with no arguments to open the game library GUI.

If the same game is installed for multiple remembered Steam accounts, it appears once per owning
account so you can choose which account should launch it. SteamSwitcher uses both manifest
`LastOwner` data and per-user Steam ticket data to detect those account-specific entries.

Game-card hover actions:

- **Play**: launch the game, switching account if needed.
- **Create Desktop Shortcut**: create a `.lnk` on the Desktop.
- **Create a shortcut**: choose the folder where the `.lnk` should be created. The picker opens on
  the Desktop by default.

Header actions:

- **Account badge**: switch accounts without launching a game.
- **About**: view app metadata and GitHub link.
- **Refresh**: reload accounts and installed games.

Shortcut target format:

```text
Target:   %LOCALAPPDATA%\SteamSwitcher\SteamSwitcher.exe --launch <appid> --owner <steamid64>
Icon:     %LOCALAPPDATA%\SteamSwitcher\data\icons\<appid>.ico
```

When the same game has multiple account cards, generated shortcut filenames include the account
display name, for example `Yu-Gi-Oh! Duel Links (TheMangetsu).lnk`.

Double-clicking a shortcut runs the switch-and-launch flow.

## Documentation

- [Architecture](docs/architecture.md)
- [Building](docs/building.md)
- [Changelog](CHANGELOG.md)
- [Agent guidelines](CLAUDE.md)

`CLAUDE.md` is the single source of truth for AI agents. Claude, Codex, GitHub Copilot, and other
agents should follow it before changing this repository.

## Project Structure

```text
SteamSwitcher/
├── SteamSwitcher/          # C# WPF project
│   ├── Steam/              # Steam integration
│   ├── UI/                 # WPF windows, controls, theme
│   ├── Shortcuts/          # .lnk creation via WScript.Shell COM
│   ├── Assets/             # app icon assets
│   ├── AppPaths.cs         # Local app data paths
│   └── SteamSwitcher.csproj
├── docs/
│   ├── architecture.md
│   └── building.md
├── scripts/
│   └── Publish-CleanLayout.ps1
├── AGENTS.md               # Redirects agents to CLAUDE.md
├── CLAUDE.md               # Agent source of truth
├── CHANGELOG.md
├── README.md
├── build.bat
└── SteamSwitcher.sln
```

## Contributing

- Open an issue first for significant changes.
- Keep PRs focused.
- Update relevant docs in the same change.
- Follow [`CLAUDE.md`](CLAUDE.md) when using AI coding agents.

## License

This project is licensed under the **[PolyForm Noncommercial License 1.0.0](LICENSE)**.

- **Non-commercial use** (personal projects, research, education, hobbyist, non-profit) is free.
- **Commercial use** requires a separate paid license.

To purchase a commercial license, contact: badr.hakkari@gmail.com

© 2026 Badr Hakkari
