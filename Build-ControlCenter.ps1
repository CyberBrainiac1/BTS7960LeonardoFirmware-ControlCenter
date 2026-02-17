param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot "ControlCenter\ArduinoFFBControlCenter\ArduinoFFBControlCenter.csproj"
$solutionPath = Join-Path $repoRoot "ControlCenter\ArduinoFFBControlCenter.sln"
$publishPath = Join-Path $repoRoot "ControlCenter\ArduinoFFBControlCenter\bin\Release\net8.0-windows\win-x64\publish"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$releasePath = Join-Path $repoRoot ("Release\ArduinoFFBControlCenter-win-x64-" + $stamp)
$zipPath = Join-Path $repoRoot ("Release\ArduinoFFBControlCenter-win-x64-" + $stamp + ".zip")

Write-Host "Restoring..."
dotnet restore $projectPath

Write-Host "Building..."
dotnet build $projectPath -c Release

if (-not $SkipTests) {
    Write-Host "Running tests..."
    dotnet test $solutionPath -c Release --no-build
}

Write-Host "Publishing self-contained win-x64 build..."
dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    -p:SelfContained=true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false

Write-Host "Copying release payload..."
New-Item -ItemType Directory -Path $releasePath -Force | Out-Null
Copy-Item (Join-Path $publishPath "*") $releasePath -Recurse -Force

$launcherPath = Join-Path $releasePath "Run-ControlCenter.cmd"
@'
@echo off
setlocal
cd /d "%~dp0"
start "" "ArduinoFFBControlCenter.exe"
'@ | Set-Content $launcherPath -Encoding ASCII -NoNewline

Write-Host "Zipping release..."
Compress-Archive -Path (Join-Path $releasePath "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force

Write-Host "Done."
Write-Host "EXE: $releasePath\ArduinoFFBControlCenter.exe"
Write-Host "ZIP: $zipPath"
