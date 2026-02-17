using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

/// <summary>
/// Guided onboarding flow for first-time wheel setup.
/// Captures wiring, firmware choice, calibration, baseline preset, and save.
/// </summary>
public partial class SetupWizardViewModel : ViewModelBase
{
    // Temporary product mode: keep steering pinout fixed for reliability tests.
    public bool IsPinMappingLocked => true;

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
    private WizardState _state = new();
    private bool _isInitializing = true;
    private bool _suspendSummaryRefresh;

    private static readonly Regex PinAssignmentRegex = new(
        @"(?<key>rpwm|lpwm|r[_\s-]?en|l[_\s-]?en|encoder\s*a|encoder\s*b|e-?stop|button\s*1|button\s*2|shifter\s*x|shifter\s*y|throttle|brake|clutch)\s*(?:=|:|is|to|->)?\s*(?<pin>[ad]\d{1,2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
    public ObservableCollection<string> AiProviderOptions { get; } = new();

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
    [ObservableProperty] private bool useEnablePins;
    [ObservableProperty] private string? encoderAPin;
    [ObservableProperty] private string? encoderBPin;
    [ObservableProperty] private string? eStopPin;
    [ObservableProperty] private string? button1Pin;
    [ObservableProperty] private string? button2Pin;
    [ObservableProperty] private string? shifterXPin;
    [ObservableProperty] private string? shifterYPin;

    [ObservableProperty] private bool hasPedals;
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
    [ObservableProperty] private string gamePresetStatus = "Not run";

    [ObservableProperty] private bool useCustomBuild;
    [ObservableProperty] private string customBuildStatus = "Custom build disabled";
    [ObservableProperty] private string aiProvider = "Ollama";
    [ObservableProperty] private string aiEndpoint = "http://localhost:11434";
    [ObservableProperty] private string aiModel = string.Empty;
    [ObservableProperty] private string aiApiKey = string.Empty;
    public bool IsApiKeyProvider => string.Equals(AiProvider, "ApiKey", StringComparison.OrdinalIgnoreCase);

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
        // Initialization order matters: load options first, then restore saved wizard state.
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
        ApplyFixedPinoutDefaults();

        if (IsPinMappingLocked)
        {
            _appSettings.SendAsInoMode = false;
            _appSettings.ForcePinoutBuild = false;
            _settingsService.Save(_appSettings);
        }

        UpdateStepText();
        UpdateWiringSummary();
        _isInitializing = false;
    }

    [RelayCommand]
    private async Task BuildCustomFirmwareAsync()
    {
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
        // Pin catalogs are constrained to valid Leonardo pins to prevent invalid mappings.
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
        RefreshPorts();

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

        SelectedFirmware = FirmwareOptions.FirstOrDefault(f => f.Name.StartsWith("Recommended", StringComparison.OrdinalIgnoreCase))
                           ?? FirmwareOptions.FirstOrDefault(f => f.Name.Contains("v250", StringComparison.OrdinalIgnoreCase))
                           ?? FirmwareOptions.FirstOrDefault();
        SelectedFirmware = _library.GetRecommended() ?? SelectedFirmware;
        SelectedPreset = Presets.FirstOrDefault();
    }

    private void RefreshPorts()
    {
        Ports.Clear();
        foreach (var port in _deviceManager.ScanPorts())
        {
            Ports.Add(port);
        }
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

        AiProviderOptions.Clear();
        AiProviderOptions.Add("Ollama");
        AiProviderOptions.Add("ApiKey");
    }

