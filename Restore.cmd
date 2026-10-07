@echo off
cd /d "%~dp0build"
"%~dp0build\Stretcher.Watchdog.exe" --restore
if errorlevel 1 (
  echo Restoration needs attention. Check %%LOCALAPPDATA%%\Stretcher\diagnostic.log and try again.
  pause
) else (
  echo Display settings restored. No active session means no display changes.
)
