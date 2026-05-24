@echo off
set ROOT=%~dp0
cd /d "%ROOT%"

echo === SteamSwitcher Build ===
echo.

set DEST=%LOCALAPPDATA%\SteamSwitcher
set STAGE=%ROOT%.build\SteamSwitcher

echo Stopping running SteamSwitcher processes...
powershell -NoProfile -NonInteractive -Command ^
  "Get-Process SteamSwitcher -ErrorAction SilentlyContinue | Stop-Process -Force"

if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%\app"

REM Publish self-contained to a staged app folder (no single-file bundling).
REM Single-file + IncludeNativeLibrariesForSelfExtract triggers AV false-positives
REM because the exe self-extracts DLLs into %%TEMP%% at startup.
REM Folder mode produces a plain host exe that AV engines do not flag.
dotnet publish SteamSwitcher\SteamSwitcher.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o "%STAGE%\app"

if errorlevel 1 (
    echo.
    echo BUILD FAILED.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\Publish-CleanLayout.ps1" ^
  -StageRoot "%STAGE%" ^
  -InstallRoot "%DEST%"

if errorlevel 1 (
    echo.
    echo INSTALL LAYOUT FAILED.
    pause
    exit /b 1
)

echo.
echo BUILD COMPLETE.
echo Installed to: %DEST%\SteamSwitcher.exe

REM Desktop shortcut
echo.
echo Creating desktop shortcut...
powershell -NoProfile -NonInteractive -Command ^
  "$dest = '%DEST%';" ^
  "$ws = New-Object -ComObject WScript.Shell;" ^
  "$s = $ws.CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'SteamSwitcher.lnk'));" ^
  "$s.TargetPath = $dest + '\SteamSwitcher.exe';" ^
  "$s.WorkingDirectory = $dest;" ^
  "$s.IconLocation = $dest + '\SteamSwitcher.exe,0';" ^
  "$s.Save()"

if errorlevel 1 (
    echo WARNING: Could not create desktop shortcut.
) else (
    echo Desktop shortcut created: %USERPROFILE%\Desktop\SteamSwitcher.lnk
)

echo.
echo Launching SteamSwitcher...
start "" "%DEST%\SteamSwitcher.exe"

echo.
echo NOTE: If Norton quarantines the exe, add an exclusion for:
echo   %DEST%
echo.
echo To use:
echo   - Run SteamSwitcher.exe to open the game library GUI
echo   - Click "Create Shortcut" on any game to add a desktop shortcut
echo   - Double-click a shortcut to auto-switch accounts and launch the game
echo.
pause
