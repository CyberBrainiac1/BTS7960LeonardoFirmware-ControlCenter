param(
    [string]$MsixPath,
    [string]$CerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Resolve-Path $PSScriptRoot

if (-not $MsixPath) {
    $MsixPath = Join-Path $root "output\\ArduinoFFBControlCenter.msix"
}
if (-not (Test-Path $MsixPath)) {
    throw "MSIX not found: $MsixPath"
}

if (-not $CerPath) {
    $CerPath = Join-Path $root "cert\\ArduinoFFBControlCenter.cer"
}

if (Test-Path $CerPath) {
    Write-Host "Installing certificate (CurrentUser\\TrustedPeople)..."
    Import-Certificate -FilePath $CerPath -CertStoreLocation "Cert:\\CurrentUser\\TrustedPeople" | Out-Null
}

Write-Host "Installing MSIX (per-user)..."
Add-AppxPackage -Path $MsixPath

Write-Host "Done."
