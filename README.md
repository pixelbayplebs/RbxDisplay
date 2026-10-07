# Stretcher 2.0.1 — source

Source distribution of the native WinUI 3 application. Start with **[BUILD.md](BUILD.md)** for Windows build instructions in Polish. **[USAGE.md](USAGE.md)** describes game profiles, display settings, recovery and the interface.

| Path | Purpose |
| --- | --- |
| `src/Stretcher.App/` | WinUI 3 interface, theme, window and tray integration, startup |
| `src/Stretcher.Core/` | Display modes, saturation, Roblox detection, saved profiles and recovery |
| `src/Stretcher.Watchdog/` | Independent restoration process |
| `tests/Stretcher.Core.Tests/` | xUnit checks for display, recovery, profiles and migration |
| `src/Stretcher.App/Assets/` | Original logos and application icon used by the build |
| `Build.cmd`, `Build.ps1` | Test and publish the complete Windows x64 distribution |

Application code, projects, assets and build scripts match the Stretcher 2.0.1 release. Executables and runtime dependencies are produced by the build. The Windows build generates the native resource index before deployment. See **[Verification.md](Verification.md)** for the release's validation scope.
