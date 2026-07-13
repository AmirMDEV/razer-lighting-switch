# Changelog

## 1.1.0 - 2026-07-13

- Added lightweight system tray controller with compact RGB wheel and brightness slider
- Added global hotkeys for black white and RGB popup
- Added persistent color brightness and last-mode settings
- Added Start with Windows registration enabled during install
- Preserved instant desktop shortcuts and hidden no-console controller
- Added safe build-install wrapper that stops the running tray before replacing its executable
- Added deterministic background-safe tray UI hotkey wheel brightness startup smoke coverage
- Assigned RGB popup to conflict-free `Ctrl+Alt+L` after live registration detected `Ctrl+Alt+R` already occupied

## 1.0.0 - 2026-07-13

- Added hidden Razer Chroma REST controller with heartbeat persistence
- Added instant black and white named-pipe commands
- Added two desktop shortcuts with distinct icons
- Added no-console single-file Windows publish flow
