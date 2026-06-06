# EnigmaLauncher Agent Guidelines

This file is the source of truth for AI agents working in this repository. Claude, Codex,
GitHub Copilot, and any other coding agent must read and follow this file before making changes.
Do not duplicate these instructions into other agent-specific files; those files should only point
back here.

## Project Overview

EnigmaLauncher is a Windows WPF app targeting .NET 8. It scans local Steam libraries, maps games
to Steam accounts, silently switches Steam accounts when possible, launches games through desktop
or user-chosen `.lnk` shortcuts, and routes games to the correct monitor on multi-display setups.

Key project areas:

- `EnigmaLauncher/Steam/`: Steam registry, VDF, library scanning, account switching, artwork logic (internal).
- `EnigmaLauncher/Stores/`: Store abstraction (`IGameStore`, `IAccountStore`, `GameInfo`, `AccountInfo`, `StoreRegistry`).
- `EnigmaLauncher/Stores/Steam/SteamStore.cs`: `IAccountStore` adapter for Steam.
- `EnigmaLauncher/Settings/`: `SettingsStore` — reads/writes `data\settings.json`.
- `EnigmaLauncher/Display/`: `DisplayManager` and `MonitorInfo` — multi-monitor support.
- `EnigmaLauncher/Migration/`: `MigrationService` — one-time upgrade from SteamSwitcher v1.0.0.
- `EnigmaLauncher/UI/`: WPF windows, cards, account switcher, About dialog, and theme resources.
- `EnigmaLauncher/Shortcuts/`: Windows `.lnk` creation and ICO generation.
- `scripts/Publish-CleanLayout.ps1`: post-publish layout script used by `build.bat`.
- `docs/`: human-readable architecture and build notes.

## Non-Negotiable Behavior

- Keep the app universal. Do not hardcode user-specific paths such as `C:\Users\...`.
- Use `%LOCALAPPDATA%` in scripts/docs and `Environment.SpecialFolder.LocalApplicationData` in code.
- Preserve the clean install layout:
  - root: `EnigmaLauncher.exe`
  - runtime files: `app\`
  - runtime data: `data\cache\`, `data\icons\`, `data\settings.json`
- Keep publishing in folder mode. Do not switch back to single-file publish; it can trigger AV
  false positives because WPF native files self-extract at runtime.
- Game shortcuts must target the root `EnigmaLauncher.exe` with `--launch <appid>`.
- Account-specific game shortcuts must include `--owner <steamid64>` when the selected card has an owner.
- Multi-owner game shortcut filenames should use the account display name as the suffix, not the SteamID64,
  while keeping `--owner <steamid64>` in the shortcut arguments.
- Library deduplication must preserve distinct `(AppId, LastOwnerSteamId64)` pairs so duplicate games
  owned by different accounts remain selectable.
- When a manifest only reports one `LastOwner`, scan remembered users' `userdata\<steamid3>\config\localconfig.vdf`
  `apptickets` and `nettickets` sections for the same AppID and synthesize additional owner-specific entries.
  Do not use cloud-only app stubs as ownership signals.
- Shortcut icons should continue to be stored under `data\icons\`.
- Downloaded artwork should continue to be stored under `data\cache\`.
- Do not move Steam switching logic into UI classes; keep `SteamStore` as the bridge between the UI
  and Steam behavior.
- The UI must bind to `IAccountStore` / `GameInfo` / `AccountInfo` only — no Steam-specific types
  in the presentation layer.

## Build And Verification

Use this before committing changes that affect code, XAML, build scripts, or build docs:

```cmd
dotnet build EnigmaLauncher.sln -c Release
```

For publish/install layout changes, also run:

```cmd
build.bat
```

`build.bat` is expected to stop any running EnigmaLauncher process before replacing install files
and launch the freshly built app after a successful build.

When validating `build.bat`, prefer running it from outside the repo at least once:

```cmd
cmd /c path\to\EnigmaLauncher\build.bat
```

Expected installed layout:

```text
%LOCALAPPDATA%\EnigmaLauncher\
  EnigmaLauncher.exe
  app\
  data\
```

Before committing, run:

```cmd
git diff --check
```

## Documentation Rules

Every behavior change must update relevant docs in the same commit.

Update these files when applicable:

- `README.md`: user-facing features, installation, usage, shortcut behavior.
- `CHANGELOG.md`: all notable unreleased changes.
- `docs/architecture.md`: runtime behavior, data paths, shortcut flow, key classes.
- `docs/building.md`: build, publish, install layout, dependencies.
- `CLAUDE.md`: agent rules, source-of-truth decisions, required workflows.

If docs and code disagree, treat this file as the agent-process source of truth and the code as the
runtime source of truth. Bring README/docs/changelog back into agreement before committing.

## Coding Guidelines

- Follow the existing C# and WPF style; keep changes focused.
- Prefer existing helper classes and local patterns over new abstractions.
- Use WPF-native APIs for UI flows when available.
- Keep card/button labels short enough to fit in the fixed 200 x 320 game card.
- Avoid adding package dependencies unless the standard library or WPF does not provide a practical
  option.

## Current UI Expectations

Main window header:

- `About` button sits next to `Refresh`.
- Account badge opens the account switcher popup.
- Display badge (🖥) opens the display switcher popup (set Windows primary monitor).

Game card hover actions:

- `Play`: launches the game, switching account if needed, routing to preferred display.
- `Create Desktop Shortcut`: creates a `.lnk` on the Desktop.
- `Create a shortcut`: prompts for a destination folder before creating the `.lnk`.
- `Display` (🖥): opens the per-game display settings popup (target monitor + switch method).

About dialog:

- Shows app version, .NET runtime, install path, license, and GitHub link.

## Git Guidelines

- Do not rewrite history unless the user explicitly asks.
- Do not revert user changes unless explicitly asked.
- Keep commits focused and include docs with behavior changes.
- Push to both remotes when the user asks for a push: `origin` and `github`.
