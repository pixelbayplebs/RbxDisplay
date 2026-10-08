@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
if errorlevel 1 goto fail

set "LINE="
for /f "delims=" %%L in ('findstr /C:"<Version>" Directory.Build.props') do set "LINE=%%L"
set "VERSION=!LINE:*>=!"
for /f "delims=<" %%A in ("!VERSION!") do set "VERSION=%%A"
if not defined VERSION (
    echo Directory.Build.props does not contain a Version.
    goto fail
)

if not exist "build\RbxDisplay.exe" (
    echo build\RbxDisplay.exe is missing.
    goto fail
)

set "ZIP=RbxDisplay-%VERSION%-win-x64.zip"
if exist "%ZIP%" del "%ZIP%"
tar.exe -a -cf "%ZIP%" -C build .
if errorlevel 1 goto fail

echo Release complete. %ZIP%
exit /b 0

:fail
pause
exit /b 1
