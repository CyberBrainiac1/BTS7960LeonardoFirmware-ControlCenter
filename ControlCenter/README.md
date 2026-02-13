# Wheel Control Center (Windows)

A Windows-only Control Center for DIY FFB wheels running **ranenbg/Arduino-FFB-wheel** firmware on **Arduino Leonardo (ATmega32U4)** with BTS7960 motor drivers.

## Key Assumptions (per firmware workflow)
- Firmware is flashed from **precompiled HEX** files because flash is limited and option letters are baked into the build.
- Configuration is through **CDC serial** (RS232 commands). The legacy GUI (`wheel_control.exe`) remains compatible.
- Optional: a minimal `INFO` command (firmware patch in `FirmwareExtras/`) enables richer capability detection.

## Beginner Mode (default)
- Safety capped strength with **hold-to-unlock** for high torque.
- Setup Wizard (detect → flash → calibrate → preset → save).
- Simplified sliders with explanations.
- Calibration status and save status on Home.

## Advanced Mode
- Toggle **Advanced Mode** in the top bar (Beginner off).
- Full effect scaling controls.
- Curve editor (stored in profiles; apply requires firmware support).
- Lab Tools: built-in effect tester + input tester.

## Setup Wizard (Wiring + Pedals)
The wizard now collects wiring info to reduce mistakes:
- Arduino pin mapping for BTS7960 and encoder
- BTS7960 wiring confirmation (PWM mode, logic voltage, common ground)
- Pedal wiring (throttle/brake/clutch) if present

The wiring summary is saved and reused in Diagnostics and Self-Test.

## Pedals
The **Pedals** page lets you:
- Map HID axes to throttle/brake/clutch
- Auto-detect an axis by moving one pedal
- Calibrate min/max and invert if needed
- See live pedal input

If your firmware does not expose pedal axes, the page will show N/A until supported.

## Demo Mode
Toggle **Demo Mode** in the top bar to explore the UI without hardware.

## AI Side View (Ollama)
The app includes an **AI Side View** page for local AI help and screen-aware Q&A.

Setup:
1. Install Ollama and start it: `ollama serve`
2. Pull a model (vision model recommended for screen context), for example:
   - `ollama pull llava:latest`
3. In the app, open **AI Side View**
4. Set endpoint (default `http://localhost:11434`), refresh models, choose a model
5. Ask questions; enable **Include current screen** to send a screenshot with the prompt

Notes:
- If the selected model does not support images, the app falls back to text-only.
- Everything stays local to your machine (no cloud dependency required).

## Phone Dashboard (LAN)
Enable **Phone Dashboard** in the app to serve a local mobile dashboard on your Wi-Fi:
- Open **Phone Dashboard** page → Enable → set port/PIN → Apply.
- Use the shown URL (e.g., `http://<PC-LAN-IP>:10500`) or QR code on your phone (same Wi-Fi).
- Windows Firewall may prompt on first start (allow private networks).
- No internet connection is required; it is hosted locally by the app.
- Dashboard is **LAN-only** and uses a PIN by default.
- Safe controls only: strength/damping/friction/inertia and profile apply.
- If calibration is required, controls are locked unless **Advanced Remote** is enabled.
- Layout editor lets you rearrange widgets and export/import layouts.

## Snapshots (Timeline + Revert)
The app creates snapshots on:
- Flash success
- Save-to-wheel
- Profile apply
- Calibration save
- Self-test

Each snapshot includes a `snapshot.json` and optional telemetry/logs. You can compare snapshots and revert settings/firmware.

## Profile Gallery (.wheelprofile)
- Export/import shareable `.wheelprofile` bundles (manifest + settings + dashboard layout).
- Optional firmware hex in the bundle.
- Import warnings and diff preview before applying.

## Self-Test / Pre-Flight
Run a one-button self-test to check encoder direction and buttons. A report is stored in snapshots.

## Custom Firmware (Advanced)
If you need non-standard pin mappings, use **Build Custom Firmware** in the Setup Wizard.
This uses Arduino CLI (advanced) and requires the firmware source to be available in the app folder.

