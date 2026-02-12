param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Publisher = "CN=ArduinoFFBControlCenter",
    [string]$IdentityName = "ArduinoFFBControlCenter",
    [string]$DisplayName = "Arduino FFB Control Center",
    [string]$PublisherDisplayName = "Arduino FFB Control Center",
    [string]$Version,
    [string]$CertPassword = "ArduinoFFBControlCenter",
    [switch]$SkipPublish,
    [switch]$SkipSign
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Find-WinSdkTool {
    param([string]$ToolName)
    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\\10\\bin"
    if (Test-Path $kitsRoot) {
        $verDir = Get-ChildItem $kitsRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if ($verDir) {
            $candidate = Join-Path $verDir.FullName "x64\\$ToolName"
            if (Test-Path $candidate) { return $candidate }
        }
        $fallback = Join-Path $kitsRoot "x64\\$ToolName"
        if (Test-Path $fallback) { return $fallback }
    }
    return $null
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "ArduinoFFBControlCenter\\ArduinoFFBControlCenter.csproj"

if (-not $Version) {
    $csproj = Get-Content $project -Raw
    $match = [regex]::Match($csproj, "<Version>([^<]+)</Version>")
    $Version = if ($match.Success) { $match.Groups[1].Value } else { "1.0.0.0" }
}

$publishDir = Join-Path $root "ArduinoFFBControlCenter\\bin\\$Configuration\\net8.0-windows\\$Runtime\\publish"
if (-not $SkipPublish) {
    dotnet publish $project -c $Configuration -r $Runtime /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
}

if (-not (Test-Path $publishDir)) {
    throw "Publish output not found: $publishDir"
}

$packageRoot = Join-Path $PSScriptRoot "PackageRoot"
if (Test-Path $packageRoot) {
    Remove-Item -Recurse -Force $packageRoot
}
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

Copy-Item (Join-Path $publishDir "*") -Destination $packageRoot -Recurse -Force

$assetsSrc = Join-Path $PSScriptRoot "Assets"
$assetsDest = Join-Path $packageRoot "Assets"
Copy-Item $assetsSrc -Destination $assetsDest -Recurse -Force

$manifestTemplate = Get-Content (Join-Path $PSScriptRoot "AppxManifest.xml") -Raw
$manifest = $manifestTemplate
$manifest = $manifest.Replace("{{IdentityName}}", $IdentityName)
$manifest = $manifest.Replace("{{Publisher}}", $Publisher)
$manifest = $manifest.Replace("{{Version}}", $Version)
$manifest = $manifest.Replace("{{DisplayName}}", $DisplayName)
$manifest = $manifest.Replace("{{PublisherDisplayName}}", $PublisherDisplayName)
Set-Content -Path (Join-Path $packageRoot "AppxManifest.xml") -Value $manifest -Encoding UTF8

$makeappx = Find-WinSdkTool "makeappx.exe"
if (-not $makeappx) {
    throw "makeappx.exe not found. Install Windows 10/11 SDK."
}

$outputDir = Join-Path $PSScriptRoot "output"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$msixPath = Join-Path $outputDir "ArduinoFFBControlCenter.msix"

& $makeappx pack /d $packageRoot /p $msixPath /o | Out-Null

if (-not $SkipSign) {
    $signtool = Find-WinSdkTool "signtool.exe"
    if (-not $signtool) {
        throw "signtool.exe not found. Install Windows 10/11 SDK."
    }

    $certDir = Join-Path $PSScriptRoot "cert"
    New-Item -ItemType Directory -Force -Path $certDir | Out-Null
    $pfxPath = Join-Path $certDir "ArduinoFFBControlCenter.pfx"
    $cerPath = Join-Path $certDir "ArduinoFFBControlCenter.cer"

    if (-not (Test-Path $pfxPath)) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature -KeyExportPolicy Exportable -CertStoreLocation "Cert:\\CurrentUser\\My" -NotAfter (Get-Date).AddYears(5)
        Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password (ConvertTo-SecureString $CertPassword -AsPlainText -Force) | Out-Null
        Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
    }

    & $signtool sign /fd SHA256 /a /f $pfxPath /p $CertPassword $msixPath | Out-Null
}

Write-Host "MSIX created at: $msixPath"
