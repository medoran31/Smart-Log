# Installs the published SMARTLOG folder under %LOCALAPPDATA%\Programs\SmartLog
# and creates Start Menu shortcuts. No administrator rights are needed.
# User data (%LOCALAPPDATA%\Smartlog) is never touched by this script.
[CmdletBinding()]
param(
    [string]$SourceDir,
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\SmartLog",
    [switch]$DesktopShortcut
)

$ErrorActionPreference = 'Stop'
if (-not $SourceDir) {
    $SolutionDir = Split-Path -Parent $PSScriptRoot
    $SourceDir = Join-Path $SolutionDir 'src\SMARTLOG.App\bin\Release\net8.0-windows\win-x64\publish'
}

$exeSource = Join-Path $SourceDir 'SMARTLOG.App.exe'
if (-not (Test-Path $exeSource)) {
    throw "Published application not found: $exeSource. Run scripts\publish.ps1 first."
}

if (-not (Test-Path $InstallDir)) { New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null }

# Do not overwrite a running executable.
$running = Get-Process -Name 'SMARTLOG.App' -ErrorAction SilentlyContinue
if ($running) { throw "SMARTLOG is running. Close it before installing." }

Copy-Item -Path (Join-Path $SourceDir '*') -Destination $InstallDir -Recurse -Force
$exe = Join-Path $InstallDir 'SMARTLOG.App.exe'

$menuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
if (-not (Test-Path $menuDir)) { New-Item -ItemType Directory -Path $menuDir -Force | Out-Null }
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $menuDir 'SMARTLOG.lnk'))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $InstallDir
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = 'SMARTLOG - suivi de chantier'
$shortcut.Save()

if ($DesktopShortcut) {
    $desktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'SMARTLOG.lnk'
    $shortcut = $shell.CreateShortcut($desktop)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Description = 'SMARTLOG - suivi de chantier'
    $shortcut.Save()
}

Write-Host "SMARTLOG installed in: $InstallDir"
Write-Host "Start Menu shortcut created. User data stays in: $env:LOCALAPPDATA\Smartlog"
