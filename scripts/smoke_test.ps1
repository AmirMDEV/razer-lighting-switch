param(
    [string]$PublishDirectory = "$PSScriptRoot\..\publish"
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $PublishDirectory 'RazerLightingSwitch.exe'
$log = Join-Path $env:LOCALAPPDATA 'Amir\RazerLightingSwitch\controller.log'
$desktop = [Environment]::GetFolderPath('Desktop')
$blackShortcut = Join-Path $desktop 'Keyboard Black.lnk'
$whiteShortcut = Join-Path $desktop 'Keyboard White.lnk'

Get-Process -Name 'RazerLightingSwitch' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue

$hostStart = [Diagnostics.Stopwatch]::StartNew()
Start-Process -FilePath $blackShortcut
while (-not (Test-Path -LiteralPath $log)) {
    if ($hostStart.Elapsed.TotalSeconds -gt 5) { throw 'Controller did not create its log within 5 seconds' }
    Start-Sleep -Milliseconds 50
}
while ((Get-Content -LiteralPath $log -Raw) -notmatch 'Applied black') {
    if ($hostStart.Elapsed.TotalSeconds -gt 5) { throw 'Black state did not apply within 5 seconds' }
    Start-Sleep -Milliseconds 50
}
$hostStart.Stop()
$hostProcess = Get-Process -Name 'RazerLightingSwitch' | Select-Object -First 1

$switchTimes = @{}
foreach ($state in @('white', 'black', 'white')) {
    $before = (Get-Content -LiteralPath $log).Count
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $shortcutPath = if ($state -eq 'black') { $blackShortcut } else { $whiteShortcut }
    Start-Process -FilePath $shortcutPath
    while ((Get-Content -LiteralPath $log).Count -le ($before + 1)) {
        if ($timer.Elapsed.TotalSeconds -gt 3) { throw "$state command was not applied within 3 seconds" }
        Start-Sleep -Milliseconds 20
    }
    $timer.Stop()
    $switchTimes[$state] = [math]::Round($timer.Elapsed.TotalMilliseconds)
}

Start-Sleep -Seconds 6
if ((Get-Content -LiteralPath $log -Raw) -match 'Heartbeat failed') { throw 'Heartbeat failed' }
if (-not (Get-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue)) { throw 'Hidden controller exited unexpectedly' }

$shell = New-Object -ComObject WScript.Shell
$shortcutResults = foreach ($name in @('Keyboard Black', 'Keyboard White')) {
    $path = Join-Path $desktop ($name + '.lnk')
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing shortcut: $path" }
    $shortcut = $shell.CreateShortcut($path)
    [pscustomobject]@{
        Name = $name
        TargetExists = Test-Path -LiteralPath $shortcut.TargetPath
        Arguments = $shortcut.Arguments
        IconExists = Test-Path -LiteralPath (($shortcut.IconLocation -split ',')[0].Trim('"'))
    }
}

[pscustomobject]@{
    HostStartMs = [math]::Round($hostStart.Elapsed.TotalMilliseconds)
    LastSwitchMs = $switchTimes['white']
    HostPid = $hostProcess.Id
    HiddenWindow = ($hostProcess.MainWindowHandle -eq 0)
    ChromaSuccessCount = ([regex]::Matches((Get-Content -LiteralPath $log -Raw), 'Applied (black|white)')).Count
    HeartbeatHealthy = $true
} | ConvertTo-Json -Compress
$shortcutResults | Format-Table -AutoSize
Get-Content -LiteralPath $log
