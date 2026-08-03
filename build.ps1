# build.ps1 -- builds every project and assembles a ready-to-run folder at .\dist
# Run from the repository root in PowerShell on a machine with the .NET 8 SDK installed:
#   .\build.ps1

$ErrorActionPreference = "Stop"
$config = "Release"

Write-Host "Restoring & building solution ($config)..." -ForegroundColor Cyan
dotnet build DirectorySorter.sln -c $config

$distRoot = Join-Path $PSScriptRoot "dist"
$appOut   = Join-Path $PSScriptRoot "src\DirectorySorter.App\bin\$config\net8.0"

if (Test-Path $distRoot) { Remove-Item $distRoot -Recurse -Force }
New-Item -ItemType Directory -Path $distRoot | Out-Null

Write-Host "Copying App + Plugins + Watcher into dist\..." -ForegroundColor Cyan
Copy-Item "$appOut\*" $distRoot -Recurse -Force

$watcherOut = Join-Path $PSScriptRoot "src\DirectorySorter.Watcher\bin\$config\net8.0"
Copy-Item "$watcherOut\DirectorySorter.Watcher.exe" $distRoot -Force
Copy-Item "$watcherOut\DirectorySorter.Watcher.dll" $distRoot -Force
Copy-Item "$watcherOut\DirectorySorter.Watcher.runtimeconfig.json" $distRoot -Force

Copy-Item (Join-Path $PSScriptRoot "sorter.config.json") $distRoot -Force

Write-Host ""
Write-Host "Build complete. Ready-to-run files are in: $distRoot" -ForegroundColor Green
Write-Host "  dist\DirectorySorter.exe          <folder> --strategy=extension --dry-run"
Write-Host "  dist\DirectorySorter.Watcher.exe  (runs continuously per sorter.config.json)"
Write-Host "  dist\Plugins\*.dll                (drop new plugin DLLs here to extend it)"
