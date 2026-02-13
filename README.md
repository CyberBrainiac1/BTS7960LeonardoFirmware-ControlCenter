# Arduino FFB Wheel + Control Center (Leonardo + BTS7960)

A polished Windows control app and firmware workflow for DIY force-feedback wheels based on **Arduino Leonardo (ATmega32U4)** and **BTS7960**.

## Original Author Credit
- Original firmware project: **Milos Rankovic (ranenbg)**
- Original repository: https://github.com/ranenbg/Arduino-FFB-wheel
- Legacy GUI: https://github.com/ranenbg/Arduino-FFB-gui

This repo is a fork/extension of that work and keeps compatibility with the original precompiled HEX workflow.

## What Changed In This Repo
Compared to the original Arduino-FFB-wheel repo, this repo adds:
- `ControlCenter/` Windows app (`.NET 8 WPF`, MVVM)
- hardened Leonardo flashing flow (1200-baud reset, bootloader detection, retry/recovery)
- guided setup wizard for BTS7960 + motor encoder + optional pedals wiring
- calibration manager + save/reload logic (wheel EEPROM when supported, PC fallback when not)
- snapshots/timeline + revert
- profile gallery with export/import
- phone dashboard host (local LAN URL + QR)
- local Ollama AI side-view (chat + optional screen capture context)
- firmware protocol extras in `FirmwareExtras/` (optional, capability-gated)

## Repository
- GitHub: https://github.com/CyberBrainiac1/BTS7960LeonardoFirmware-ControlCenter

## Quick Start (End Users)
1. Open `ControlCenter/Installer/Install-ControlCenter.cmd`.
2. Launch **Arduino FFB Control Center**.
3. Connect Leonardo via USB.
4. Run Setup Wizard:
- Detect device
- Flash recommended HEX
- Confirm BTS7960 wiring
- Calibrate center/direction/rotation
- Save settings

If you prefer portable mode, run `Run-ControlCenter.cmd`.

## Firmware Workflow (Important)
This project follows original Arduino-FFB-wheel assumptions:
- **Flash precompiled HEX** (do not expect normal users to compile `.ino`)
- firmware options are encoded as letter suffixes in the version string
- choose HEX variant that matches your enabled features/wiring

HEX folders:
- `brWheel_my/leonardo hex/`
- `brWheel_my/promicro hex/`

## Wiring Notes (BTS7960 + Encoder + Pedals)
- Recommended motor mode for BTS7960: **Fast PWM**, **PWM+-**, ~**8kHz**
- Use a **common ground** between Arduino and motor driver power system
- Ensure encoder A/B are on expected interrupt-capable pins (firmware dependent)
- Pedals are supported through analog inputs or configured firmware options

Wiring images are included in:
- `brWheel_my/wirings/`
- root images used by setup screens:
- `arduinoLeoPinout.jpg`
- `BTS7960-Pinout.jpg`
- `bts7960_wiring_diagram.png`

## Control Center Build (Developer)
Prerequisite: install **.NET SDK 8**

```powershell
cd ControlCenter/ArduinoFFBControlCenter
dotnet restore
dotnet build -c Release
```

### Publish EXE (safe default, multi-file)
```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```
Output:
- `ControlCenter/ArduinoFFBControlCenter/bin/Release/net8.0-windows/win-x64/publish/`

### Publish single-file EXE (optional)
```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

## Known Guardrails
- Capability-driven UI: unsupported firmware features are shown but disabled with explanation.
- App keeps serial usage short so HID/FFB operation is not blocked.
- Flashing never runs if port is busy.

## Troubleshooting
### `dotnet` command not found
Install .NET SDK 8:
- https://dotnet.microsoft.com/download/dotnet/8.0

### WPF `Spacing` XAML compile error
This project now avoids unsupported WPF `Spacing` properties. Pull latest repo and rebuild.

### App exits on launch
Check crash log:
- `%TEMP%/ArduinoFFBControlCenter-crash.log`

### Flashing fails on Leonardo
- Close Arduino IDE / serial monitors / legacy GUI.
- Use **Reset Board** from Firmware page to force bootloader reset.
- Retry with manual recovery mode and press RESET when prompted.

### Ollama side view does not connect
- Start Ollama first: `ollama serve`
- Verify endpoint in app: `http://localhost:11434`
- Pull at least one model (vision-capable model recommended for screen Q&A)

## License / Credits
Please retain attribution to original firmware authors in downstream forks.
