param(
  [string]$SketchPath = "brWheel_my",
  [string]$Fqbn = "arduino:avr:leonardo"
)

# Requires:
# - arduino-cli installed and on PATH
# - Arduino AVR core installed
# - modified core from this repo already applied (see main README)

arduino-cli core update-index
arduino-cli core install arduino:avr

# Build with optional Control Center protocol flag
arduino-cli compile --fqbn $Fqbn $SketchPath -e --build-property compiler.cpp.extra_flags="-DUSE_CC_PROTOCOL"

# Copy the HEX from build output to FirmwareExtras/hex
$buildDir = Join-Path $SketchPath "build"
$hex = Get-ChildItem -Path $buildDir -Filter "*.hex" -Recurse | Select-Object -First 1
if ($hex) {
  Copy-Item $hex.FullName -Destination "FirmwareExtras\hex" -Force
  Write-Host "HEX exported to FirmwareExtras\\hex"
}
