# Arduino FFB Control Center (Windows)

This folder contains the Windows app for DIY FFB wheels based on:
- Arduino Leonardo (ATmega32U4)
- ranenbg/Arduino-FFB-wheel firmware workflow (precompiled HEX)
- BTS7960 motor driver
- wheel encoder + optional pedals

## 1) What This App Does
The app is an end-to-end control center that handles:
- firmware flashing (Leonardo-safe bootloader flow)
- serial configuration + EEPROM save (when firmware supports it)
- calibration wizard + verification
- setup wizard with wiring validation
- profiles + timeline snapshots + rollback
- live telemetry + hardware mirror dashboard
- local phone dashboard (LAN)
- local Ollama AI side-view (optional)

## UI Refresh (React-like Material style)
The app now uses a redesigned visual system with:
- brighter modern palette (Soft Dark + optional Light mode)
- elevated cards and polished button states
- floating-label form fields for setup/firmware/settings flows
- themed dropdown popups (no default white combo popup)
- commit-style timeline rows
- Home dashboard with health meters + phone dashboard QR preview

UI review checklist:
- ComboBox popup is themed and readable in both themes
- Focus ring and hover states are visible on all form controls
- Setup Wizard pin mapping uses floating form controls
- Home shows device card, health meters, recent timeline, and phone dashboard card
- No default WPF gray controls should remain in key workflows

## 2) Core Firmware Rules (Important)
This app follows Arduino-FFB-wheel assumptions:
- normal users flash precompiled `.hex` files
- firmware features are encoded in version option letters
- serial commands and capabilities vary by build

The UI is capability-driven:
- if firmware supports a feature -> control is enabled
- if not -> control is shown disabled with explanation

## 3) App Modes
- Beginner Mode: safer defaults and easier flow
- Advanced Mode: full controls
- Demo Mode: no hardware required, synthetic data

Note: Kid mode was removed from the visible UX and forced off in runtime.

## 3.1) Arduino CLI (needed for "Send as .ino" mode)
If you enable custom pinout build mode, the app compiles firmware from source using `arduino-cli`.

Official docs:
- Installation guide: https://arduino.github.io/arduino-cli/latest/installation/
- Releases: https://github.com/arduino/arduino-cli/releases

Quick Windows install (PowerShell):
```powershell
winget install arduino.arduino-cli
```

Seamless behavior in app:
- if `Send as .ino` is enabled and `arduino-cli` is missing, the app auto-downloads the latest Windows x64 Arduino CLI release into:
  - `%AppData%/ArduinoFFBControlCenter/tools/arduino-cli/`
- then it auto-runs:
  - `arduino-cli core update-index`
  - `arduino-cli core install arduino:avr`
- so the user usually does not need to install Arduino CLI manually.

Then verify:
```powershell
arduino-cli version
arduino-cli core update-index
arduino-cli core install arduino:avr
```

## 4) Main User Flow
1. Open Setup Wizard
2. Confirm wiring (BTS7960 + encoder + pedals)
3. Detect device + choose HEX (default is `Recommended - BTS7960 Default Stable`)
4. Flash firmware
5. Calibrate (center, direction, rotation)
6. Apply baseline preset
7. Save to wheel (EEPROM) or PC fallback

## 4.1) Default HEX + Custom HEX behavior
- The app now includes one primary default firmware:
  - `Assets/FirmwareLibrary/Leonardo/recommended/wheel_default_recommended.hex`
- This is auto-selected in Setup Wizard and Factory Restore for fastest "just works" setup.
- If pinout differs from default, the app can still build a custom pinout HEX (`Send as .ino` mode) and flash that instead.

## 5) Firmware Flashing Logic
Implemented in `Services/FirmwareFlasherService.cs`:
- checks avrdude assets exist
- checks selected COM exists and is not busy
- triggers Leonardo reset (open/close at 1200 baud)
- detects temporary bootloader COM port
- runs avrdude flash + verify
- retries once for transient failures
- maps common avrdude failures to friendly hints

Extra actions:
- Reset Board button (bootloader reset without flashing)
- Manual Recovery flow
- Last Known Good rollback

## 6) Calibration Logic
Implemented in `Services/CalibrationService.cs` and wizard pages:
- detect if calibration is needed
- center capture at rest
- direction validation
- rotation apply + endpoint checks
- save + verify
- status shown on Home (`Calibrated / Not calibrated`)

## 7) Settings Persistence Logic
Implemented in:
- `Services/SettingsService.cs` (JSON persistence)
- `Services/DeviceSettingsService.cs` (wheel vs PC save path)
- `Services/SettingsPersistenceTracker.cs` (state machine)

Rules:
- Apply = temporary runtime write
- Save to Wheel = EEPROM write (if supported)
- If EEPROM unavailable = save profile locally
- On reconnect, app can sync from wheel and resolve profile conflicts

