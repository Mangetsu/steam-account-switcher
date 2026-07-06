param(
    [Parameter(Mandatory = $true)]
    [string]$StageRoot,

    [Parameter(Mandatory = $true)]
    [string]$InstallRoot
)

$ErrorActionPreference = 'Stop'

function Resolve-FullPath([string]$Path) {
    return [IO.Path]::GetFullPath($Path)
}

function Move-DirectoryContents([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source)) {
        return
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Move-Item -LiteralPath $_.FullName -Destination $Destination -Force
    }

    Remove-Item -LiteralPath $Source -Force
}

function Patch-AppHost([string]$ExePath) {
    # The apphost exe has the managed DLL path baked in as a UTF-8 string.
    # We shorten it to 'app\EL.dll' so the runtime knows where to find the
    # managed assembly after we move it out of the publish root.
    $oldName = 'EnigmaLauncher.dll'
    $newName = 'app\EL.dll'

    $oldBytes = [Text.Encoding]::UTF8.GetBytes($oldName)
    $newBytes = [Text.Encoding]::UTF8.GetBytes($newName)

    if ($newBytes.Length -gt $oldBytes.Length) {
        throw "Replacement apphost path '$newName' is longer than '$oldName'."
    }

    $bytes = [IO.File]::ReadAllBytes($ExePath)

    for ($i = 0; $i -le $bytes.Length - $oldBytes.Length; $i++) {
        $found = $true
        for ($j = 0; $j -lt $oldBytes.Length; $j++) {
            if ($bytes[$i + $j] -ne $oldBytes[$j]) {
                $found = $false
                break
            }
        }

        if ($found) {
            for ($j = 0; $j -lt $oldBytes.Length; $j++) {
                $bytes[$i + $j] = 0
            }

            for ($j = 0; $j -lt $newBytes.Length; $j++) {
                $bytes[$i + $j] = $newBytes[$j]
            }

            [IO.File]::WriteAllBytes($ExePath, $bytes)
            return
        }
    }

    throw "Could not find '$oldName' in apphost '$ExePath'."
}

$stageRoot   = Resolve-FullPath $StageRoot
$installRoot = Resolve-FullPath $InstallRoot
$appDir      = Join-Path $stageRoot 'app'
$dataDir     = Join-Path $installRoot 'data'

if (-not (Test-Path -LiteralPath $appDir)) {
    throw "Missing publish app directory: $appDir"
}

# Promote the host exe to the layout root and patch it to find app\EL.dll
$rootExe = Join-Path $stageRoot 'EnigmaLauncher.exe'
$appExe  = Join-Path $appDir    'EnigmaLauncher.exe'
Copy-Item -LiteralPath $appExe -Destination $rootExe -Force
Remove-Item -LiteralPath $appExe -Force

# Rename managed assembly and config files to their short aliases
Rename-Item -LiteralPath (Join-Path $appDir 'EnigmaLauncher.dll')              -NewName 'EL.dll'
Rename-Item -LiteralPath (Join-Path $appDir 'EnigmaLauncher.deps.json')        -NewName 'EL.deps.json'
Rename-Item -LiteralPath (Join-Path $appDir 'EnigmaLauncher.runtimeconfig.json') -NewName 'EL.runtimeconfig.json'
if (Test-Path -LiteralPath (Join-Path $appDir 'EnigmaLauncher.pdb')) {
    Rename-Item -LiteralPath (Join-Path $appDir 'EnigmaLauncher.pdb') -NewName 'EL.pdb'
}

Patch-AppHost $rootExe

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
New-Item -ItemType Directory -Force -Path $dataDir     | Out-Null

# Preserve any existing data that might have been written at the old flat layout
# (pre-data/ era) or by a previous build.
Move-DirectoryContents (Join-Path $installRoot 'cache')  (Join-Path $dataDir 'cache')
Move-DirectoryContents (Join-Path $installRoot 'icons')  (Join-Path $dataDir 'icons')

# Wipe everything except data/, then copy the fresh stage in
Get-ChildItem -LiteralPath $installRoot -Force |
    Where-Object Name -ne 'data' |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }

Copy-Item -Path (Join-Path $stageRoot '*') -Destination $installRoot -Recurse -Force
