# Arduino FFB Wheel + Control Center (Leonardo + BTS7960)

Fork of Arduino-FFB-wheel with a full Windows Control Center app.

## Original Author Credit
- Original firmware project: **Milos Rankovic (ranenbg)**
- Original repository: https://github.com/ranenbg/Arduino-FFB-wheel
- Legacy GUI: https://github.com/ranenbg/Arduino-FFB-gui

## What This Fork Adds
- Windows WPF Control Center (`ControlCenter/`)
- hardened Leonardo flashing + reset + rollback
- setup wizard with wiring validation (BTS7960 + encoder + pedals)
- calibration workflow + persistence tracking
- telemetry + testing dashboard mirror
- phone dashboard (LAN)
- local Ollama AI side-view
- profile gallery + snapshots/timeline + revert

## Full Documentation
- App and architecture docs: `ControlCenter/README.md`
- Optional firmware protocol extras: `FirmwareExtras/`

## Quick Start
1. Open `ControlCenter/Installer/Install-ControlCenter.cmd` (or build and run portable publish).
2. Connect Leonardo over USB.
3. Run Setup Wizard end-to-end.

## Build/Test/Publish
```powershell
# from repo root
dotnet build ControlCenter/ArduinoFFBControlCenter.sln -c Release
dotnet test ControlCenter/ArduinoFFBControlCenter.sln -c Release --no-build

# publish single-file exe
dotnet publish ControlCenter/ArduinoFFBControlCenter/ArduinoFFBControlCenter.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

Output:
- `ControlCenter/ArduinoFFBControlCenter/bin/Release/net8.0-windows/win-x64/publish/ArduinoFFBControlCenter.exe`

## Firmware Workflow Reminder
This project follows the original workflow:
- flash precompiled HEX for end users
- option letters in firmware version determine available features
- serial configuration depends on firmware capabilities

## Local Cleanup (non-needed generated files)
If you want a clean workspace, remove generated build outputs:
```powershell
cmd /c "if exist ControlCenter\ArduinoFFBControlCenter\bin rmdir /s /q ControlCenter\ArduinoFFBControlCenter\bin & if exist ControlCenter\ArduinoFFBControlCenter\obj rmdir /s /q ControlCenter\ArduinoFFBControlCenter\obj & if exist ControlCenter\ArduinoFFBControlCenter.Tests\bin rmdir /s /q ControlCenter\ArduinoFFBControlCenter.Tests\bin & if exist ControlCenter\ArduinoFFBControlCenter.Tests\obj rmdir /s /q ControlCenter\ArduinoFFBControlCenter.Tests\obj"
```

These folders are ignored by git and regenerated automatically.

## Important Note
Please keep attribution to original firmware authors in downstream forks.