## Install
### Recommended (MSIX, per-user, no admin)
1. Build the MSIX with `ControlCenter/Installer/build-msix.ps1` or download the release MSIX.
2. If the package is signed with the included self-signed cert, run `ControlCenter/Installer/Install.ps1` (installs cert to CurrentUser and installs the MSIX).
3. Launch **Arduino FFB Control Center** from the Start Menu.

### Portable EXE (no install)
1. Build or download the app release ZIP and extract it anywhere.
2. Run `ArduinoFFBControlCenter.exe`.

### Required assets
- Ensure **avrdude** is present in `Assets/Tools/avrdude` (see README.txt in that folder).
- Optional: place `wheel_control.exe` into `Assets/LegacyGUI` to enable the "Open Legacy GUI" button.

## Firmware Library
The app ships with the Leonardo HEX files from:
- `brWheel_my/leonardo hex`

To add custom HEX:
- Copy your `.hex` into `Assets/FirmwareLibrary/Leonardo` (any subfolder is ok).
- Click **Reload Library** in the Firmware page.

## Flashing
- Select the correct HEX (options letters are part of the version string).
- Select the COM port.
- Click **Flash Selected**. The app will try the 1200-baud bootloader reset and then run `avrdude`.
- Use **Reset Board** to force Leonardo bootloader reset without flashing.
- If the bootloader isn’t detected, use **Manual Recovery** (press reset twice quickly, then flash).
- Rollback is available using **Last Known Good**.

## Calibration (When Needed)
The app automatically checks whether calibration is required:
- If firmware reports calibration (via INFO), it uses those values.
- If not, it infers from HID axis center at rest (~2 seconds).
- If center is off or rotation looks invalid, it flags **Not calibrated**.

Use the **Calibration** page or Setup Wizard to:
- Capture center (hands off)
- Verify direction
- Set rotation and verify endpoints
- Save to EEPROM (if supported) or save to PC (if not)

## Saving (Wheel vs PC)
- **Apply** updates the wheel immediately (temporary).
- **Save to Wheel** writes settings to EEPROM when supported.
- If EEPROM is unavailable, settings are stored to a local profile and marked **Saved to PC**.
- On connect, the app prefers **Wheel settings** and prompts if the last profile differs.
- A backup snapshot is saved before every EEPROM write; use **Restore Previous** on the FFB page.
- Toggle **Auto-apply last profile on connect** in Profiles to re-apply your last tune.

## Recovery (Bootloader)
1. Unplug the wheel.
2. Hold reset, plug USB, then release (or double-press reset).
3. Use **Manual Recovery** and select the bootloader COM port.

## BTS7960 PWM Note
Release notes and community guidance recommend:
- **Fast PWM**
- **PWM+-**
- **~8kHz** (7.8kHz) for H-bridge drivers like BTS7960

These are the typical defaults in the firmware for BTS7960 builds.

## Data Storage
Profiles, logs, and settings are stored at:
```
%AppData%/ArduinoFFBControlCenter/
```

## Troubleshooting
- If flash fails, verify the port, close other serial tools, and check that `avrdude.exe` + `avrdude.conf` exist.
- If telemetry is flat, enable FFB monitor by clicking **Telemetry** and making sure the device is connected.
- If the device isn’t detected, manually select the COM port and click **Connect**.
- For bootloader issues, double-press reset and use **Manual Recovery**.

## Build (Developer)
```
dotnet restore ControlCenter/ArduinoFFBControlCenter/ArduinoFFBControlCenter.csproj

# Release publish

dotnet publish ControlCenter/ArduinoFFBControlCenter/ArduinoFFBControlCenter.csproj -c Release -r win-x64 \
  /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

### MSIX packaging (Developer)
```
ControlCenter/Installer/build-msix.ps1
```
Outputs `ControlCenter/Installer/output/ArduinoFFBControlCenter.msix` and a self-signed cert in `ControlCenter/Installer/cert/`.