    private void ApplyState(WizardState state)
    {
        StepIndex = Math.Clamp(state.LastStep, 0, 7);
        SelectedPort = state.SelectedPort;
        SelectedFirmware = FirmwareOptions.FirstOrDefault(f => f.Path == state.SelectedFirmwarePath) ?? SelectedFirmware;
        UseCustomBuild = state.UseCustomBuild;
        AiProvider = string.IsNullOrWhiteSpace(_appSettings.AiProvider) ? "Ollama" : _appSettings.AiProvider;
        AiEndpoint = string.IsNullOrWhiteSpace(_appSettings.AiEndpoint) ? "http://localhost:11434" : _appSettings.AiEndpoint!;
        AiModel = _appSettings.AiModel ?? string.Empty;
        AiApiKey = _appSettings.AiApiKey ?? string.Empty;

        var w = state.Wiring ?? new WiringConfig();
        RpwmPin = string.IsNullOrWhiteSpace(w.RpwmPin) ? "D10" : w.RpwmPin;
        LpwmPin = string.IsNullOrWhiteSpace(w.LpwmPin) ? "D9" : w.LpwmPin;
        REnPin = w.REnPin;
        LEnPin = w.LEnPin;
        UseEnablePins = w.UseEnablePins;
        EncoderAPin = string.IsNullOrWhiteSpace(w.EncoderAPin) ? "D2" : w.EncoderAPin;
        EncoderBPin = string.IsNullOrWhiteSpace(w.EncoderBPin) ? "D3" : w.EncoderBPin;
        EStopPin = w.EStopPin;
        Button1Pin = w.Button1Pin;
        Button2Pin = w.Button2Pin;
        ShifterXPin = w.ShifterXPin;
        ShifterYPin = w.ShifterYPin;
        HasPedals = w.HasPedals || !string.IsNullOrWhiteSpace(w.ThrottlePin) || !string.IsNullOrWhiteSpace(w.BrakePin) || !string.IsNullOrWhiteSpace(w.ClutchPin);
        ThrottlePin = w.ThrottlePin;
        BrakePin = w.BrakePin;
        ClutchPin = w.ClutchPin;
        PwmMode = string.IsNullOrWhiteSpace(w.PwmMode) ? "PWM+-" : w.PwmMode;
        LogicVoltage = string.IsNullOrWhiteSpace(w.LogicVoltage) ? "5V" : w.LogicVoltage;
        CommonGround = w.CommonGround;
        MotorPlusTerminal = string.IsNullOrWhiteSpace(w.MotorPlusTerminal) ? "M+" : w.MotorPlusTerminal;
        MotorMinusTerminal = string.IsNullOrWhiteSpace(w.MotorMinusTerminal) ? "M-" : w.MotorMinusTerminal;
        LogicVccConnected = w.LogicVccConnected;
        LogicGndConnected = w.LogicGndConnected;

        // Migrate legacy wizard defaults to the current steering-only baseline.
        if (IsLegacyDefaultMapping())
        {
            UseMyWheelPreset();
        }
    }

    private void ApplyFixedPinoutDefaults()
    {
        if (!IsPinMappingLocked)
        {
            return;
        }

        // Fixed known-good default wiring:
        // D10 -> RPWM, D9 -> LPWM, D2/D3 encoder, EN tied to 5V, steering-only (no pedals).
        RpwmPin = "D10";
        LpwmPin = "D9";
        REnPin = null;
        LEnPin = null;
        UseEnablePins = false;
        EncoderAPin = "D2";
        EncoderBPin = "D3";
        HasPedals = false;
        ThrottlePin = null;
        BrakePin = null;
        ClutchPin = null;
        PwmMode = "PWM+-";
        LogicVoltage = "5V";
        CommonGround = true;
        LogicVccConnected = true;
        LogicGndConnected = true;
        SelectedFirmware = _library.GetRecommended() ?? SelectedFirmware;
    }