## 8) Telemetry + Testing Dashboard
Telemetry page includes:
- time series plots (angle, torque, velocity, loop dt)
- clipping/oscillation/noise heuristics
- hardware mirror card with:
  - live wheel angle visual
  - live throttle/brake/clutch bars

Files:
- `ViewModels/TelemetryViewModel.cs`
- `Views/TelemetryView.xaml`

## 9) Phone Dashboard (Local LAN)
Implemented in `Services/DashboardHostService.cs`.
- default `http://<pc-ip>:10500`
- LAN-only behavior
- optional PIN
- safe control subset from phone
- no firmware flashing via phone

## 10) Ollama AI Side View
Implemented with:
- `Services/OllamaService.cs`
- `Services/ScreenCaptureService.cs`
- `ViewModels/OllamaViewModel.cs`
- `Views/OllamaView.xaml`

Features:
- local model list from Ollama
- ask questions from inside app
- optional full-screen screenshot context
- fallback to text-only if selected model rejects images
- global AI sidebar available on every page (can be disabled in Settings)
- local action routing: AI can apply Setup pin mappings and FFB tuning values from prompts
- setup wizard includes AI provider choice (`Ollama` or `ApiKey`)
- after setup, provider/key changes are managed from `Settings` page

Setup:
1. install Ollama
2. run `ollama serve`
3. pull a model (`ollama pull llava:latest` recommended for image context)
4. app -> AI Side View -> Refresh Models -> select model -> Ask

## 11) Project Structure
- `ArduinoFFBControlCenter/` main WPF app
- `ArduinoFFBControlCenter.Tests/` unit tests
- `Installer/` MSIX scripts and manifest

Inside app project:
- `Models/` DTOs/state objects
- `Services/` hardware/protocol/IO logic
- `ViewModels/` page behavior (MVVM)
- `Views/` XAML screens
- `Helpers/` converters and small utilities

## 12) Important Files (Reader Guide)
If you are new, read these first:
- `ViewModels/MainViewModel.cs`
- `Services/FirmwareFlasherService.cs`
- `Services/DeviceProtocolService.cs`
- `Services/DeviceSettingsService.cs`
- `ViewModels/SetupWizardViewModel.cs`
- `ViewModels/TelemetryViewModel.cs`
- `Services/OllamaService.cs`
- `Services/ScreenCaptureService.cs`

These files now include additional inline comments to make logic easier to follow.

## 13) Build and Test
From repo root:

```powershell
# build app + tests
dotnet build ControlCenter/ArduinoFFBControlCenter.sln -c Release

# run tests
dotnet test ControlCenter/ArduinoFFBControlCenter.sln -c Release --no-build
```

## 14) Publish EXE
Safe default (recommended):

```powershell
dotnet publish ControlCenter/ArduinoFFBControlCenter/ArduinoFFBControlCenter.csproj -c Release -r win-x64 --self-contained true
```

Single-file publish:

```powershell
dotnet publish ControlCenter/ArduinoFFBControlCenter/ArduinoFFBControlCenter.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

Output path:
- `ControlCenter/ArduinoFFBControlCenter/bin/Release/net8.0-windows/win-x64/publish/`

## 15) Installer (MSIX)

```powershell
ControlCenter/Installer/build-msix.ps1
```

Then install using:

```powershell
ControlCenter/Installer/Install.ps1
```

## 16) Where Data Is Stored
Everything persists under:
- `%AppData%/ArduinoFFBControlCenter/`

Includes:
- app settings
- wizard state
- profiles
- snapshots
- logs
- support bundles

## 17) Troubleshooting
### App does not open
- check `%TEMP%/ArduinoFFBControlCenter-crash.log`
- update to latest repo (startup issues were fixed)

### Flash fails / bootloader not detected
- close IDE, serial monitor, legacy GUI
- try Reset Board
- then Manual Recovery and press reset when prompted

### No telemetry
- ensure device supports telemetry/capability
- ensure serial is connected
- check if firmware build has expected options

### Ollama not responding
- start service: `ollama serve`
- verify endpoint `http://localhost:11434`
- run `ollama list` and ensure a model exists

## 18) Cleanup of Non-Needed Local Files
To keep workspace clean, generated build folders are safe to remove:
- `ControlCenter/ArduinoFFBControlCenter/bin`
- `ControlCenter/ArduinoFFBControlCenter/obj`
- `ControlCenter/ArduinoFFBControlCenter.Tests/bin`
- `ControlCenter/ArduinoFFBControlCenter.Tests/obj`

These are ignored by `.gitignore` and recreated automatically on build.

## 19) Credits
- Original firmware: Milos Rankovic (ranenbg)
- Original repo: https://github.com/ranenbg/Arduino-FFB-wheel
- Legacy GUI: https://github.com/ranenbg/Arduino-FFB-gui
- Windows Control Center and integration work: built with **OpenAI Codex**
