> [!CAUTION]
> Build RbxDisplay from the source in this repository.

> [!IMPORTANT]
> RbxDisplay 2.0.1 runs on Windows 10 version 2004 or later, and on Windows 11, x64.

<p align="center">
    <img src="src/RbxDisplay.App/Assets/RbxDisplay.png" width="380" alt="RbxDisplay">
</p>

<div align="center">

2.0.1 · Windows x64 · WinUI 3

</div>

----

RbxDisplay is a native WinUI 3 application for saved Roblox display profiles. Each game has its own monitor, baseline, resolution, saturation, and focus behavior. RbxDisplay matches the running game's Universe ID to a saved profile and launches that profile's Place ID.

Game detection reads the current `RobloxPlayerBeta` process log. A join is confirmed by a Universe ID and a replicator connection before the profile is applied.

For the game list, display modes, color, focus, and recovery, see [USAGE.md](USAGE.md). To build on Windows, see [BUILD.md](BUILD.md).

RbxDisplay runs on Windows only.

## Frequently Asked Questions

**Q: Is this malware?**

**A:** No. The source in this repository is what you build. RbxDisplay reads the `RobloxPlayerBeta` log, changes Windows display settings, and restores them. It does not inject code into the Roblox client or read process memory.

**Q: Can using this get me banned?**

**A:** RbxDisplay changes the Windows display mode and the fullscreen color transform, and it reads the player log. It does not inject code into the Roblox client or read process memory. That is not a promise about how Roblox enforces its rules.

## Features

- Each saved game keeps its own monitor, baseline, resolution, saturation, and focus behavior. BloxStrike is the included preset (Place ID `114234929420007`, Universe ID `7633926880`) and can be removed.
- The running game is matched by Universe ID. **Play** launches that profile's Place ID. Add a game with its name and a Place ID or a `roblox.com/games` link.
- Resolution choices are modes the selected display driver reports and Windows accepts with `CDS_TEST`, at the baseline refresh rate, color depth, orientation, and scan flags. RbxDisplay does not lower Hz or substitute an unsupported saved resolution.
- Saturation is saved per game on a 0–200% scale. **100%** leaves colors unchanged. RbxDisplay applies it with the Windows fullscreen color transform while that game is focused. The effect covers the desktop on every display.
- Colors return when the game loses focus. Resolution stays until you leave the game, unless **Restore resolution when the game loses focus** is on.
- **Ctrl+Alt+F12** restores the display and leaves monitoring on. The profile stays off until that game loses focus or its process changes. `RbxDisplay.Watchdog.exe` restores the session if the application exits unexpectedly or stops sending heartbeats. Closing the application restores a session it owns.
- **Test for 15 seconds** previews a profile. **Minimize to tray** hides the window; the tray icon opens it again.

## Installing

Install the [.NET 10 SDK, x64](https://dotnet.microsoft.com/download/dotnet/10.0), open a terminal in this folder, and run:

```powershell
dotnet --version
.\Build.cmd
.\build\RbxDisplay.exe
```

`dotnet --version` should show a stable `10.0.x`. [global.json](global.json) allows a newer stable SDK on the 10.0 line.

`Build.cmd` runs [Build.ps1](Build.ps1). The script runs the xUnit tests, publishes the WinUI app, the watchdog, and the test runner, checks that the resources are present, and writes the finished folder to `build`. On Windows it creates `resources.pri` during publish. A Windows App SDK installer is not required. The script needs NuGet access. It runs without administrator rights and without a signing certificate.

The computer that runs `build\RbxDisplay.exe` needs the installed x64 .NET Runtime 10 (`Microsoft.NETCore.App`). The .NET Desktop Runtime includes that framework. The SDK is only for building.

Copy the whole `build` folder when you move the app. Keep the executables, DLLs, `Assets`, and resource files together.

If the build fails, the cases and the test-only command are in [BUILD.md](BUILD.md). The first launch of the unsigned executable can trip Windows SmartScreen; see [Code signing](#code-signing) below.

## Code

The interface is native WinUI 3 in C#, built with [Windows App SDK 2.5.1](https://learn.microsoft.com/windows/apps/windows-app-sdk/). The app targets `net10.0-windows10.0.19041.0`, x64. It is unpackaged (`WindowsPackageType` is `None`). Native Windows App SDK files are published beside the executable (`WindowsAppSDKSelfContained`). The .NET runtime stays installed on the machine (`SelfContained` is `false`).

| Path | Purpose |
| --- | --- |
| `src/RbxDisplay.App/` | WinUI 3 interface, theme, window and tray integration, startup |
| `src/RbxDisplay.Core/` | Display modes, saturation, Roblox detection, saved profiles and recovery |
| `src/RbxDisplay.Watchdog/` | Independent restoration process |
| `tests/RbxDisplay.Core.Tests/` | xUnit checks for display, recovery and profiles |
| `src/RbxDisplay.App/Assets/` | Original logos and application icon used by the build |
| `Build.cmd`, `Build.ps1` | Test and publish the complete Windows x64 distribution |

What the 2.0.1 checks cover, and what still has to be tried on a Windows machine, is in [Verification.md](Verification.md).

## Code signing

The build is unsigned. This repository does not include a code-signing certificate. The first time you start `RbxDisplay.exe`, Windows SmartScreen may warn that the program is unrecognized. Choose **More info**, then **Run anyway**.
