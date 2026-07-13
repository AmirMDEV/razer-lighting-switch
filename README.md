# Razer Lighting Switch

Two instant Windows desktop shortcuts for a Razer Chroma keyboard:

- `Keyboard Black` turns lighting off with `CHROMA_NONE`
- `Keyboard White` applies static white with `CHROMA_STATIC`

One hidden controller keeps the Razer Chroma REST session alive. Later launches send their state to the existing controller and exit immediately.

## Build and install

```powershell
dotnet publish -c Release -o publish
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

Logs: `%LOCALAPPDATA%\Amir\RazerLightingSwitch\controller.log`