    private void SaveState()
    {
        // Property change handlers fire during constructor setup; skip persistence until ready.
        if (_isInitializing || _state == null)
        {
            return;
        }

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
        _appSettings.AiProvider = AiProvider;
        _appSettings.AiEndpoint = AiEndpoint;
        _appSettings.AiModel = AiModel;
        _appSettings.AiApiKey = AiApiKey;
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
        RefreshPorts();

        if (!string.IsNullOrWhiteSpace(SelectedPort) &&
            Ports.Contains(SelectedPort, StringComparer.OrdinalIgnoreCase))
        {
            WizardHint = $"Using selected port {SelectedPort}.";
            SaveState();
            return;
        }

        var detected = await _deviceManager.AutoDetectPortAsync();
        if (!string.IsNullOrWhiteSpace(detected))
        {
            SelectedPort = detected;
            WizardHint = $"Detected device on {detected}.";
            SaveState();
        }
        else if (Ports.Count == 1)
        {
            SelectedPort = Ports[0];
            WizardHint = $"Only one COM port found, using {SelectedPort}.";
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
        if (string.IsNullOrWhiteSpace(SelectedPort) || !Ports.Contains(SelectedPort, StringComparer.OrdinalIgnoreCase))
        {
            await DetectDeviceAsync();
            if (string.IsNullOrWhiteSpace(SelectedPort) || !Ports.Contains(SelectedPort, StringComparer.OrdinalIgnoreCase))
            {
                WizardHint = "Select firmware and COM port first.";
                return;
            }
        }

        if (SelectedFirmware == null && !_appSettings.SendAsInoMode)
        {
            SelectedFirmware = _library.GetRecommended() ?? FirmwareOptions.FirstOrDefault();
            if (SelectedFirmware == null)
            {
                WizardHint = "No firmware HEX found in library.";
                return;
            }
        }
        if (_deviceState.IsDemoMode)
        {
            WizardHint = "Demo mode: flashing skipped.";
            return;
        }

        FlashLog = string.Empty;
        IsBusy = true;
        var hexPath = SelectedFirmware?.Path;
        var customUsed = false;

        try
        {
            var wiring = BuildWiringConfig();
            var requiresPinoutBuild = _appSettings.SendAsInoMode || _appSettings.ForcePinoutBuild || !wiring.IsDefaultLeonardo();
            if (requiresPinoutBuild)
            {
                CustomBuildStatus = _appSettings.SendAsInoMode
                    ? "Building from .ino source using your pinout..."
                    : "Building pinout-specific firmware...";
                var build = await _builder.BuildAsync(wiring, CancellationToken.None);
                CustomBuildStatus = build.Message;

                if (!build.Success || string.IsNullOrWhiteSpace(build.HexPath))
                {
                    if (!string.IsNullOrWhiteSpace(build.OutputLog))
                    {
                        FlashLog += build.OutputLog + "\n";
                    }
                    WizardHint = $"Custom build failed: {build.Message}";
                    return;
                }

                hexPath = build.HexPath;
                customUsed = true;
                FlashLog += $"Using custom HEX for selected pinout: {hexPath}\n";

                var existing = FirmwareOptions.FirstOrDefault(f => string.Equals(f.Path, hexPath, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    existing = new FirmwareHexInfo
                    {
                        Name = $"Custom Pinout {DateTime.Now:HH:mm:ss}",
                        Path = hexPath,
                        Notes = "Auto-built from wizard wiring"
                    };
                    FirmwareOptions.Insert(0, existing);
                }
                SelectedFirmware = existing;
            }

            if (string.IsNullOrWhiteSpace(hexPath))
            {
                WizardHint = "No HEX selected.";
                return;
            }

            var progress = new Progress<string>(line => FlashLog += line + "\n");
            var result = await _flasher.FlashWithRetryAsync(hexPath, SelectedPort, progress, CancellationToken.None);

            // Fallback: if reset/bootloader handshake failed, try direct flash once.
            if (!result.Success && result.ErrorType == FlashErrorType.BootloaderNotDetected)
            {
                FlashLog += "Bootloader detect failed; trying direct flash on selected COM...\n";
                result = await _flasher.FlashWithRetryAsync(hexPath, SelectedPort, progress, CancellationToken.None, skipReset: true);
            }

            WizardHint = result.Success
                ? customUsed
                    ? "Flash complete. Pinout-specific custom firmware is now on the wheel."
                    : "Flash complete. Continue to calibration."
                : $"{result.UserMessage} {result.SuggestedAction}";
        }
        finally
        {
            IsBusy = false;
        }
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
        ApplyFixedPinoutDefaults();
        UpdateWiringSummary();
        WizardHint = "Loaded steering-only default preset (RPWM D10, LPWM D9, Encoder D2/D3, EN tied to 5V).";
    }

    [RelayCommand]
    private async Task StartGamePresetsAsync()
    {
        if (_deviceState.CurrentDevice == null && !_deviceState.IsDemoMode)
        {
            GamePresetStatus = "Connect the wheel first.";
            WizardHint = GamePresetStatus;
            return;
        }

        IsBusy = true;
        try
        {
            var preset = PresetLibraryService.GetGamePresets()
                             .FirstOrDefault(p => p.Name.Contains("BeamNG", StringComparison.OrdinalIgnoreCase))
                         ?? PresetLibraryService.GetWheelPresets().FirstOrDefault();
            if (preset == null)
            {
                GamePresetStatus = "No game preset found.";
                WizardHint = GamePresetStatus;
                return;
            }

            if (_deviceState.IsDemoMode)
            {
                GamePresetStatus = $"Demo mode: would apply {preset.Name}.";
                WizardHint = GamePresetStatus;
                return;
            }

            if (!_deviceState.CurrentDevice!.SupportsSerialConfig)
            {
                GamePresetStatus = "Current firmware does not support serial tuning commands.";
                WizardHint = GamePresetStatus;
                return;
            }

            await _settings.ApplyConfigAsync(preset.Config, CancellationToken.None);
            _tuningState.UpdateConfig(preset.Config);

            var motorCheck = await _calibration.RunMotorRotationCalibrationAsync(CancellationToken.None);
            if (!motorCheck.Success)
            {
                GamePresetStatus = $"Preset applied, but motor/encoder check failed: {motorCheck.Message}";
                WizardHint = GamePresetStatus;
                return;
            }

            if (_settings.CanSaveToWheel())
            {
                await _settings.SaveToWheelAsync(CancellationToken.None);
            }
            else
            {
                _settings.SaveToPc(new Profile
                {
                    Name = $"Game Ready {DateTime.Now:yyyyMMdd-HHmm}",
                    Notes = $"Auto generated from Start Game Presets ({preset.Name})",
                    Config = preset.Config,
                    FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion
                });
            }

            _snapshots.CreateSnapshot(new SnapshotEntry
            {
                Kind = SnapshotKind.ApplyProfile,
                Label = $"Game ready ({preset.Name})",
                Config = preset.Config,
                FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion,
                Wiring = BuildWiringConfig()
            });

            GamePresetStatus = $"Wheel ready for gameplay: {preset.Name}. Encoder check OK.";
            WizardHint = GamePresetStatus;
        }
        catch (Exception ex)
        {
            GamePresetStatus = $"Game preset start failed: {ex.Message}";
            WizardHint = GamePresetStatus;
        }
        finally
        {
            IsBusy = false;
        }
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
        if (_suspendSummaryRefresh)
        {
            return;
        }

        // Human-readable wiring table used in UI and troubleshooting exports.
        var sb = new StringBuilder();
        sb.AppendLine("Arduino Leonardo Wiring Summary");
        sb.AppendLine($"RPWM: {RpwmPin}");
        sb.AppendLine($"LPWM: {LpwmPin}");
        sb.AppendLine($"R_EN: {(UseEnablePins ? (string.IsNullOrWhiteSpace(REnPin) ? "UNSET" : REnPin) : "Tied to 5V")}");
        sb.AppendLine($"L_EN: {(UseEnablePins ? (string.IsNullOrWhiteSpace(LEnPin) ? "UNSET" : LEnPin) : "Tied to 5V")}");
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
        else
        {
            sb.AppendLine("Pedals: Not used (steering wheel only)");
        }
        WiringSummary = sb.ToString();
        ValidateWiring();
        SaveState();
    }

    private bool ValidateWiring()
    {
        // Validation is strict by design: catch unsafe/invalid wiring early.
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
        WiringValid = WiringWarnings.Count == 0;
        return WiringValid;
    }

    /// <summary>
    /// Applies wiring/pin values from free-form text like:
    /// "rpwm d9, lpwm d10, encoder a d2, encoder b d3, throttle a0".
    /// Used by the AI sidebar to modify wizard fields directly.
    /// </summary>
    public (bool Applied, string Summary) ApplyNaturalLanguageConfig(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, "No input provided.");
        }

        var updates = new List<string>();
        var warnings = new List<string>();

        _suspendSummaryRefresh = true;
        try
        {
            foreach (Match match in PinAssignmentRegex.Matches(text))
            {
                var key = NormalizeKey(match.Groups["key"].Value);
                var pin = NormalizePin(match.Groups["pin"].Value);
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(pin))
                {
                    continue;
                }

                if (!TryApplyPin(key, pin, updates, warnings))
                {
                    continue;
                }
            }

            // Mode/safety toggles from natural language.
            if (Regex.IsMatch(text, @"\b(disable|off|no)\s+enable\s+pins?\b", RegexOptions.IgnoreCase))
            {
                UseEnablePins = false;
                updates.Add("Enable pins OFF");
            }
            else if (Regex.IsMatch(text, @"\b(enable|use|on)\s+enable\s+pins?\b", RegexOptions.IgnoreCase))
            {
                UseEnablePins = true;
                updates.Add("Enable pins ON");
            }

            if (Regex.IsMatch(text, @"\bpwm\+/?-\b|\bpwm\s*\+\s*-\b", RegexOptions.IgnoreCase))
            {
                PwmMode = "PWM+-";
                updates.Add("PWM mode PWM+-");
            }
            else if (Regex.IsMatch(text, @"\bpwm\+dir\b|\bpwm\s*\+\s*dir\b", RegexOptions.IgnoreCase))
            {
                PwmMode = "PWM+DIR";
                updates.Add("PWM mode PWM+DIR");
            }

            if (Regex.IsMatch(text, @"\b3\.?3v\b", RegexOptions.IgnoreCase))
            {
                LogicVoltage = "3.3V";
                updates.Add("Logic 3.3V");
            }
            else if (Regex.IsMatch(text, @"\b5v\b", RegexOptions.IgnoreCase))
            {
                LogicVoltage = "5V";
                updates.Add("Logic 5V");
            }

            if (Regex.IsMatch(text, @"\b(no|without)\s+common\s+ground\b", RegexOptions.IgnoreCase))
            {
                CommonGround = false;
                updates.Add("Common ground OFF");
            }
            else if (Regex.IsMatch(text, @"\b(common|shared)\s+ground\b", RegexOptions.IgnoreCase))
            {
                CommonGround = true;
                updates.Add("Common ground ON");
            }

            if (Regex.IsMatch(text, @"\b(no|without)\s+pedals?\b", RegexOptions.IgnoreCase))
            {
                HasPedals = false;
                ThrottlePin = null;
                BrakePin = null;
                ClutchPin = null;
                updates.Add("Pedals OFF");
            }
            else if (Regex.IsMatch(text, @"\b(with|enable)\s+pedals?\b", RegexOptions.IgnoreCase))
            {
                HasPedals = true;
                updates.Add("Pedals ON");
            }
        }
        finally
        {
            _suspendSummaryRefresh = false;
        }

