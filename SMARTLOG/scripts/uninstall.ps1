# Removes the installed application and its shortcuts.
# User data (%LOCALAPPDATA%\Smartlog: database, documents, backups, AI config)
# is kept by default. Add -RemoveUserData to delete it explicitly.
[CmdletBinding()]
param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\SmartLog",
    [switch]$RemoveUserData
)

$ErrorActionPreference = 'Stop'
$running = Get-Process -Name 'SMARTLOG.App' -ErrorAction SilentlyContinue
if ($running) { throw "SMARTLOG is running. Close it before uninstalling." }

$menuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcut = Join-Path $menuDir 'SMARTLOG.lnk'
if (Test-Path $shortcut) { Remove-Item $shortcut -Force }
$desktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'SMARTLOG.lnk'
if (Test-Path $desktop) { Remove-Item $desktop -Force }

if (Test-Path $InstallDir) {
    Remove-Item $InstallDir -Recurse -Force
    Write-Host "Removed: $InstallDir"
}
else {
    Write-Host "No installation found in: $InstallDir"
}

$dataDir = Join-Path $env:LOCALAPPDATA 'Smartlog'
if ($RemoveUserData -and (Test-Path $dataDir)) {
    Remove-Item $dataDir -Recurse -Force
    Write-Host "Removed user data: $dataDir"
}
else {
    Write-Host "User data kept in: $dataDir (back it up before deleting it)."
}
