> [!IMPORTANT]
> RbxDisplay 2.0.1 runs on 64-bit Windows 10, version 2004 (May 2020) or newer, and on 64-bit Windows 11.

<p align="center">
    <img src="src/RbxDisplay.App/Assets/RbxDisplayFull.png" width="640" alt="RbxDisplay">
</p>

----

RbxDisplay remembers a display setup for each Roblox game. While that game is the window in front, it applies that game's monitor, everyday resolution, game resolution, color, and Alt+Tab behavior. When you leave the game, it puts the display back.

It sees which game you joined by reading Roblox's own log. **Play** opens the game link saved for that setup.

How to use the window, the game list, and recovery is in [USAGE.md](USAGE.md). Building the app yourself is optional and is described in [BUILD.md](BUILD.md).

RbxDisplay runs on Windows only.

## Frequently Asked Questions

**Q: Is this malware?**

**A:** No. The source code is on this page, and the files on Releases are built from it. RbxDisplay changes Windows display settings while a saved Roblox game is in front, then restores them. It reads Roblox's log to see which game you joined. It does not change the Roblox client, inject code into it, or read the game's memory.

**Q: Can using this get me banned?**

**A:** RbxDisplay changes your monitor settings in Windows and reads Roblox's log. It does not modify the Roblox client. Roblox still applies its own rules. This is not a promise that an account cannot be punished.

## Features

- Each game keeps its own setup: which monitor, your everyday resolution, the resolution for that game, color strength, and what happens when you switch away. BloxStrike is included (Place ID `114234929420007`) and can be removed.
- The setup follows the game you are in. **Play** opens the Roblox place saved for it. Add a game with a name and a Place ID, or paste a `roblox.com/games` link.
- The resolution list only includes modes your monitor reports and Windows accepts, at the same refresh rate as your everyday picture. RbxDisplay keeps that refresh rate. If a saved game resolution is missing from the list, it leaves the choice empty until you pick one that is there.
- Color is saved per game, from 0% to 200%. **100%** is normal color. While that game is in front, the color change covers the whole desktop, on every monitor.
- Color comes back when the game is no longer the front window. The game resolution stays until you leave the game, unless **Restore resolution when the game loses focus** is on.
- **Ctrl+Alt+F12** puts the display back at once. RbxDisplay stays open. That game's setup stays off until you leave the game or the game closes. If RbxDisplay closes unexpectedly, `RbxDisplay.Watchdog.exe` puts the display back. Closing RbxDisplay yourself also puts back a display it changed.
- **Test for 15 seconds** shows a setup before you join. **Minimize to tray** hides the window. Click the RbxDisplay icon by the clock to open it. Right-click that icon for Open, Restore display, or Exit.

## Installing

1. Download the [latest release](https://github.com/pixelbayplebs/Stretcher/releases/latest).
2. If the download is a zip file, right-click it and choose **Extract All**. Use the folder that Windows creates. A good place is Downloads or the Desktop. Leave the files together: `RbxDisplay.exe`, `RbxDisplay.Watchdog.exe`, the DLL files, and the `Assets` folder. Open `RbxDisplay.exe` from that folder. Opening it from inside the zip window leaves those files behind, and the app cannot run that way.
3. The PC needs the 64-bit .NET Desktop Runtime 10. If a window says the app cannot start and asks for .NET, open the [.NET 10 download page](https://dotnet.microsoft.com/download/dotnet/10.0), download **Desktop Runtime** for **x64**, install it, and start `RbxDisplay.exe` again.
4. The first time you open `RbxDisplay.exe`, Windows can show a blue window titled **Windows protected your PC**. The text says Microsoft Defender SmartScreen prevented an unrecognized app from starting. Publisher is listed as unknown. The button on the window is **Don't run**.

   That window is the unknown-publisher warning. This release has no digital signature, so Windows has not seen this publisher before. A different window, one that says a virus or threat was found, is not this step. Close that window and do not continue.

   On the blue unknown-app window, click the **More info** link. A **Run anyway** button appears. Click **Run anyway**. RbxDisplay opens after that.

## Building from source

Skip this section if you downloaded a release.

To compile RbxDisplay yourself, install the [.NET 10 SDK, x64](https://dotnet.microsoft.com/download/dotnet/10.0), open a terminal in this folder, and run:

```powershell
dotnet --version
.\Build.cmd
.\build\RbxDisplay.exe
```

`dotnet --version` should show a stable `10.0.x`. [global.json](global.json) allows a newer stable SDK on the 10.0 line.

`Build.cmd` runs [Build.ps1](Build.ps1). The script runs the xUnit tests, publishes the WinUI app, the watchdog, and the test runner, checks that the resources are present, and writes the finished folder to `build`. On Windows it creates `resources.pri` during publish. The Windows App SDK files are copied next to the program, so there is no separate Windows App SDK install. The script needs NuGet access. It runs without administrator rights and without a signing certificate.

Copy the whole `build` folder when you move that build. Keep the executables, DLLs, `Assets`, and resource files together. The first start can show the same SmartScreen window as in [Installing](#installing).

If the build fails, the cases and the test-only command are in [BUILD.md](BUILD.md).

## Code

Skip this section if you only want to run the app.

The window is native WinUI 3 in C#, built with [Windows App SDK 2.5.1](https://learn.microsoft.com/windows/apps/windows-app-sdk/). The app targets `net10.0-windows10.0.19041.0`, 64-bit. It is unpackaged (`WindowsPackageType` is `None`). The Windows App SDK files ship beside the program (`WindowsAppSDKSelfContained`). The .NET runtime stays installed on the PC (`SelfContained` is `false`).

| Path | Purpose |
| --- | --- |
| `src/RbxDisplay.App/` | WinUI 3 interface, theme, window and tray integration, startup |
| `src/RbxDisplay.Core/` | Display modes, saturation, Roblox detection, saved profiles and recovery |
| `src/RbxDisplay.Watchdog/` | Independent restoration process |
| `tests/RbxDisplay.Core.Tests/` | xUnit checks for display, recovery and profiles |
| `src/RbxDisplay.App/Assets/` | Original logos and application icon used by the build |
| `Build.cmd`, `Build.ps1` | Test and publish the complete Windows x64 distribution |

What the 2.0.1 checks cover, and what still has to be tried on a Windows PC, is in [Verification.md](Verification.md).

## Code signing

Releases are not digitally signed. This repository does not include a code-signing certificate. The first time you start `RbxDisplay.exe`, Windows can show the unknown-app SmartScreen window. Click **More info**, then **Run anyway**. The window is described step by step in [Installing](#installing). If the text says a virus or threat was found, that is a different warning. Close it and do not use **Run anyway**.
