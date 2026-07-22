# Changelog

## Unreleased

- Fixed Black to send a real static black Chroma effect instead of clearing the effect and allowing Razer's rainbow default to resume.
- Black now persists as black at 100% brightness, so the next startup restores the requested state.
- Extended the hidden startup controller retry window to five minutes while Chroma services finish loading.

## 1.2.1 - 2026-07-18

- Made the Windows startup launch wait briefly and retry when the Razer Chroma service has not finished starting yet.
- Kept manual launches immediate, with no background retry loop after the tray controller is running.

## 1.2.0 - 2026-07-13

- Renamed public product to Razor Lightweight Keyboard Lighting Control
- Added visible Start with Windows checkbox to RGB popup
- Added Built by Amir footer Follow Amir link and Donate button
- Added embedded executable icon fallback for portable public use
- Added compressed self-contained Windows x64 release build
- Added release packaging script SHA-256 proof and MIT license
- Increased bounded cold Chroma request timeout after live public-name registration exceeded 3 seconds
- Refreshed public copy around full RGB customization and global hotkeys
- Added a real software screenshot to the public README

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
