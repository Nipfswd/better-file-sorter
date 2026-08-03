<#
build.ps1 -- builds every project and assembles a clean, release-ready folder at .\dist

  .\build.ps1                  # Release build (default)
  .\build.ps1 -Configuration Debug

Symbols (.pdb) and XML doc-comment files are stripped out of dist\ so it's a lean
release payload, but they are NOT deleted -- they're moved into dist\symbols\
(same relative layout) so you still have them on hand if you ever need to debug
a crash from a shipped build.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "Restoring & building solution ($Configuration)..." -ForegroundColor Cyan
dotnet build DirectorySorter.sln -c $Configuration

$distRoot    = Join-Path $PSScriptRoot "dist"
$symbolsRoot = Join-Path $distRoot "symbols"
$appOut      = Join-Path $PSScriptRoot "src\DirectorySorter.App\bin\$Configuration\net8.0"
$watcherOut  = Join-Path $PSScriptRoot "src\DirectorySorter.Watcher\bin\$Configuration\net8.0"
$uiOut       = Join-Path $PSScriptRoot "src\DirectorySorter.UI\bin\$Configuration\net8.0-windows"

if (Test-Path $distRoot) { Remove-Item $distRoot -Recurse -Force }
New-Item -ItemType Directory -Path $distRoot    | Out-Null
New-Item -ItemType Directory -Path $symbolsRoot | Out-Null

Write-Host "Copying DirectorySorter.exe + Core.dll + Plugins\ into dist\..." -ForegroundColor Cyan
if (-not (Test-Path $appOut)) { throw "App build output not found at $appOut -- did the build above fail?" }
Copy-Item "$appOut\*" $distRoot -Recurse -Force

Write-Host "Copying DirectorySorter.Watcher.exe into dist\..." -ForegroundColor Cyan
Copy-Item "$watcherOut\DirectorySorter.Watcher.exe" $distRoot -Force
Copy-Item "$watcherOut\DirectorySorter.Watcher.dll" $distRoot -Force
Copy-Item "$watcherOut\DirectorySorter.Watcher.runtimeconfig.json" $distRoot -Force
Get-ChildItem "$watcherOut" -Filter "DirectorySorter.Watcher.*" | Copy-Item -Destination $distRoot -Force

Write-Host "Copying DirectorySorter.UI.exe into dist\..." -ForegroundColor Cyan
if (Test-Path $uiOut) {
    Copy-Item "$uiOut\*" $distRoot -Recurse -Force
} else {
    Write-Host "  (UI build output not found -- skipping. Requires the .NET desktop workload / Windows to build.)" -ForegroundColor Yellow
}

Copy-Item (Join-Path $PSScriptRoot "sorter.config.json") $distRoot -Force

Write-Host "Stripping debug symbols out of the release folder..." -ForegroundColor Cyan
$stripped = Get-ChildItem -Path $distRoot -Recurse -File -Include *.pdb,*.xml |
    Where-Object { $_.FullName -notlike "$symbolsRoot*" }

foreach ($file in $stripped) {
    $relative  = $file.FullName.Substring($distRoot.Length).TrimStart('\')
    $target    = Join-Path $symbolsRoot $relative
    $targetDir = Split-Path $target -Parent
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Move-Item $file.FullName $target -Force
}
Write-Host "  Moved $($stripped.Count) symbol/doc file(s) into dist\symbols\ (kept, just out of the way)." -ForegroundColor DarkGray

# Clean up any now-empty directories left behind by the move (but never touch symbols\ itself)
Get-ChildItem -Path $distRoot -Recurse -Directory |
    Where-Object { $_.FullName -ne $symbolsRoot -and (Get-ChildItem $_.FullName -Recurse -File | Measure-Object).Count -eq 0 } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

$zipPath = Join-Path $PSScriptRoot "DirectorySorter-release.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $distRoot '*') -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Build complete." -ForegroundColor Green
Write-Host "  Release folder : $distRoot"
Write-Host "  Debug symbols  : $symbolsRoot  (not required to run, kept for crash debugging)"
Write-Host "  Release zip    : $zipPath"
Write-Host ""
Write-Host "  dist\DirectorySorter.exe     <folder> --strategy=extension --dry-run"
Write-Host "  dist\DirectorySorter.exe     --strategy=rules --recursive   (uses sorter.config.json Rules)"
Write-Host "  dist\DirectorySorter.UI.exe  (GUI)"
Write-Host "  dist\DirectorySorter.Watcher.exe  (background auto-sort per sorter.config.json)"
