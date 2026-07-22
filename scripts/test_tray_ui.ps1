param(
    [string]$PublishDirectory = "$PSScriptRoot\..\publish",
    [string]$ScreenshotDirectory = "$PSScriptRoot\..\..\.codex-temp"
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $PublishDirectory 'RazorLightweightKeyboardLightingControl.exe'
$log = Join-Path $env:LOCALAPPDATA 'Amir\RazerLightingSwitch\controller.log'
$settingsPath = Join-Path $env:LOCALAPPDATA 'Amir\RazerLightingSwitch\settings.json'
$suppressExternal = Join-Path $env:LOCALAPPDATA 'Amir\RazerLightingSwitch\.suppress-external-launch'
New-Item -ItemType Directory -Force -Path $ScreenshotDirectory | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class TrayNative {
  public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr value);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder text, int maxCount);
  [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
}
'@

function Wait-Log([string]$Pattern, [int]$StartingLines) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        Start-Sleep -Milliseconds 40
        $lines = @(Get-Content -LiteralPath $log -ErrorAction SilentlyContinue)
        if ($lines.Count -gt $StartingLines -and ($lines[$StartingLines..($lines.Count - 1)] -match $Pattern)) { return }
    } while ($timer.Elapsed.TotalSeconds -lt 4)
    throw "Timed out waiting for log pattern: $Pattern"
}

function Invoke-Command([string]$Command, [string]$Pattern) {
    $count = @(Get-Content -LiteralPath $log).Count
    Start-Process -FilePath $exe -ArgumentList $Command -WindowStyle Hidden
    Wait-Log $Pattern $count
}

