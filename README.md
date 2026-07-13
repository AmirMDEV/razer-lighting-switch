# Razer Lighting Switch

Two instant Windows desktop shortcuts for a Razer Chroma keyboard:

- `Keyboard Black` turns lighting off with `CHROMA_NONE`
- `Keyboard White` applies static white with `CHROMA_STATIC`

One hidden tray controller keeps the Razer Chroma REST session alive. Later launches send their state to the existing controller and exit immediately.

## Tray controls

- Left-click tray icon: compact RGB wheel and brightness slider
- `Ctrl+Alt+B`: black
- `Ctrl+Alt+W`: white
- `Ctrl+Alt+L`: open RGB wheel
- Right-click tray icon: presets startup toggle and exit
- Starts with Windows by default

## Build and install

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build_install.ps1
```

Logs: `%LOCALAPPDATA%\Amir\RazerLightingSwitch\controller.log`

Settings: `%LOCALAPPDATA%\Amir\RazerLightingSwitch\settings.json`
