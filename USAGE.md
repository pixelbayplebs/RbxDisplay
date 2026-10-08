# RbxDisplay 2.0.1 — usage

This source archive must be built before starting the app; follow BUILD.md. The instructions below apply to the published application.

RbxDisplay is a native **WinUI 3** desktop application for saved Roblox game profiles. Each game has its own monitor, baseline, resolution, saturation and focus behavior. BloxStrike is included as the first preset. RbxDisplay matches the running game's Universe ID to a saved profile. Launching still uses that profile's Place ID.

## Start

1. Extract the **entire RbxDisplay folder** to a writable location, such as Downloads. Keep the executables, DLLs, Assets and resource files together.
2. Start **RbxDisplay.exe**. A Windows build creates the WinUI resource index during publishing. No .NET SDK is required on the computer running the published distribution. That computer does need the installed x64 .NET Runtime 10 (`Microsoft.NETCore.App`). The .NET Desktop Runtime includes that framework. A Windows App SDK installer is not required.
3. Monitoring starts with the app. Open **Games**, keep **Use the current Windows resolution as the baseline** enabled unless you want a manual baseline, and choose a supported **Game resolution**.
4. Use **Test for 15 seconds** to preview a profile, or **Play** to launch that game. RbxDisplay applies the matching profile when the game is in the foreground. You do not choose an active profile, and you do not press Start.

The distribution targets **Windows 10 version 2004 or later / Windows 11, x64**. Native WinUI files stay in the folder beside the executable. The .NET runtime is installed on the computer, not copied with the folder. The native Windows interface, first-launch resource indexing and actual driver behavior could not be run in the Linux build environment; see Verification.md for the checks performed and the Windows validation steps.

## Your game list

Use **Add game** to enter a name and a Roblox Place ID or a game link such as `https://www.roblox.com/games/123456789/Game`. RbxDisplay looks up that place's Universe ID when you save, which requires an internet connection. One place is enough: teleports to other places in the same experience stay on this profile. Additional Place IDs, separated with commas, are only needed when one profile should cover more than one experience. The launch link still uses the first Place ID.

The left navigation has **Live**, **Games**, and **Settings**. **Live** shows the detected game and the applied profile. **Games** is the library: selecting a row opens that profile for editing and saves the previous row. Changes to resolution, saturation, focus, monitor, and baseline are saved immediately. If that profile is the one currently applied, the desktop updates immediately too. Any saved game can be removed, including the last one, which leaves the list empty. Adding, editing, and removing stay available while a game is running.

The included BloxStrike preset uses Place ID **114234929420007**, Universe ID **7633926880**, resolution **1920 × 1440** and saturation **135%**. It is the starting preset and can be removed. The list can be empty.

## Display modes and stretching

Resolution dropdowns contain modes reported by the selected display driver and accepted by Windows' `CDS_TEST`. Game choices use the baseline's exact reported refresh rate, color depth, orientation and scan flags. RbxDisplay validates the selected modes again before applying them and does not lower Hz or substitute an unsupported saved resolution.

For a **3440 × 1440** screen, **1920 × 1440 (4:3)** is the default stretched profile. It is selected only if your driver supports it at the baseline Hz. If it is unavailable, the dropdown remains unselected until you choose a supported mode. Configure full-screen/panel scaling in your graphics driver if you want the lower aspect ratio to fill the panel; RbxDisplay does not change vendor scaling settings or create custom display modes.

Automatic baseline reads the current Windows mode at the start of every game session. The active session keeps its original snapshot, so the stretched mode cannot become its new baseline. With a manual baseline, the same selected Hz is used for both baseline and game modes.

The window uses a left navigation pane, scrolls the page that needs it, and fits the working area after a display mode change. Where Windows supports it, the shell uses Acrylic; otherwise the background stays `#242424`. All interface text is English.

## Colors and focus

**Enable saturation** and the **0–200%** slider are saved separately for each game. **100%** preserves the original colors. RbxDisplay uses the Windows fullscreen color transform, composing it with the existing transform and restoring that exact matrix. This is a global desktop effect, affecting all displays while the selected game is focused.

Colors return when the game loses focus. By default, the resolution remains stretched until you leave that game. Enable **Restore resolution when the game loses focus** to restore it on Alt+Tab as well.

## Restore and tray

- **Ctrl+Alt+F12** restores the display and leaves monitoring on. The profile stays off until that game loses focus or its process changes.
- From the copied folder, **RbxDisplay.Watchdog.exe --restore** runs the independent recovery process, including if the main interface cannot open. **Restore.cmd** is the source-tree wrapper for that process.
- **Minimize to tray** on the Settings page hides the window. Click the RbxDisplay tray icon to open it; right-click for Open, Restore display, or Exit.
- Closing the application restores an owned session. The separate **RbxDisplay.Watchdog.exe** also restores it if the application exits unexpectedly or stops sending heartbeats.

Restoration preserves display or color changes made by another tool. It checks the original monitor identity and retains unresolved recovery data if that monitor is disconnected. Profile mutation and watchdog recovery share a lock so recovery cannot run between writing the journal and applying its display change. Resolution changes are temporary; RbxDisplay never writes a stretched mode to the Windows display registry.

## Settings and diagnostics

Profiles: `%LOCALAPPDATA%\RbxDisplay\settings.json`  
Diagnostics: `%LOCALAPPDATA%\RbxDisplay\diagnostic.log`  
Recovery journal: `%LOCALAPPDATA%\RbxDisplay\session.json`

RbxDisplay keeps its own settings, diagnostics and recovery journal. It does not read or copy files from another program. A settings file already in this folder that still uses the single-monitor layout is upgraded in place: that monitor and baseline are copied onto every saved game. A malformed settings file blocks profile editing; **Settings** offers **Reset settings**, which backs up the existing file before saving defaults.

Game detection reads the current `RobloxPlayerBeta` process log. A join is confirmed by a Universe ID and a replicator connection. The first saved profile that lists that Universe ID is applied. Teleports within that universe keep the profile. Joining a different saved universe switches profiles. An unknown universe, leaving the game, disconnecting outside a teleport, and stale logs clear the stretch. There is no process injection or memory reading.

## Branding

Background: `#242424` · Foreground: `#FFF7E7` · Accent: `#f43f42`

`Assets/RbxDisplay.png` is the supplied transparent logo inside the app. `Assets/RbxDisplay.ico` is the icon for the executable, title bar, taskbar, minimized window and system tray. `Assets/RbxDisplayBackground.png` is the source artwork for that icon. Both PNG files are included unchanged.

## Build from source on Windows

Install the [stable .NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then run **Build.cmd**. `Build.ps1` runs the portable core tests, publishes the WinUI app with its native Windows resource index, publishes the watchdog and test runner, and copies the finalized distribution to this folder. It needs NuGet access; it does not require administrator privileges or a signing certificate.

The UI is built directly from native WinUI controls in C#, with a controls metadata provider. There is no WinForms or web-rendered frontend. Source projects are in `src/`, tests in `tests/`. **Start.cmd**, **Restore.cmd**, and **SelfTest.cmd** are source-tree commands; they are not in the copied `build` folder. **SelfTest.cmd** checks display rules, recovery decisions, detection and game profiles without changing display settings.

Primary implementation references: [WinUI unpackaged deployment](https://learn.microsoft.com/windows/apps/package-and-deploy/unpackage-winui-app), [Windows App SDK self-contained deployment](https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps), [MakePRI commands](https://learn.microsoft.com/windows/uwp/app-resources/makepri-exe-command-options), [Roblox deep links](https://create.roblox.com/docs/production/promotion/deeplinks).
