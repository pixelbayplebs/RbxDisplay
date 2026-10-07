# Verification — Stretcher 2.0.1

## Completed

- Actual WinUI 3 source compiled against Microsoft.WindowsAppSDK 2.5.1, targeting `net10.0-windows10.0.19041.0`, x64. Native WinUI controls, AppWindow APIs, metadata provider, theme resources and HWND integration are type-checked against Microsoft's assemblies.
- **Display, detection and recovery checks passed** under xUnit: exact Hz preservation, driver catalog filtering, driver rejection, no unavailable-mode fallback, log connection confirmation, stale/teleport/disconnect handling, color composition, exact and idempotent recovery, external changes, disconnected monitors, partial failures, 64-bit journal serialization and watchdog parent identity.
- **Profile, migration and locking checks passed** under xUnit: custom games and links, exact large IDs, rejected malformed/spoofed input, saved selection, independent per-game settings, frozen runtime profiles, duplicate validation, settings round trips, literal JSON compatibility, migration without rewriting the legacy file, malformed/incomplete/versioned config preservation, and mutation/recovery exclusion.
- Windows x64 app, watchdog and test runner published with runtime files. Both supplied logos are retained byte-for-byte; the executable/taskbar/tray icon uses the background variant.
- The in-window logo uses WinUI's local-resource `ms-appx:///Assets/Stretcher.png` URI. Image-loading failures are written to diagnostics. This correction is compiled into the executable; its rendering still requires the Windows validation below.

## Build environment and resource indexing

The build host is Linux and cannot execute the Windows SDK resource indexer or the WinUI window. The code-only frontend is compiled normally by the .NET SDK. The SDK-generated activation manifest is merged without dropping its activation entries. Cross-publish skips the Windows-only PRI build tasks; the distribution contains Microsoft's native `makepri.exe` and prepares `resources.pri` once on the first Windows launch. This path has not been executed on Windows here. `Build.cmd` on Windows uses the regular SDK manifest/PRI tasks and produces a fully finalized publish.

These are compilation and core-logic checks. They do **not** constitute a visual, driver, tray, hotkey, Magnification API, Roblox live-log or first-launch smoke test on Windows.

## Windows validation

1. Extract the complete folder and start Stretcher.exe. Confirm first-launch resource preparation succeeds, the logo loads, all controls are visible/scrollable, the taskbar/tray icon uses the background logo, and accent is green.
2. Check the current mode and baseline Hz on your 3440 × 1440 monitor. Confirm the game dropdown contains only modes at that Hz, and 1920 × 1440 is offered only if accepted by the driver.
3. Add a second Roblox game using its `/games/` URL. Save different resolution/saturation values for both games. Switch and restart to confirm independent persistence.
4. Use Test for 15 seconds, including Alt+Tab. Confirm exact mode and color restoration, the emergency hotkey and Restore.cmd.
5. Start monitoring, join the selected game, leave/teleport, and switch focus. Confirm the appropriate activation and restoration.
6. Test the window at your Windows scaling setting, resize it narrowly, and minimize/reopen from the tray.
7. While a test is active, end the main process. Confirm the watchdog restores the journal. Check diagnostics if restoration needs attention.
