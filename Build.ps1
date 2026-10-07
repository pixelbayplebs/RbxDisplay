param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if ($env:OS -ne 'Windows_NT') { throw 'The release build requires Windows for native WinUI resource indexing.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 10 SDK, then run Build.cmd again.' }
$sdk = & dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^10\.') { throw 'Install the .NET 10 SDK. global.json selects a stable .NET 10 feature band.' }
function Run-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $($Arguments -join ' ')" }
}
function Copy-TestClosure([string]$Checks, [string]$Build) {
    $names = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @('Stretcher.Core.Tests.exe', 'Stretcher.Core.Tests.dll', 'Stretcher.Core.Tests.deps.json', 'Stretcher.Core.Tests.runtimeconfig.json')) {
        [void]$names.Add($name)
    }
    $depsPath = Join-Path $Checks 'Stretcher.Core.Tests.deps.json'
    if (Test-Path $depsPath) {
        $deps = Get-Content $depsPath -Raw | ConvertFrom-Json
        foreach ($target in $deps.targets.PSObject.Properties) {
            foreach ($package in $target.Value.PSObject.Properties) {
                foreach ($kind in @('runtime', 'native')) {
                    $group = $package.Value.$kind
                    if ($null -eq $group) { continue }
                    foreach ($asset in @($group.PSObject.Properties.Name)) {
                        [void]$names.Add([System.IO.Path]::GetFileName(($asset -replace '/', '\')))
                    }
                }
            }
        }
    }
    foreach ($name in $names) {
        $source = Join-Path $Checks $name
        if (-not (Test-Path $source)) { continue }
        $dest = Join-Path $Build $name
        if ((Test-Path $dest) -and ((Get-FileHash $source).Hash -ne (Get-FileHash $dest).Hash)) { continue }
        Copy-Item $source $dest -Force
    }
}
if (-not $SkipTests) { Run-Dotnet -Arguments @('test', 'tests/Stretcher.Core.Tests/Stretcher.Core.Tests.csproj', '-c', 'Release') }
$build = Join-Path $PSScriptRoot 'build'
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ('Stretcher-stage-' + [guid]::NewGuid().ToString('N'))
$guard = Join-Path $stage 'watchdog'
$checks = Join-Path $stage 'checks'
if (Test-Path $build) { Remove-Item $build -Recurse -Force }
try {
    Run-Dotnet -Arguments @('publish', 'src/Stretcher.App/Stretcher.App.csproj', '-c', 'Release', '-p:Platform=x64', '-o', $build)
    Run-Dotnet -Arguments @('publish', 'src/Stretcher.Watchdog/Stretcher.Watchdog.csproj', '-c', 'Release', '-p:Platform=x64', '-o', $guard)
    Run-Dotnet -Arguments @('publish', 'tests/Stretcher.Core.Tests/Stretcher.Core.Tests.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $checks)
    if (-not (Test-Path (Join-Path $build 'resources.pri'))) { throw 'WinUI resource indexing did not produce resources.pri. The release was not copied.' }
    $required = @('Stretcher.exe', 'Stretcher.dll', 'Stretcher.deps.json', 'Stretcher.runtimeconfig.json', 'Stretcher.Core.dll', 'Microsoft.UI.Xaml.Controls.dll', 'Microsoft.WinUI.dll', 'Assets/Stretcher.ico', 'Assets/Stretcher.png')
    foreach ($file in $required) { if (-not (Test-Path (Join-Path $build $file))) { throw "The release is missing $file" } }
    foreach ($file in @('Stretcher.Watchdog.exe', 'Stretcher.Watchdog.dll', 'Stretcher.Watchdog.deps.json', 'Stretcher.Watchdog.runtimeconfig.json')) { Copy-Item (Join-Path $guard $file) $build -Force }
    Copy-TestClosure $checks $build
    $pending = Join-Path $build 'Stretcher.resources.pending'
    if (Test-Path $pending) { Remove-Item $pending -Force }
    Write-Host 'Build complete. Start build\Stretcher.exe. The Windows resource index is finalized.'
} finally {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
}
