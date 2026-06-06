@echo off
set ROOT=%~dp0
cd /d "%ROOT%"

echo === EnigmaLauncher Build ===
echo.

set DEST=%LOCALAPPDATA%\EnigmaLauncher
set STAGE=%ROOT%.build\EnigmaLauncher

echo Stopping running EnigmaLauncher processes...
powershell -NoProfile -NonInteractive -Command ^
  "Get-Process EnigmaLauncher -ErrorAction SilentlyContinue | Stop-Process -Force"

if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%\app"

REM Publish self-contained to a staged app folder (no single-file bundling).
REM Single-file + IncludeNativeLibrariesForSelfExtract triggers AV false-positives
REM because the exe self-extracts DLLs into %%TEMP%% at startup.
REM Folder mode produces a plain host exe that AV engines do not flag.
dotnet publish EnigmaLauncher\EnigmaLauncher.csproj ^
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
echo Installed to: %DEST%\EnigmaLauncher.exe

REM Desktop shortcut
echo.
echo Creating desktop shortcut...
powershell -NoProfile -NonInteractive -Command ^
  "$dest = '%DEST%';" ^
  "$ws = New-Object -ComObject WScript.Shell;" ^
  "$s = $ws.CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'EnigmaLauncher.lnk'));" ^
  "$s.TargetPath = $dest + '\EnigmaLauncher.exe';" ^
  "$s.WorkingDirectory = $dest;" ^
  "$s.IconLocation = $dest + '\EnigmaLauncher.exe,0';" ^
  "$s.Save()"

if errorlevel 1 (
    echo WARNING: Could not create desktop shortcut.
) else (
    echo Desktop shortcut created: %USERPROFILE%\Desktop\EnigmaLauncher.lnk
)

echo.
echo Launching EnigmaLauncher...
start "" "%DEST%\EnigmaLauncher.exe"

echo.
echo NOTE: If Norton quarantines the exe, add an exclusion for:
echo   %DEST%
echo.
echo To use:
echo   - Run EnigmaLauncher.exe to open the game library GUI
echo   - Click "Create Shortcut" on any game to add a desktop shortcut
echo   - Double-click a shortcut to auto-switch accounts and launch the game
echo.
pause
