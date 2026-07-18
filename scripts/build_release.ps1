param(
    [string]$Version = '1.2.1'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $repo 'publish'
$release = Join-Path $repo 'release'

$running = Get-Process -Name 'RazerLightingSwitch','RazorLightweightKeyboardLightingControl' -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
}

& dotnet publish (Join-Path $repo 'RazerLightingSwitch.csproj') -c Release -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

New-Item -ItemType Directory -Force -Path $release | Out-Null
$source = Join-Path $publish 'RazorLightweightKeyboardLightingControl.exe'
$asset = Join-Path $release "Razor-Lightweight-Keyboard-Lighting-Control-v$Version-win-x64.exe"
Copy-Item -LiteralPath $source -Destination $asset -Force

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'install.ps1') -PublishDirectory $publish
if ($LASTEXITCODE -ne 0) { throw "install failed with exit code $LASTEXITCODE" }

$hash = Get-FileHash -LiteralPath $asset -Algorithm SHA256
[pscustomobject]@{
    Version = $Version
    Asset = $asset
    Size = (Get-Item -LiteralPath $asset).Length
    SHA256 = $hash.Hash
} | ConvertTo-Json -Compress
