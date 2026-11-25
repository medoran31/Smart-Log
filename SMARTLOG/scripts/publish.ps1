# Publishes SMARTLOG as a self-contained Windows x64 application.
# No .NET runtime is required on the target computer.
[CmdletBinding()]
param(
    [string]$SolutionDir = (Split-Path -Parent $PSScriptRoot),
    [string]$Profile = 'win-x64-selfcontained'
)

$ErrorActionPreference = 'Stop'
$appDir = Join-Path $SolutionDir 'src\SMARTLOG.App'
$publishDir = Join-Path $appDir 'bin\Release\net8.0-windows\win-x64\publish'

Write-Host "Publishing SMARTLOG (self-contained win-x64)..."
dotnet publish (Join-Path $appDir 'SMARTLOG.App.csproj') `
    -c Release -p:PublishProfile=$Profile --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (code $LASTEXITCODE)." }

$exe = Join-Path $publishDir 'SMARTLOG.App.exe'
if (-not (Test-Path $exe)) { throw "Executable not found after publish: $exe" }
Write-Host "Published: $exe"
Write-Host "Run scripts\install.ps1 to install it on this computer."