function Capture-Window([IntPtr]$Handle, [string]$Path) {
    $rect = New-Object TrayNative+RECT
    if (-not [TrayNative]::GetWindowRect($Handle, [ref]$rect)) { throw 'GetWindowRect failed' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object Drawing.Bitmap $width, $height
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    try {
        if (-not [TrayNative]::PrintWindow($Handle, $hdc, 2)) { throw 'PrintWindow failed' }
    } finally {
        $graphics.ReleaseHdc($hdc)
        $graphics.Dispose()
    }
    $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

$registration = Get-Content -LiteralPath $log | Where-Object { $_ -match 'Hotkey registration' } | Select-Object -Last 1
if ($registration -notmatch 'black=True white=True rgb=True hwnd=(\d+)') { throw "Hotkey registration unhealthy: $registration" }
$hotkeyWindow = [IntPtr][long]$Matches[1]

$count = @(Get-Content -LiteralPath $log).Count
[TrayNative]::PostMessage($hotkeyWindow, 0x0312, [IntPtr]1, [IntPtr]0) | Out-Null
Wait-Log 'Applied black' $count
$count = @(Get-Content -LiteralPath $log).Count
[TrayNative]::PostMessage($hotkeyWindow, 0x0312, [IntPtr]2, [IntPtr]0) | Out-Null
Wait-Log 'Applied white' $count
$count = @(Get-Content -LiteralPath $log).Count
[TrayNative]::PostMessage($hotkeyWindow, 0x0312, [IntPtr]3, [IntPtr]0) | Out-Null
Wait-Log 'Picker shown' $count
$pickerLine = Get-Content -LiteralPath $log | Where-Object { $_ -match 'Picker hwnd=' } | Select-Object -Last 1
if ($pickerLine -notmatch 'Picker hwnd=(\d+)') { throw 'RGB picker handle missing' }
$picker = [IntPtr][long]$Matches[1]

$children = New-Object Collections.Generic.List[IntPtr]
$callback = [TrayNative+EnumProc]{ param($hwnd, $unused) $children.Add($hwnd); return $true }
[TrayNative]::EnumChildWindows($picker, $callback, [IntPtr]::Zero) | Out-Null
$wheel = [IntPtr]::Zero
foreach ($child in $children) {
    $rect = New-Object TrayNative+RECT
    [TrayNative]::GetWindowRect($child, [ref]$rect) | Out-Null
    if (($rect.Right - $rect.Left) -ge 200 -and ($rect.Bottom - $rect.Top) -ge 200) { $wheel = $child; break }
}
if ($wheel -eq [IntPtr]::Zero) { throw 'RGB wheel child control missing' }
$startupCheckbox = [IntPtr]::Zero
$donateButton = [IntPtr]::Zero
$brightnessSlider = [IntPtr]::Zero
$childClasses = @()
foreach ($child in $children) {
    $text = New-Object Text.StringBuilder 128
    [TrayNative]::GetWindowText($child, $text, $text.Capacity) | Out-Null
    $className = New-Object Text.StringBuilder 128
    [TrayNative]::GetClassName($child, $className, $className.Capacity) | Out-Null
    $childClasses += $className.ToString()
    if ($text.ToString() -eq 'Start with Windows') { $startupCheckbox = $child }
    if ($text.ToString() -eq 'Donate') { $donateButton = $child }
    if ($className.ToString() -like '*msctls_trackbar32*') { $brightnessSlider = $child }
}
if ($startupCheckbox -eq [IntPtr]::Zero) { throw 'Startup checkbox missing' }
if ($donateButton -eq [IntPtr]::Zero) { throw 'Donate button missing' }
if ($brightnessSlider -eq [IntPtr]::Zero) { throw "Brightness slider missing. Found classes: $($childClasses -join ', ')" }
$beforeWheel = @(Get-Content -LiteralPath $log).Count
$point = [IntPtr]((107 -shl 16) -bor 205)
[TrayNative]::PostMessage($wheel, 0x0201, [IntPtr]1, $point) | Out-Null
[TrayNative]::PostMessage($wheel, 0x0202, [IntPtr]0, $point) | Out-Null
Wait-Log 'Applied rgb' $beforeWheel

$beforeBrightness = @(Get-Content -LiteralPath $log).Count
[TrayNative]::SetFocus($brightnessSlider) | Out-Null
for ($i = 0; $i -lt 58; $i++) {
    [TrayNative]::SendMessage($brightnessSlider, 0x0100, [IntPtr]0x25, [IntPtr]::Zero) | Out-Null
    [TrayNative]::SendMessage($brightnessSlider, 0x0101, [IntPtr]0x25, [IntPtr]::Zero) | Out-Null
}
Wait-Log 'Applied rgb' $beforeBrightness
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
if ($settings.Brightness -ne 42) { throw "Brightness slider persistence failed: $($settings.Brightness)" }

$existingRun = (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).AmirRazerLightingSwitch
if ($null -eq $existingRun) { Invoke-Command 'startup-on' 'Startup enabled' }
$count = @(Get-Content -LiteralPath $log).Count
[TrayNative]::SendMessage($startupCheckbox, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Wait-Log 'Startup disabled' $count
$runKey = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue
$runValue = $runKey.AmirRazerLightingSwitch
if ($null -ne $runValue) { throw 'Startup disable failed' }
$count = @(Get-Content -LiteralPath $log).Count
[TrayNative]::SendMessage($startupCheckbox, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Wait-Log 'Startup enabled' $count
$runValue = Get-ItemPropertyValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'AmirRazerLightingSwitch'
if ($runValue -notmatch 'RazorLightweightKeyboardLightingControl.exe.+startup') { throw 'Startup enable failed' }

New-Item -ItemType File -Force -Path $suppressExternal | Out-Null
try {
    $count = @(Get-Content -LiteralPath $log).Count
    [TrayNative]::SendMessage($donateButton, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Wait-Log 'Donate opened: https://www.paypal.com/donate/\?hosted_button_id=2U2GXSKFJKJCA' $count
} finally {
    Remove-Item -LiteralPath $suppressExternal -Force -ErrorAction SilentlyContinue
}

Start-Process -FilePath $exe -ArgumentList 'show-light' -WindowStyle Hidden
Start-Sleep -Milliseconds 300
$lightPath = Join-Path $ScreenshotDirectory 'razer-tray-light.png'
Capture-Window $picker $lightPath

Start-Process -FilePath $exe -ArgumentList 'show-dark' -WindowStyle Hidden
Start-Sleep -Milliseconds 300
$darkPath = Join-Path $ScreenshotDirectory 'razer-tray-dark.png'
Capture-Window $picker $darkPath

[TrayNative]::ShowWindow($picker, 0) | Out-Null
Invoke-Command 'white' 'Applied white'

[pscustomobject]@{
    HotkeyBlack = $true
    HotkeyWhite = $true
    HotkeyRgbPopup = $true
    WheelInteraction = $true
    BrightnessPersistence = $settings.Brightness
    StartupToggle = $true
    StartupCheckbox = $true
    DonateButton = $true
    StartupValue = $runValue
    FinalState = 'white 100%'
    LightScreenshot = $lightPath
    DarkScreenshot = $darkPath
} | ConvertTo-Json -Compress
