# Building EnigmaLauncher

## Prerequisites

| Tool | Version | Download |
|---|---|---|
| Windows | 10 or 11 (64-bit) | — |
| .NET SDK | 8.0 or later | https://dotnet.microsoft.com/download/dotnet/8 |
| Git | any | https://git-scm.com |

Visual Studio 2022 (with the **.NET desktop development** workload) is recommended for IDE work
but is not required for building.

---

## Quick build

```cmd
git clone https://github.com/Mangetsu/steam-account-switcher.git
cd steam-account-switcher
build.bat
```

`build.bat` publishes a self-contained folder to `%LOCALAPPDATA%\EnigmaLauncher\` and prints
the install path when done.

---

## Manual publish

```cmd
dotnet publish EnigmaLauncher\EnigmaLauncher.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o "%TEMP%\enigma-stage\app"
```

Then run `scripts\Publish-CleanLayout.ps1` to create the final install layout:

```powershell
.\scripts\Publish-CleanLayout.ps1 `
  -StageRoot  "$env:TEMP\enigma-stage" `
  -InstallRoot "$env:LOCALAPPDATA\EnigmaLauncher"
```

### Install layout

```
%LOCALAPPDATA%\EnigmaLauncher\
  EnigmaLauncher.exe          ← host exe (patched to load app\EL.dll)
  app\
    EL.dll                    ← managed assembly (renamed from EnigmaLauncher.dll)
    EL.deps.json
    EL.runtimeconfig.json
    (WPF native DLLs, …)
  data\
    cache\                    ← downloaded artwork
    icons\                    ← generated .ico files for shortcuts
    settings.json             ← user preferences (display routing, etc.)
```

### Why folder mode (not single-file)?

Single-file publish with `IncludeNativeLibrariesForSelfExtract=true` makes the exe unpack WPF's
native DLLs into `%TEMP%` at runtime. That self-extraction pattern is indistinguishable from
dropper malware to reputation-based AV engines (Norton `Win64:Malware-gen`, etc.), causing the
file to be quarantined on first run.

Folder-mode publish produces a standard Windows host exe that loads DLLs from its own directory —
no extraction, no AV alerts.

---

## Antivirus exclusion

Because the binary is unsigned (no paid code-signing certificate), reputation-based AV engines
may alert on the first run. Add the install folder to your AV exclusion list:

- **Norton:** Settings → Antivirus → Scans and Risks → Exclusions → Add Folder
  → `%LOCALAPPDATA%\EnigmaLauncher`
- **Windows Defender:** Windows Security → Virus & threat protection → Manage settings
  → Exclusions → Add an exclusion → Folder → `%LOCALAPPDATA%\EnigmaLauncher`

---

## Running in Visual Studio

1. Open `EnigmaLauncher.sln`
2. Set **EnigmaLauncher** as the startup project (it already is)
3. Press **F5** — the app reads your real Steam install from the registry

To test the `--launch` mode, set the project debug arguments to `--launch <appid>` in
Project Properties → Debug → Application arguments.

---

## Project layout

```
EnigmaLauncher/
├── EnigmaLauncher/                  # C# WPF project
│   ├── Steam/                       # Steam integration layer (internal)
│   │   ├── SteamConfig.cs           # Registry reader, path resolver
│   │   ├── SteamAccount.cs          # Steam account model
│   │   ├── AccountManager.cs        # loginusers.vdf parser, current-account detection
│   │   ├── GameEntry.cs             # Steam game model
│   │   ├── LibraryScanner.cs        # libraryfolders.vdf + .acf parser
│   │   ├── AccountSwitcher.cs       # Core switching logic
│   │   └── ArtworkResolver.cs       # Local cache + CDN artwork
│   ├── Stores/                      # Store abstraction layer
│   │   ├── IGameStore.cs            # Base interface: scan, artwork, launch
│   │   ├── IAccountStore.cs         # Extension: accounts, switch operations
│   │   ├── GameInfo.cs              # Store-agnostic game model
│   │   ├── AccountInfo.cs           # Store-agnostic account model
│   │   ├── StoreRegistry.cs         # Discovers and holds registered stores
│   │   └── Steam/
│   │       └── SteamStore.cs        # Account + client-action adapter for Steam
│   ├── Settings/
│   │   └── SettingsStore.cs         # data\settings.json reader/writer
│   ├── Display/
│   │   ├── MonitorInfo.cs           # Monitor model (device name, bounds, DPI)
│   │   └── DisplayManager.cs        # Enumerate monitors, set primary display
│   ├── Migration/
│   │   └── MigrationService.cs      # One-time upgrade from SteamSwitcher v1.0.0
│   ├── UI/
│   │   ├── Controls/
│   │   │   ├── GameCard.xaml        # 200×320 portrait game card
│   │   │   └── AccountBadge.xaml    # Coloured account pill
│   │   ├── Styles/
│   │   │   └── Theme.xaml           # Dark Steam-themed ResourceDictionary
│   │   ├── MainWindow.xaml          # Main library window
│   │   ├── LaunchWindow.xaml        # Floating progress dialog
│   │   └── AboutWindow.xaml         # App info dialog
│   ├── Shortcuts/
│   │   └── ShortcutCreator.cs       # .lnk creation via WScript.Shell COM
│   ├── Assets/
│   │   └── app_icon.ico
│   ├── AppPaths.cs                  # Centralised path constants
│   ├── App.xaml                     # Entry point, --launch vs GUI routing
│   └── EnigmaLauncher.csproj
├── docs/
│   ├── architecture.md              # Technical deep-dive
│   └── building.md                  # This file
├── scripts/
│   └── Publish-CleanLayout.ps1      # Post-publish install layout script
├── .editorconfig
├── .gitignore
├── build.bat
├── CHANGELOG.md
├── LICENSE
├── README.md
└── EnigmaLauncher.sln
```

---

## Dependencies

| Package | Version | Used for |
|---|---|---|
| [ValveKeyValue](https://github.com/nicklvsa/ValveKeyValue) | 0.10.x | VDF file parsing |
| [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) | 8.0.x | ICO generation for shortcuts |

All other functionality uses built-in .NET 8 APIs (WPF, `Microsoft.Win32.Registry`,
`System.Net.Http`, `System.Diagnostics.Process`, `System.Windows.Forms.Screen`).
