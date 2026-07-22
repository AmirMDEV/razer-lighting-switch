# Razer lighting switch

## Static black fell back to rainbow and Chroma started too slowly

- **Symptom:** Selecting **Black** logged success but the keyboard returned to Razer's cycling colours. The RGB panel could save a very low brightness, and the startup controller could exit before Chroma opened its local REST service.
- **Root cause:** `ChromaClient.ApplyCommandAsync` sent `CHROMA_NONE` for black. That clears the SDK effect instead of applying black, so Chroma resumes its own default effect. Startup retried a fixed number of times and then exited. The local settings had also persisted `rgb` at 1% brightness.
- **Fix:** Send static `Color.Black` at 100% for the black command; persist black, `#000000`, and 100%; retry the startup Chroma connection for up to five minutes. The tray smoke test now drives the native brightness trackbar rather than only testing the external RGB command.
- **Files:** `ChromaClient.cs`, `TrayAppContext.cs`, `Program.cs`, `scripts/test_tray_ui.ps1`, `CHANGELOG.md`.
- **Commands:** `dotnet build RazerLightingSwitch.csproj -c Release`; `scripts/build_install.ps1 -Configuration Release`; `scripts/test_tray_ui.ps1`.
- **Proof:** The installed controller accepted real Chroma black and persisted `LastMode=black`, `ColorArgb=-16777216`, `Brightness=100`; tray test proved hotkeys, wheel, native brightness control (42%), startup registration, and Chroma responses.
- **Final health:** The Run entry starts the installed publish executable with `startup` and the current controller state is static black at 100%.
- **Do not retry:** Do not use `CHROMA_NONE` for an off/black effect. Do not treat a successful log entry as proof of black unless the command is a static black effect. Do not assume six startup retries cover a slow Chroma boot.
