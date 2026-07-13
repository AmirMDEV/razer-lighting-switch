param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $repo 'publish'

Get-Process -Name 'RazerLightingSwitch' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

& dotnet publish (Join-Path $repo 'RazerLightingSwitch.csproj') -c $Configuration -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'install.ps1') -PublishDirectory $publish
if ($LASTEXITCODE -ne 0) { throw "install failed with exit code $LASTEXITCODE" }

Write-Output 'Build install complete'
