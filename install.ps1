param(
    [string]$PublishDirectory = "$PSScriptRoot\publish"
)

$ErrorActionPreference = 'Stop'
$desktop = [Environment]::GetFolderPath('Desktop')
$exe = Join-Path $PublishDirectory 'RazerLightingSwitch.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Missing published app: $exe" }

$shell = New-Object -ComObject WScript.Shell
$items = @(
    @{ Name = 'Keyboard Black'; Argument = 'black'; Icon = 'assets\keyboard-black.ico' },
    @{ Name = 'Keyboard White'; Argument = 'white'; Icon = 'assets\keyboard-white.ico' }
)

foreach ($item in $items) {
    $shortcutPath = Join-Path $desktop ($item.Name + '.lnk')
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.Arguments = $item.Argument
    $shortcut.WorkingDirectory = $PublishDirectory
    $shortcut.IconLocation = (Join-Path $PublishDirectory $item.Icon) + ',0'
    $shortcut.Description = "Set Razer keyboard lighting $($item.Argument)"
    $shortcut.Save()
}

$startupKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -Path $startupKey -Force | Out-Null
Set-ItemProperty -Path $startupKey -Name 'AmirRazerLightingSwitch' -Value ('"' + $exe + '" startup')

Get-Process -Name 'RazerLightingSwitch' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process -FilePath $exe -ArgumentList 'startup' -WindowStyle Hidden

Write-Output "Installed desktop shortcuts plus tray startup: Keyboard Black, Keyboard White"
