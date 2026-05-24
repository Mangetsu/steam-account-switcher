# Building SteamSwitcher

## Prerequisites

| Tool | Version | Download |
|---|---|---|
| Windows | 10 or 11, 64-bit | https://www.microsoft.com/windows |
| .NET SDK | 8.0 or later | https://dotnet.microsoft.com/download/dotnet/8 |
| Git | any recent version | https://git-scm.com |

Visual Studio 2022 with the .NET desktop development workload is recommended for IDE work, but it
is not required for command-line builds.

## Quick Build

```cmd
git clone https://github.com/Mangetsu/steam-account-switcher.git
cd SteamSwitcher
build.bat
```

`build.bat` stops any running SteamSwitcher process, publishes a self-contained install to
`%LOCALAPPDATA%\SteamSwitcher\`, creates or updates the Desktop launcher shortcut, and launches the
freshly built app when it finishes.

Expected install layout:

```text
%LOCALAPPDATA%\SteamSwitcher\
  SteamSwitcher.exe
  app\
  data\
```

## Clean Layout Publish

Do not publish directly to the install folder if you want the clean layout. Use `build.bat`.

The batch file:

1. Stops any running `SteamSwitcher` process so install files are not locked.
2. Publishes the normal self-contained output to `.build\SteamSwitcher\app\`.
3. Copies the apphost to the install root as `SteamSwitcher.exe`.
4. Patches the apphost to load `app\SS.dll` by relative path.
5. Migrates old root `cache\` and `icons\` folders into `data\cache\` and `data\icons\`.
6. Replaces the old runtime files while preserving `data\`.
7. Launches `%LOCALAPPDATA%\SteamSwitcher\SteamSwitcher.exe`.

## Why Folder Mode?

Single-file publish with native extraction can make WPF unpack DLLs into `%TEMP%` at runtime. That
pattern can be flagged by reputation-based antivirus products. Folder mode keeps files on disk in a
normal application layout and avoids that self-extraction behavior.

## Running In Visual Studio

1. Open `SteamSwitcher.sln`.
2. Set `SteamSwitcher` as the startup project.
3. Press F5.

To test shortcut launch mode, set debug arguments to:

```text
--launch <appid>
```

## Verification

For code, XAML, or build-script changes:

```cmd
dotnet build SteamSwitcher.sln -c Release
```

For publish layout changes:

```cmd
cmd /c path\to\SteamSwitcher\build.bat
```

Before committing:

```cmd
git diff --check
```

## Project Layout

```text
SteamSwitcher/
├── SteamSwitcher/
│   ├── Steam/                   # Steam registry, VDF, library, switching logic
│   ├── UI/
│   │   ├── Controls/            # GameCard, AccountBadge
│   │   ├── Styles/              # Theme.xaml
│   │   ├── AboutWindow.xaml     # About dialog
│   │   ├── LaunchWindow.xaml    # Progress dialog
│   │   └── MainWindow.xaml      # Main library window
│   ├── Shortcuts/               # .lnk creation and ICO generation
│   ├── Assets/                  # app_icon.ico
│   ├── AppPaths.cs              # Local app data paths
│   └── SteamSwitcher.csproj
├── docs/
│   ├── architecture.md
│   └── building.md
├── scripts/
│   └── Publish-CleanLayout.ps1
├── AGENTS.md                    # Redirects agents to CLAUDE.md
├── CLAUDE.md                    # Agent source of truth
├── CHANGELOG.md
├── README.md
├── build.bat
└── SteamSwitcher.sln
```

## Dependencies

| Package | Version | Used for |
|---|---|---|
| ValveKeyValue | 0.10.x | VDF file parsing |
| System.Drawing.Common | 8.0.x | ICO generation for shortcuts |

All other functionality uses built-in .NET 8 and Windows APIs, including WPF,
`Microsoft.Win32.Registry`, `Microsoft.Win32.OpenFolderDialog`, `System.Net.Http`, and
`System.Diagnostics.Process`.
