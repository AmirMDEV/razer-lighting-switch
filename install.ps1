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

Write-Output "Installed desktop shortcuts: Keyboard Black, Keyboard White"
