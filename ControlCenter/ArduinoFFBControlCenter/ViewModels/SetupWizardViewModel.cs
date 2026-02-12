using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class SetupWizardViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly DeviceManagerService _deviceManager;
    private readonly FirmwareFlasherService _flasher;
    private readonly FirmwareLibraryService _library;
    private readonly ProfileService _profiles;
    private readonly DeviceStateService _deviceState;
    private readonly TuningStateService _tuningState;
    private readonly CalibrationService _calibration;
    private readonly DeviceSettingsService _settings;
    private readonly WizardStateService _wizardStateService;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _appSettings;
    private readonly CustomFirmwareBuilderService _builder;
    private readonly SnapshotService _snapshots;
    private WizardState _state;

    public ObservableCollection<string> Ports { get; } = new();
    public ObservableCollection<FirmwareHexInfo> FirmwareOptions { get; } = new();
    public ObservableCollection<PresetProfile> Presets { get; } = new();

    public ObservableCollection<string> DigitalPins { get; } = new();
    public ObservableCollection<string> PwmPins { get; } = new();
    public ObservableCollection<string> InterruptPins { get; } = new();
    public ObservableCollection<string> AnalogPins { get; } = new();
    public ObservableCollection<string> PwmModes { get; } = new();
    public ObservableCollection<string> LogicVoltages { get; } = new();
    public ObservableCollection<string> MotorTerminals { get; } = new();

    public ObservableCollection<string> WiringWarnings { get; } = new();

    [ObservableProperty] private int stepIndex;
    [ObservableProperty] private string wizardStatus = "Step 1 of 8";
    [ObservableProperty] private string wizardHint = "Start with wiring so the app can guide you.";

    [ObservableProperty] private string? selectedPort;
    [ObservableProperty] private FirmwareHexInfo? selectedFirmware;
    [ObservableProperty] private PresetProfile? selectedPreset;
    [ObservableProperty] private string flashLog = string.Empty;
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string? rpwmPin;
    [ObservableProperty] private string? lpwmPin;
    [ObservableProperty] private string? rEnPin;
    [ObservableProperty] private string? lEnPin;
    [ObservableProperty] private bool useEnablePins = true;
    [ObservableProperty] private string? encoderAPin;
    [ObservableProperty] private string? encoderBPin;
    [ObservableProperty] private string? eStopPin;
    [ObservableProperty] private string? button1Pin;
    [ObservableProperty] private string? button2Pin;
    [ObservableProperty] private string? shifterXPin;
    [ObservableProperty] private string? shifterYPin;

    [ObservableProperty] private bool hasPedals = true;
    [ObservableProperty] private string? throttlePin;
    [ObservableProperty] private string? brakePin;
    [ObservableProperty] private string? clutchPin;

    [ObservableProperty] private string pwmMode = "PWM+-";
    [ObservableProperty] private string logicVoltage = "5V";
    [ObservableProperty] private bool commonGround = true;
    [ObservableProperty] private string motorPlusTerminal = "M+";
    [ObservableProperty] private string motorMinusTerminal = "M-";
    [ObservableProperty] private bool logicVccConnected = true;
    [ObservableProperty] private bool logicGndConnected = true;

    [ObservableProperty] private string wiringSummary = string.Empty;
    [ObservableProperty] private bool wiringValid = true;

    [ObservableProperty] private bool useCustomBuild;
    [ObservableProperty] private string customBuildStatus = "Custom build disabled";

    public SetupWizardViewModel(LoggerService logger,
        DeviceManagerService deviceManager,
        FirmwareFlasherService flasher,
        FirmwareLibraryService library,
        ProfileService profiles,
        DeviceStateService deviceState,
        TuningStateService tuningState,
        CalibrationService calibration,
        DeviceSettingsService settings,
        WizardStateService wizardStateService,
        CustomFirmwareBuilderService builder,
        SettingsService settingsService,
        AppSettings appSettings,
        SnapshotService snapshots)
    {
        _logger = logger;
        _deviceManager = deviceManager;
        _flasher = flasher;
        _library = library;
        _profiles = profiles;
        _deviceState = deviceState;
        _tuningState = tuningState;
        _calibration = calibration;
        _settings = settings;
        _wizardStateService = wizardStateService;
        _builder = builder;
        _settingsService = settingsService;
        _appSettings = appSettings;
        _snapshots = snapshots;

        LoadPinOptions();
        LoadOptions();
        LoadWiringOptions();

        _state = _wizardStateService.Load();
        ApplyState(_state);

        UpdateStepText();
        UpdateWiringSummary();
    }

    [RelayCommand]
    private async Task BuildCustomFirmwareAsync()
    {
        if (!UseCustomBuild)
        {
            CustomBuildStatus = "Enable custom build first.";
            return;
        }

        IsBusy = true;
        CustomBuildStatus = "Building custom firmware...";
        var wiring = BuildWiringConfig();
        var result = await _builder.BuildAsync(wiring, CancellationToken.None);
        IsBusy = false;

        CustomBuildStatus = result.Message;
        if (result.Success && result.HexPath != null)
        {
            var custom = new FirmwareHexInfo
            {
                Name = $"Custom {DateTime.Now:HHmm}",
                Path = result.HexPath,
                Notes = "Custom build"
            };
            FirmwareOptions.Insert(0, custom);
            SelectedFirmware = custom;
        }
    }

    private void LoadPinOptions()
    {
        DigitalPins.Clear();
        foreach (var pin in LeonardoPinCatalog.GetAllPins())
        {
            DigitalPins.Add(pin.Name);
        }

        PwmPins.Clear();
        foreach (var pin in LeonardoPinCatalog.GetPwmPins())
        {
            PwmPins.Add(pin.Name);
        }

        InterruptPins.Clear();
        foreach (var pin in LeonardoPinCatalog.GetInterruptPins())
        {
            InterruptPins.Add(pin.Name);
        }

        AnalogPins.Clear();
        foreach (var pin in LeonardoPinCatalog.GetAnalogPins())
        {
            AnalogPins.Add(pin.Name);
        }
    }

    private void LoadOptions()
    {
        Ports.Clear();
        foreach (var port in _deviceManager.ScanPorts())
        {
            Ports.Add(port);
        }

        FirmwareOptions.Clear();
        foreach (var hex in _library.LoadLibrary())
        {
            FirmwareOptions.Add(hex);
        }

        Presets.Clear();
        foreach (var p in PresetLibraryService.GetWheelPresets())
        {
            Presets.Add(p);
        }

        SelectedFirmware = FirmwareOptions.FirstOrDefault(f => f.Name.Contains("v250", StringComparison.OrdinalIgnoreCase)) ?? FirmwareOptions.FirstOrDefault();
        SelectedPreset = Presets.FirstOrDefault();
    }

    private void LoadWiringOptions()
    {
        PwmModes.Clear();
        PwmModes.Add("PWM+-");
        PwmModes.Add("PWM+DIR");

        LogicVoltages.Clear();
        LogicVoltages.Add("5V");
        LogicVoltages.Add("3.3V");

        MotorTerminals.Clear();
        MotorTerminals.Add("M+");
        MotorTerminals.Add("M-");
    }

    private void ApplyState(WizardState state)
    {
        StepIndex = Math.Clamp(state.LastStep, 0, 7);
        SelectedPort = state.SelectedPort;
        SelectedFirmware = FirmwareOptions.FirstOrDefault(f => f.Path == state.SelectedFirmwarePath) ?? SelectedFirmware;
        UseCustomBuild = state.UseCustomBuild;

        var w = state.Wiring ?? new WiringConfig();
        RpwmPin = w.RpwmPin;
        LpwmPin = w.LpwmPin;
        REnPin = w.REnPin;
        LEnPin = w.LEnPin;
        UseEnablePins = w.UseEnablePins;
        EncoderAPin = w.EncoderAPin;
        EncoderBPin = w.EncoderBPin;
        EStopPin = w.EStopPin;
        Button1Pin = w.Button1Pin;
        Button2Pin = w.Button2Pin;
        ShifterXPin = w.ShifterXPin;
        ShifterYPin = w.ShifterYPin;
        HasPedals = w.HasPedals;
        ThrottlePin = w.ThrottlePin;
        BrakePin = w.BrakePin;
        ClutchPin = w.ClutchPin;
        PwmMode = w.PwmMode;
        LogicVoltage = w.LogicVoltage;
        CommonGround = w.CommonGround;
        MotorPlusTerminal = w.MotorPlusTerminal;
        MotorMinusTerminal = w.MotorMinusTerminal;
        LogicVccConnected = w.LogicVccConnected;
        LogicGndConnected = w.LogicGndConnected;
    }

    private void SaveState()
    {
        _state.LastStep = StepIndex;
        _state.SelectedPort = SelectedPort;
        _state.SelectedFirmwarePath = SelectedFirmware?.Path;
        _state.UseCustomBuild = UseCustomBuild;
        _state.Wiring = BuildWiringConfig();
        _wizardStateService.Save(_state);

        if (!string.IsNullOrWhiteSpace(SelectedPort))
        {
            _appSettings.LastPort = SelectedPort;
        }
        if (SelectedFirmware?.Path != null)
        {
            _appSettings.LastFirmwareHex = SelectedFirmware.Path;
        }
        _settingsService.Save(_appSettings);
    }

    [RelayCommand]
    private void Back()
    {
        StepIndex = Math.Max(0, StepIndex - 1);
        UpdateStepText();
        SaveState();
    }

    [RelayCommand]
    private void Next()
    {
        if (!ValidateWiring() && StepIndex <= 2)
        {
            WizardHint = "Fix wiring warnings before continuing.";
            return;
        }

        StepIndex = Math.Min(7, StepIndex + 1);
        UpdateStepText();
        SaveState();
    }

    [RelayCommand]
    private void CopyWiringSummary()
    {
        try
        {
            System.Windows.Clipboard.SetText(WiringSummary);
            WizardHint = "Wiring summary copied to clipboard.";
        }
        catch
        {
            WizardHint = "Unable to copy. Select the text manually.";
        }
    }

    [RelayCommand]
    private async Task DetectDeviceAsync()
    {
        var detected = await _deviceManager.AutoDetectPortAsync();
        if (!string.IsNullOrWhiteSpace(detected))
        {
            SelectedPort = detected;
            WizardHint = $"Detected device on {detected}.";
            SaveState();
        }
        else
        {
            WizardHint = "Device not detected. Select the COM port manually.";
        }
    }

    [RelayCommand]
    private async Task FlashFirmwareAsync()
    {
        if (SelectedFirmware == null || string.IsNullOrWhiteSpace(SelectedPort))
        {
            WizardHint = "Select firmware and COM port first.";
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            WizardHint = "Demo mode: flashing skipped.";
            return;
        }

        FlashLog = string.Empty;
        IsBusy = true;
        var progress = new Progress<string>(line => FlashLog += line + "\n");
        var result = await _flasher.FlashWithRetryAsync(SelectedFirmware.Path, SelectedPort, progress, CancellationToken.None);
        IsBusy = false;

        WizardHint = result.Success ? "Flash complete. Continue to calibration." : $"{result.UserMessage} {result.SuggestedAction}";
    }

    [RelayCommand]
    private async Task CalibrateAsync()
    {
        if (!_deviceState.CurrentDevice?.SupportsSerialConfig ?? true)
        {
            WizardHint = "Calibration requires serial config support.";
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            WizardHint = "Demo mode: calibration skipped.";
            return;
        }
        try
        {
            var result = await _calibration.CaptureCenterAsync(CancellationToken.None);
            WizardHint = result.Message;
        }
        catch (Exception ex)
        {
            WizardHint = $"Calibration failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplyPresetAsync()
    {
        if (SelectedPreset == null)
        {
            WizardHint = "Select a preset first.";
            return;
        }
        if (!_deviceState.CurrentDevice?.SupportsSerialConfig ?? true)
        {
            WizardHint = "Applying presets requires serial config support.";
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            WizardHint = "Demo mode: preset apply skipped.";
            return;
        }

        var cfg = SelectedPreset.Config;
        try
        {
            await _settings.ApplyConfigAsync(cfg, CancellationToken.None);
            _tuningState.UpdateConfig(cfg);
            WizardHint = "Preset applied.";
        }
        catch (Exception ex)
        {
            WizardHint = $"Preset apply failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAndFinishAsync()
    {
        if (!_deviceState.CurrentDevice?.SupportsSerialConfig ?? true)
        {
            WizardHint = "Save requires serial config support.";
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            WizardHint = "Demo mode: save skipped.";
            return;
        }

        try
        {
            await _settings.SaveToWheelAsync(CancellationToken.None);
            var profile = new Profile
            {
                Name = $"Wizard Backup {DateTime.Now:yyyyMMdd-HHmm}",
                Notes = "Auto-saved from Setup Wizard",
                Config = _tuningState.CurrentConfig ?? new FfbConfig(),
                Curve = _tuningState.CurrentCurve,
                Advanced = _tuningState.CurrentAdvanced,
                FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion
            };
            _profiles.SaveProfile(profile);
            _state.Completed = true;
            _state.CompletedUtc = DateTime.UtcNow;
            SaveState();
            _snapshots.CreateSnapshot(new SnapshotEntry
            {
                Kind = SnapshotKind.SaveToWheel,
                Label = "Wizard save",
                Config = _tuningState.CurrentConfig,
                Curve = _tuningState.CurrentCurve,
                Advanced = _tuningState.CurrentAdvanced,
                FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion,
                Wiring = BuildWiringConfig()
            });
            WizardHint = _settings.CanSaveToWheel() ? "Setup complete. Settings saved to wheel." : "Setup complete. EEPROM unavailable; settings saved to PC.";
        }
        catch (Exception ex)
        {
            WizardHint = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void UseMyWheelPreset()
    {
        RpwmPin = "D9";
        LpwmPin = "D10";
        REnPin = "D7";
        LEnPin = "D8";
        UseEnablePins = true;
        EncoderAPin = "D2";
        EncoderBPin = "D3";
        PwmMode = "PWM+-";
        LogicVoltage = "5V";
        CommonGround = true;
        HasPedals = true;
        ThrottlePin = "A0";
        BrakePin = "A1";
        ClutchPin = "A2";
        UpdateWiringSummary();
        WizardHint = "Loaded My Wheel developer preset.";
    }

    private void UpdateStepText()
    {
        WizardStatus = $"Step {StepIndex + 1} of 8";
        WizardHint = StepIndex switch
        {
            0 => "Map Arduino pins for BTS7960 and encoder.",
            1 => "Confirm BTS7960 wiring and power.",
            2 => "Configure pedals and optional inputs.",
            3 => "Detect device and select firmware.",
            4 => "Flash firmware (one-click).",
            5 => "Calibrate center and rotation.",
            6 => "Apply a baseline preset.",
            7 => "Save settings and finish.",
            _ => WizardHint
        };
    }

    private WiringConfig BuildWiringConfig()
    {
        return new WiringConfig
        {
            RpwmPin = RpwmPin,
            LpwmPin = LpwmPin,
            REnPin = REnPin,
            LEnPin = LEnPin,
            UseEnablePins = UseEnablePins,
            EncoderAPin = EncoderAPin,
            EncoderBPin = EncoderBPin,
            EStopPin = EStopPin,
            Button1Pin = Button1Pin,
            Button2Pin = Button2Pin,
            ShifterXPin = ShifterXPin,
            ShifterYPin = ShifterYPin,
            HasPedals = HasPedals,
            ThrottlePin = ThrottlePin,
            BrakePin = BrakePin,
            ClutchPin = ClutchPin,
            PwmMode = PwmMode,
            LogicVoltage = LogicVoltage,
            CommonGround = CommonGround,
            MotorPlusTerminal = MotorPlusTerminal,
            MotorMinusTerminal = MotorMinusTerminal,
            LogicVccConnected = LogicVccConnected,
            LogicGndConnected = LogicGndConnected
        };
    }

    private void UpdateWiringSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Arduino Leonardo Wiring Summary");
        sb.AppendLine($"RPWM: {RpwmPin}");
        sb.AppendLine($"LPWM: {LpwmPin}");
        sb.AppendLine($"R_EN: {REnPin}");
        sb.AppendLine($"L_EN: {LEnPin}");
        sb.AppendLine($"Encoder A: {EncoderAPin}");
        sb.AppendLine($"Encoder B: {EncoderBPin}");
        sb.AppendLine($"PWM Mode: {PwmMode}");
        sb.AppendLine($"Logic Voltage: {LogicVoltage}");
        sb.AppendLine($"Logic VCC/GND: {(LogicVccConnected && LogicGndConnected ? "Connected" : "Check")}");
        sb.AppendLine($"Common Ground: {(CommonGround ? "Yes" : "No")}");
        sb.AppendLine($"Motor + -> {MotorPlusTerminal}, Motor - -> {MotorMinusTerminal}");
        if (HasPedals)
        {
            sb.AppendLine("Pedals:");
            sb.AppendLine($"  Throttle: {ThrottlePin}");
            sb.AppendLine($"  Brake: {BrakePin}");
            sb.AppendLine($"  Clutch: {ClutchPin}");
        }
        WiringSummary = sb.ToString();
        ValidateWiring();
        SaveState();
    }

    private bool ValidateWiring()
    {
        WiringWarnings.Clear();
        var selected = new List<string?> { RpwmPin, LpwmPin, REnPin, LEnPin, EncoderAPin, EncoderBPin, EStopPin, Button1Pin, Button2Pin, ShifterXPin, ShifterYPin };
        if (HasPedals)
        {
            selected.AddRange(new[] { ThrottlePin, BrakePin, ClutchPin });
        }

        var duplicates = selected.Where(s => !string.IsNullOrWhiteSpace(s))
            .GroupBy(s => s)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            WiringWarnings.Add($"Duplicate pins detected: {string.Join(", ", duplicates)}");
        }

        if (!string.IsNullOrWhiteSpace(RpwmPin) && !PwmPins.Contains(RpwmPin))
        {
            WiringWarnings.Add("RPWM must be on a PWM-capable pin.");
        }

        if (PwmMode == "PWM+-" && !string.IsNullOrWhiteSpace(LpwmPin) && !PwmPins.Contains(LpwmPin))
        {
            WiringWarnings.Add("LPWM must be on a PWM-capable pin when using PWM+-.");
        }
        if (PwmMode == "PWM+DIR" && string.IsNullOrWhiteSpace(LpwmPin))
        {
            WiringWarnings.Add("PWM+DIR mode uses one PWM pin and a DIR pin. Ensure LPWM is wired to DIR.");
        }

        if (UseEnablePins && (string.IsNullOrWhiteSpace(REnPin) || string.IsNullOrWhiteSpace(LEnPin)))
        {
            WiringWarnings.Add("Enable pins are enabled but not mapped.");
        }

        if (!string.IsNullOrWhiteSpace(EncoderAPin) && !InterruptPins.Contains(EncoderAPin))
        {
            WiringWarnings.Add("Encoder A should be on an interrupt-capable pin.");
        }
        if (!string.IsNullOrWhiteSpace(EncoderBPin) && !InterruptPins.Contains(EncoderBPin))
        {
            WiringWarnings.Add("Encoder B should be on an interrupt-capable pin.");
        }

        if (!CommonGround)
        {
            WiringWarnings.Add("Common ground is required between Arduino and motor driver.");
        }

        if (!LogicVccConnected || !LogicGndConnected)
        {
            WiringWarnings.Add("BTS7960 logic VCC and GND must be connected.");
        }

        if (LogicVoltage == "3.3V")
        {
            WiringWarnings.Add("Leonardo logic is 5V; verify level shifting for 3.3V logic.");
        }

        var wiring = BuildWiringConfig();
        if (!UseCustomBuild && !wiring.IsDefaultLeonardo())
        {
            WiringWarnings.Add("Wiring does not match default Leonardo pinout. Use custom build or select a matching HEX.");
        }

        WiringValid = WiringWarnings.Count == 0;
        return WiringValid;
    }

    partial void OnRpwmPinChanged(string? value) => UpdateWiringSummary();
    partial void OnLpwmPinChanged(string? value) => UpdateWiringSummary();
    partial void OnEncoderAPinChanged(string? value) => UpdateWiringSummary();
    partial void OnEncoderBPinChanged(string? value) => UpdateWiringSummary();
    partial void OnREnPinChanged(string? value) => UpdateWiringSummary();
    partial void OnLEnPinChanged(string? value) => UpdateWiringSummary();
    partial void OnEStopPinChanged(string? value) => UpdateWiringSummary();
    partial void OnButton1PinChanged(string? value) => UpdateWiringSummary();
    partial void OnButton2PinChanged(string? value) => UpdateWiringSummary();
    partial void OnShifterXPinChanged(string? value) => UpdateWiringSummary();
    partial void OnShifterYPinChanged(string? value) => UpdateWiringSummary();
    partial void OnUseEnablePinsChanged(bool value) => UpdateWiringSummary();
    partial void OnThrottlePinChanged(string? value) => UpdateWiringSummary();
    partial void OnBrakePinChanged(string? value) => UpdateWiringSummary();
    partial void OnClutchPinChanged(string? value) => UpdateWiringSummary();
    partial void OnPwmModeChanged(string value) => UpdateWiringSummary();
    partial void OnLogicVoltageChanged(string value) => UpdateWiringSummary();
    partial void OnCommonGroundChanged(bool value) => UpdateWiringSummary();
    partial void OnHasPedalsChanged(bool value) => UpdateWiringSummary();
    partial void OnLogicVccConnectedChanged(bool value) => UpdateWiringSummary();
    partial void OnLogicGndConnectedChanged(bool value) => UpdateWiringSummary();
    partial void OnSelectedFirmwareChanged(FirmwareHexInfo? value) => SaveState();
    partial void OnSelectedPortChanged(string? value) => SaveState();
    partial void OnUseCustomBuildChanged(bool value) => SaveState();
}