        UpdateWiringSummary();

        if (updates.Count == 0 && warnings.Count == 0)
        {
            return (false, "No recognized wiring changes found. Example: RPWM D10, LPWM D9, Encoder A D2, Encoder B D3.");
        }

        var summary = updates.Count > 0
            ? $"Applied: {string.Join("; ", updates)}."
            : "No valid updates applied.";
        if (warnings.Count > 0)
        {
            summary += $" Warnings: {string.Join(" | ", warnings)}";
        }

        WizardHint = summary;
        return (updates.Count > 0, summary);
    }

    private static string NormalizeKey(string raw)
    {
        var key = raw.Trim().ToLowerInvariant();
        return key switch
        {
            "rpwm" => "rpwm",
            "lpwm" => "lpwm",
            "r_en" or "r en" or "ren" => "ren",
            "l_en" or "l en" or "len" => "len",
            "encoder a" => "enca",
            "encoder b" => "encb",
            "e-stop" or "estop" => "estop",
            "button 1" => "btn1",
            "button 2" => "btn2",
            "shifter x" => "shiftx",
            "shifter y" => "shifty",
            "throttle" => "throttle",
            "brake" => "brake",
            "clutch" => "clutch",
            _ => string.Empty
        };
    }

    private static string NormalizePin(string raw)
    {
        var value = raw.Trim().ToUpperInvariant();
        if (!value.StartsWith("D") && !value.StartsWith("A"))
        {
            return string.Empty;
        }

        return value;
    }

    private bool TryApplyPin(string key, string pin, List<string> updates, List<string> warnings)
    {
        bool IsValid(ObservableCollection<string> options) => options.Contains(pin);

        switch (key)
        {
            case "rpwm":
                if (!IsValid(PwmPins))
                {
                    warnings.Add($"RPWM pin {pin} is not PWM-capable.");
                    return false;
                }
                RpwmPin = pin;
                updates.Add($"RPWM={pin}");
                return true;

            case "lpwm":
                if (PwmMode == "PWM+-" && !IsValid(PwmPins))
                {
                    warnings.Add($"LPWM pin {pin} is not PWM-capable for PWM+- mode.");
                    return false;
                }
                LpwmPin = pin;
                updates.Add($"LPWM={pin}");
                return true;

            case "ren":
                if (!IsValid(DigitalPins))
                {
                    warnings.Add($"R_EN pin {pin} is invalid.");
                    return false;
                }
                REnPin = pin;
                updates.Add($"R_EN={pin}");
                return true;

            case "len":
                if (!IsValid(DigitalPins))
                {
                    warnings.Add($"L_EN pin {pin} is invalid.");
                    return false;
                }
                LEnPin = pin;
                updates.Add($"L_EN={pin}");
                return true;

            case "enca":
                if (!IsValid(InterruptPins))
                {
                    warnings.Add($"Encoder A pin {pin} should be interrupt-capable.");
                }
                EncoderAPin = pin;
                updates.Add($"Encoder A={pin}");
                return true;

            case "encb":
                if (!IsValid(InterruptPins))
                {
                    warnings.Add($"Encoder B pin {pin} should be interrupt-capable.");
                }
                EncoderBPin = pin;
                updates.Add($"Encoder B={pin}");
                return true;

            case "estop":
                if (!IsValid(DigitalPins))
                {
                    warnings.Add($"E-Stop pin {pin} is invalid.");
                    return false;
                }
                EStopPin = pin;
                updates.Add($"E-Stop={pin}");
                return true;

            case "btn1":
                if (!IsValid(DigitalPins))
                {
                    warnings.Add($"Button 1 pin {pin} is invalid.");
                    return false;
                }
                Button1Pin = pin;
                updates.Add($"Button1={pin}");
                return true;

            case "btn2":
                if (!IsValid(DigitalPins))
                {
                    warnings.Add($"Button 2 pin {pin} is invalid.");
                    return false;
                }
                Button2Pin = pin;
                updates.Add($"Button2={pin}");
                return true;

            case "shiftx":
                if (!IsValid(AnalogPins))
                {
                    warnings.Add($"Shifter X pin {pin} should be analog.");
                    return false;
                }
                ShifterXPin = pin;
                updates.Add($"Shifter X={pin}");
                return true;

            case "shifty":
                if (!IsValid(AnalogPins))
                {
                    warnings.Add($"Shifter Y pin {pin} should be analog.");
                    return false;
                }
                ShifterYPin = pin;
                updates.Add($"Shifter Y={pin}");
                return true;

            case "throttle":
                if (!IsValid(AnalogPins))
                {
                    warnings.Add($"Throttle pin {pin} should be analog.");
                    return false;
                }
                ThrottlePin = pin;
                updates.Add($"Throttle={pin}");
                return true;

            case "brake":
                if (!IsValid(AnalogPins))
                {
                    warnings.Add($"Brake pin {pin} should be analog.");
                    return false;
                }
                BrakePin = pin;
                updates.Add($"Brake={pin}");
                return true;

            case "clutch":
                if (!IsValid(AnalogPins))
                {
                    warnings.Add($"Clutch pin {pin} should be analog.");
                    return false;
                }
                ClutchPin = pin;
                updates.Add($"Clutch={pin}");
                return true;
        }

        return false;
    }

    private bool IsLegacyDefaultMapping()
    {
        return string.Equals(RpwmPin, "D9", StringComparison.OrdinalIgnoreCase)
               && string.Equals(LpwmPin, "D10", StringComparison.OrdinalIgnoreCase)
               && string.Equals(REnPin, "D7", StringComparison.OrdinalIgnoreCase)
               && string.Equals(LEnPin, "D8", StringComparison.OrdinalIgnoreCase)
               && string.Equals(EncoderAPin, "D2", StringComparison.OrdinalIgnoreCase)
               && string.Equals(EncoderBPin, "D3", StringComparison.OrdinalIgnoreCase)
               && HasPedals
               && string.Equals(ThrottlePin, "A0", StringComparison.OrdinalIgnoreCase)
               && string.Equals(BrakePin, "A1", StringComparison.OrdinalIgnoreCase)
               && string.Equals(ClutchPin, "A2", StringComparison.OrdinalIgnoreCase);
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
    partial void OnHasPedalsChanged(bool value)
    {
        if (!value)
        {
            ThrottlePin = null;
            BrakePin = null;
            ClutchPin = null;
        }
        else
        {
            ThrottlePin ??= "A0";
            BrakePin ??= "A1";
            ClutchPin ??= "A2";
        }

        UpdateWiringSummary();
    }
    partial void OnLogicVccConnectedChanged(bool value) => UpdateWiringSummary();
    partial void OnLogicGndConnectedChanged(bool value) => UpdateWiringSummary();
    partial void OnSelectedFirmwareChanged(FirmwareHexInfo? value) => SaveState();
    partial void OnSelectedPortChanged(string? value) => SaveState();
    partial void OnUseCustomBuildChanged(bool value) => SaveState();
    partial void OnAiProviderChanged(string value)
    {
        OnPropertyChanged(nameof(IsApiKeyProvider));
        SaveState();
    }
    partial void OnAiEndpointChanged(string value) => SaveState();
    partial void OnAiModelChanged(string value) => SaveState();
    partial void OnAiApiKeyChanged(string value) => SaveState();
}
