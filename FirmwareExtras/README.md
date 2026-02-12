# Firmware Extras (Optional)

The Control Center works with stock firmware using the existing RS232 commands (`V`, `U`, `FG`, `FD`, `E`, etc.).

This folder contains **optional** patches that add a minimal `INFO` command for richer device capability detection.

## Patch: `cc-info-command.patch`
Adds:
- `USE_CC_PROTOCOL` compile flag
- New `I` command that outputs:
  - firmware version string with option letters
  - **capabilities bitmask** (matches Control Center flags)
  - a condensed config summary (same order as `U` output)
  - calibration fields (present flag, center offset, invert placeholder, rotation)

Calibration set uses existing commands:
- `C` (center)
- `G <deg>` (rotation)
- `A` (save to EEPROM)

This is backward compatible and only active if `USE_CC_PROTOCOL` is defined.

## Build HEX (Leonardo)
You still must follow the project’s existing build assumptions:
- Arduino IDE 1.8.19
- AVR Boards 1.6.21
- Modified core from this repo

A helper script is provided below. It assumes you have `arduino-cli` installed and the modified core in place.

```
./build_hex.ps1
```

Output HEX files can be placed in `FirmwareExtras/hex/` and then copied into the app’s Firmware Library.

## Telemetry
For telemetry, the app uses:
- HID for angle
- Serial FFB monitor (effstate bit4) for torque

This requires **no firmware changes**.
