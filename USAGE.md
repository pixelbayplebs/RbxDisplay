# Stretcher 2.0.1 — usage

This source archive must be built before starting the app; follow BUILD.md. The instructions below apply to the published application.

Stretcher is a native **WinUI 3** desktop application for saved Roblox game profiles. Each game has its own resolution, saturation and focus behavior. BloxStrike is included as the first preset. Detection uses the selected game's Universe ID. Launching still uses its Place ID.

## Start

1. Extract the **entire Stretcher folder** to a writable location, such as Downloads. Keep the executables, DLLs, Assets and resource files together.
2. Start **Stretcher.exe** or **Start.cmd**. A Windows build creates the WinUI resource index during publishing. No .NET SDK is required on the computer running the published distribution.
3. Select your game and monitor. Keep **Use the current Windows resolution as the baseline** enabled unless you want to define a manual baseline.
4. Choose a supported **Game resolution** from the dropdown. Use **Test for 15 seconds** to check it, then **Start monitoring** or **Play selected game**.

The distribution targets **Windows 10 version 2004 or later / Windows 11, x64**. It includes the .NET 10 and Windows App SDK runtimes. The native Windows interface, first-launch resource indexing and actual driver behavior could not be run in the Linux build environment; see Verification.md for the checks performed and the Windows validation steps.

## Your game list

Use **Add game** to enter a name and a Roblox Place ID or a game link such as `https://www.roblox.com/games/123456789/Game`. Stretcher looks up that place's Universe ID when you save, which requires an internet connection. One place is enough: teleports to other places in the same experience stay on this profile. Additional Place IDs, separated with commas, are only needed when one profile should cover more than one experience. The launch link still uses the first Place ID.

Selecting an item in **Saved game** loads its saved profile. Switching games also saves the previous game's edited values. **Save profile** explicitly saves the current choices. Adding, editing, removing and selecting games persists across restarts. The final remaining game cannot be removed. Game and display editing is disabled while monitoring or testing; use **Restore and stop** first.

The included BloxStrike preset uses Place ID **114234929420007**, Universe ID **7633926880**, resolution **1920 × 1440** and saturation **135%**. It is an editable preset, and can be removed once another game is saved.

## Display modes and stretching

Resolution dropdowns contain modes reported by the selected display driver and accepted by Windows' `CDS_TEST`. Game choices use the baseline's exact reported refresh rate, color depth, orientation and scan flags. Stretcher validates the selected modes again before applying them and does not lower Hz or substitute an unsupported saved resolution.

For a **3440 × 1440** screen, **1920 × 1440 (4:3)** is the default stretched profile. It is selected only if your driver supports it at the baseline Hz. If it is unavailable, the dropdown remains unselected until you choose a supported mode. Configure full-screen/panel scaling in your graphics driver if you want the lower aspect ratio to fill the panel; Stretcher does not change vendor scaling settings or create custom display modes.

Automatic baseline reads the current Windows mode at the start of every game session. The active session keeps its original snapshot, so the stretched mode cannot become its new baseline. With a manual baseline, the same selected Hz is used for both baseline and game modes.

The layout scrolls vertically, adapts button columns to the available width, and fits the window to the working area after a display mode change. All interface text is English.

## Colors and focus

**Enable saturation** and the **0–200%** slider are saved separately for each game. **100%** preserves the original colors. Stretcher uses the Windows fullscreen color transform, composing it with the existing transform and restoring that exact matrix. This is a global desktop effect, affecting all displays while the selected game is focused.

Colors return when the game loses focus. By default, the resolution remains stretched until you leave that game. Enable **Restore resolution when the game loses focus** to restore it on Alt+Tab as well.

## Restore and tray

- **Restore and stop** restores the owned session and stops monitoring.
- **Ctrl+Alt+F12** provides an emergency restore when the shortcut is available.
- **Restore.cmd** runs the independent recovery process, including if the main interface cannot open.
- **Minimize to tray** hides the window. Click the Stretcher tray icon to open it; right-click for Open, Restore and stop, or Exit.
- Closing the application restores an owned session. The separate **Stretcher.Watchdog.exe** also restores it if the application exits unexpectedly or stops sending heartbeats.

Restoration preserves display or color changes made by another tool. It checks the original monitor identity and retains unresolved recovery data if that monitor is disconnected. Profile mutation and watchdog recovery share a lock so recovery cannot run between writing the journal and applying its display change. Resolution changes are temporary; Stretcher never writes a stretched mode to the Windows display registry.

## Settings and diagnostics

Profiles: `%LOCALAPPDATA%\Stretcher\settings.json`  
Diagnostics: `%LOCALAPPDATA%\Stretcher\diagnostic.log`

On first use, settings from the earlier BloxMod/RobloxDisplayProfile release are imported without changing the old file. A custom old target becomes an Imported game, with BloxStrike retained as another preset. A malformed new settings file blocks profile editing and applying; the interface offers **Reset settings**, which backs up the existing file before saving defaults.

The recovery journal remains at `%LOCALAPPDATA%\RobloxDisplayProfile\session.json`, and the process/recovery mutex names retain that compatibility prefix. This lets Stretcher restore a journal from the earlier release and prevents two versions controlling the desktop together.

Game detection reads the current `RobloxPlayerBeta` process log. A join is confirmed by a Universe ID and a replicator connection. Teleports within that universe keep the current profile. Leaving the game, joining a different universe, disconnecting outside a teleport, and stale logs clear the previous target. There is no process injection or memory reading.

## Branding

Background: `#242424` · Foreground: `#FFF7E7` · Accent: `#8CF156`

`Assets/Stretcher.png` is the supplied transparent logo inside the app. `Assets/Stretcher.ico` is a multi-size icon converted from the supplied `StretcherBackground.png` for the executable, title bar, taskbar, minimized window and system tray. Both original PNG files are included unchanged.

## Build from source on Windows

Install the [stable .NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then run **Build.cmd**. `Build.ps1` runs the portable core tests, publishes the WinUI app with its native Windows resource index, publishes the watchdog and test runner, and copies the finalized distribution to this folder. It needs NuGet access; it does not require administrator privileges or a signing certificate.

The UI is built directly from native WinUI controls in C#, with a controls metadata provider. There is no WinForms or web-rendered frontend. Source projects are in `src/`, tests in `tests/`. Run **SelfTest.cmd** to check display rules, recovery decisions, detection, game profiles and migrations without changing display settings.

Primary implementation references: [WinUI unpackaged deployment](https://learn.microsoft.com/windows/apps/package-and-deploy/unpackage-winui-app), [Windows App SDK self-contained deployment](https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps), [MakePRI commands](https://learn.microsoft.com/windows/uwp/app-resources/makepri-exe-command-options), [Roblox deep links](https://create.roblox.com/docs/production/promotion/deeplinks).
