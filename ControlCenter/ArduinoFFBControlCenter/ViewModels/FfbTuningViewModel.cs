using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class FfbTuningViewModel : ViewModelBase
{
    private static readonly Regex ValuePattern = new(
        @"(?<key>strength|overall|general|damping|damper|friction|inertia|spring|constant|periodic|centering|center|endstop|stop|min\s*torque|minforce|steering\s*scale|slew|smoothing|notch|lowpass)\s*(?:=|:|to|is)?\s*(?<val>-?\d{1,3})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly LoggerService _logger;
    private readonly DeviceSettingsService _settings;
    private readonly DeviceStateService _deviceState;
    private readonly UiModeService _uiMode;
    private readonly TuningStateService _tuningState;
    private readonly DeviceCapabilitiesService _caps;
    private readonly SnapshotService _snapshots;
    private readonly System.Windows.Threading.DispatcherTimer _unlockTimer;
    private DateTime _unlockStart;
    private bool _unlocking;

    [ObservableProperty] private int generalGain = 100;
    [ObservableProperty] private int damperGain = 50;
    [ObservableProperty] private int frictionGain = 50;
    [ObservableProperty] private int inertiaGain = 50;
    [ObservableProperty] private int springGain = 100;
    [ObservableProperty] private int constantGain = 100;
    [ObservableProperty] private int periodicGain = 100;
    [ObservableProperty] private int centerGain = 70;
    [ObservableProperty] private int stopGain = 100;
    [ObservableProperty] private int minTorque = 0;

    [ObservableProperty] private double previewSmoothing = 0.35;
    [ObservableProperty] private PointCollection previewPoints = new();

    [ObservableProperty] private bool canSaveToEeprom = true;
    [ObservableProperty] private string eepromNotice = string.Empty;
    [ObservableProperty] private bool canRestoreBackup;
    [ObservableProperty] private string backupNotice = string.Empty;

    [ObservableProperty] private bool canUseSerialConfig;
    [ObservableProperty] private string serialConfigNotice = string.Empty;

    [ObservableProperty] private bool isBeginnerMode;
    [ObservableProperty] private bool isKidMode;
    [ObservableProperty] private bool advancedEnabled;
    [ObservableProperty] private bool strengthUnlocked;
    [ObservableProperty] private double strengthUnlockProgress;
    [ObservableProperty] private int strengthMax = 120;

    [ObservableProperty] private int mechanicalDampingRatio = 40;
    [ObservableProperty] private int steeringScale = 100;
    [ObservableProperty] private int minForceBoost;

    [ObservableProperty] private FfbCurveType curveType = FfbCurveType.Linear;
    [ObservableProperty] private bool isCustomCurve;
    [ObservableProperty] private double curveBias = 0.5;
    [ObservableProperty] private double curveCustomP1 = 0.25;
    [ObservableProperty] private double curveCustomP2 = 0.5;
    [ObservableProperty] private double curveCustomP3 = 0.75;
    [ObservableProperty] private PointCollection curvePoints = new();
    [ObservableProperty] private bool canApplyCurve;
    [ObservableProperty] private string curveNotice = "Curve apply requires firmware support.";

    [ObservableProperty] private int slewRate;
    [ObservableProperty] private int smoothing;
    [ObservableProperty] private int notchFilter;
    [ObservableProperty] private int lowPassFilter;
    [ObservableProperty] private int softLockStrength;
    [ObservableProperty] private int softLockRange;
    [ObservableProperty] private bool oscillationGuardEnabled;
    [ObservableProperty] private int oscillationGuardStrength = 20;
    [ObservableProperty] private int dampingBoost = 10;
    [ObservableProperty] private int frictionBoost = 10;
    [ObservableProperty] private bool advancedSupported;
    [ObservableProperty] private string advancedNotice = "Advanced filters require firmware support.";

    public IReadOnlyList<FfbCurveType> CurveTypeOptions { get; } = Enum.GetValues(typeof(FfbCurveType)).Cast<FfbCurveType>().ToList();

    public FfbTuningViewModel(LoggerService logger, DeviceSettingsService settings, DeviceStateService deviceState, UiModeService uiMode, TuningStateService tuningState, DeviceCapabilitiesService caps, SnapshotService snapshots)
    {
        _logger = logger;
        _settings = settings;
        _deviceState = deviceState;
        _uiMode = uiMode;
        _tuningState = tuningState;
        _caps = caps;
        _snapshots = snapshots;
        IsBeginnerMode = _uiMode.IsBeginnerMode;
        IsKidMode = _uiMode.IsKidMode;
        AdvancedEnabled = !IsBeginnerMode && !IsKidMode;
        IsCustomCurve = CurveType == FfbCurveType.Custom;
        SerialConfigNotice = "Connect a device to enable tuning.";
        UpdatePreview();
        UpdateCurvePreview();
        _deviceState.DeviceChanged += OnDeviceChanged;
        _uiMode.ModeChanged += OnModeChanged;
        _uiMode.KidModeChanged += OnKidModeChanged;
        _tuningState.StateChanged += OnTuningStateChanged;
        CanRestoreBackup = _settings.LoadBackup() != null;

        _unlockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(60)
        };
        _unlockTimer.Tick += (_, __) => TickUnlock();
    }

    partial void OnPreviewSmoothingChanged(double value)
    {
        UpdatePreview();
    }

    partial void OnCurveTypeChanged(FfbCurveType value)
    {
        IsCustomCurve = value == FfbCurveType.Custom;
        UpdateCurvePreview();
    }

    partial void OnCurveBiasChanged(double value)
    {
        UpdateCurvePreview();
    }

    partial void OnCurveCustomP1Changed(double value) => UpdateCurvePreview();
    partial void OnCurveCustomP2Changed(double value) => UpdateCurvePreview();
    partial void OnCurveCustomP3Changed(double value) => UpdateCurvePreview();

    partial void OnSteeringScaleChanged(int value) => UpdateAdvancedState();
    partial void OnMinForceBoostChanged(int value) => UpdateAdvancedState();
    partial void OnSlewRateChanged(int value) => UpdateAdvancedState();
    partial void OnSmoothingChanged(int value) => UpdateAdvancedState();
    partial void OnNotchFilterChanged(int value) => UpdateAdvancedState();
    partial void OnLowPassFilterChanged(int value) => UpdateAdvancedState();
    partial void OnSoftLockStrengthChanged(int value) => UpdateAdvancedState();
    partial void OnSoftLockRangeChanged(int value) => UpdateAdvancedState();
    partial void OnOscillationGuardEnabledChanged(bool value) => UpdateAdvancedState();
    partial void OnOscillationGuardStrengthChanged(int value) => UpdateAdvancedState();
    partial void OnDampingBoostChanged(int value) => UpdateAdvancedState();
    partial void OnFrictionBoostChanged(int value) => UpdateAdvancedState();

    partial void OnMechanicalDampingRatioChanged(int value)
    {
        if (!CanUseSerialConfig)
        {
            return;
        }

        var clamped = Math.Clamp(value, 0, 100);
        DamperGain = clamped;
        FrictionGain = (int)(clamped * 0.7);
        InertiaGain = (int)(clamped * 0.5);
    }

    private void UpdatePreview()
    {
        var points = new PointCollection();
        double prev = 0;
        for (int i = 0; i <= 100; i++)
        {
            double input = (i / 100.0) * 2 - 1;
            prev = prev + (input - prev) * PreviewSmoothing;
            var x = i;
            var y = 50 - prev * 45;
            points.Add(new Point(x, y));
        }
        PreviewPoints = points;
    }

    [RelayCommand]
    private async Task LoadFromDeviceAsync()
    {
        if (!CanUseSerialConfig)
        {
            _logger.Warn("Serial config not available.");
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            _logger.Info("Demo mode: load skipped.");
            return;
        }
        try
        {
            var cfg = await _settings.LoadFromDeviceAsync(CancellationToken.None);
            if (cfg != null)
            {
                UpdateFromConfig(cfg);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Load from device failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (!CanUseSerialConfig)
        {
            _logger.Warn("Serial config not available.");
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            _logger.Info("Demo mode: apply skipped.");
            return;
        }
        try
        {
            UpdateAdvancedState();
            await _settings.ApplyConfigAsync(BuildConfigFromCurrent(), CancellationToken.None);
            _logger.Info("FFB settings applied.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Apply failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveToDeviceAsync()
    {
        if (_deviceState.IsDemoMode)
        {
            _logger.Info("Demo mode: save skipped.");
            return;
        }
        try
        {
            if (CanSaveToEeprom)
            {
                await _settings.SaveToWheelAsync(CancellationToken.None);
                _logger.Info("Settings saved to EEPROM.");
                _snapshots.CreateSnapshot(new SnapshotEntry
                {
                    Kind = SnapshotKind.SaveToWheel,
                    Label = "Save to wheel",
                    Config = BuildConfigFromCurrent(),
                    Curve = _tuningState.CurrentCurve,
                    Advanced = _tuningState.CurrentAdvanced,
                    FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion
                });
            }
            else
            {
                var profile = new Profile
                {
                    Name = $"Auto Backup {DateTime.Now:yyyyMMdd-HHmm}",
                    Notes = "EEPROM unavailable; saved to PC",
                    Config = BuildConfigFromCurrent(),
                    Curve = _tuningState.CurrentCurve,
                    Advanced = _tuningState.CurrentAdvanced,
                    FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion
                };
                _settings.SaveToPc(profile);
                _logger.Info("Settings saved to PC profile.");
            }
            CanRestoreBackup = _settings.LoadBackup() != null;
        }
        catch (Exception ex)
        {
            _logger.Error($"Save failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RestorePreviousAsync()
    {
        if (!CanRestoreBackup)
        {
            _logger.Warn("No backup available.");
            return;
        }
        try
        {
            await _settings.RestoreBackupAsync(CancellationToken.None);
            _logger.Info("Previous settings restored.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Restore failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ApplyPresetBts()
    {
        GeneralGain = 100;
        DamperGain = 40;
        FrictionGain = 40;
        InertiaGain = 30;
        SpringGain = 90;
        ConstantGain = 100;
        PeriodicGain = 100;
        CenterGain = 70;
        StopGain = 100;
        MinTorque = 0;
        UpdateTuningStateFromCurrent();
    }

    [RelayCommand]
    private void ApplyPresetComfort()
    {
        GeneralGain = 70;
        DamperGain = 30;
        FrictionGain = 30;
        InertiaGain = 25;
        SpringGain = 80;
        ConstantGain = 80;
        PeriodicGain = 80;
        CenterGain = 60;
        StopGain = 80;
        MinTorque = 0;
        UpdateTuningStateFromCurrent();
    }

    [RelayCommand]
    private void ApplyPresetDrift()
    {
        GeneralGain = 110;
        DamperGain = 20;
        FrictionGain = 15;
        InertiaGain = 10;
        SpringGain = 80;
        ConstantGain = 110;
        PeriodicGain = 110;
        CenterGain = 40;
        StopGain = 70;
        MinTorque = 0;
        UpdateTuningStateFromCurrent();
    }

    [RelayCommand]
    private void ApplyPresetRace()
    {
        GeneralGain = 120;
        DamperGain = 60;
        FrictionGain = 50;
        InertiaGain = 45;
        SpringGain = 110;
        ConstantGain = 120;
        PeriodicGain = 120;
        CenterGain = 70;
        StopGain = 120;
        MinTorque = 0;
        UpdateTuningStateFromCurrent();
    }

    private void OnDeviceChanged(DeviceInfo? info)
    {
        if (info == null)
        {
            CanSaveToEeprom = false;
            EepromNotice = "Connect a device to enable EEPROM save.";
            CanRestoreBackup = _settings.LoadBackup() != null;
            CanUseSerialConfig = false;
            SerialConfigNotice = "Connect a device to enable tuning.";
            AdvancedSupported = false;
            CurveNotice = "Curve apply requires firmware support.";
            CanApplyCurve = false;
            return;
        }

        var caps = _caps.GetCapabilities(info);
        CanUseSerialConfig = caps.SupportsSerialConfig || info.IsDemo;
        SerialConfigNotice = CanUseSerialConfig ? string.Empty : "Serial config not supported by firmware.";

        if (!caps.SupportsEepromSave)
        {
            CanSaveToEeprom = false;
            EepromNotice = "Save disabled: firmware built with 'p' (no EEPROM).";
        }
        else
        {
            CanSaveToEeprom = true;
            EepromNotice = string.Empty;
        }

        StrengthUnlocked = info.IsDemo;
        StrengthMax = _uiMode.IsKidMode ? 80 : (StrengthUnlocked ? 200 : 120);
        CanRestoreBackup = _settings.LoadBackup() != null;

        AdvancedSupported = false;
        CanApplyCurve = false;
        CurveNotice = "Curve apply requires firmware support.";
    }

    private void OnModeChanged(bool isBeginner)
    {
        IsBeginnerMode = isBeginner;
        AdvancedEnabled = !IsBeginnerMode && !IsKidMode;
    }

    private void OnKidModeChanged(bool isKid)
    {
        IsKidMode = isKid;
        StrengthUnlocked = false;
        StrengthMax = isKid ? 80 : (StrengthUnlocked ? 200 : 120);
        AdvancedEnabled = !IsBeginnerMode && !IsKidMode;
    }

    private void UpdateCurvePreview()
    {
        var points = new PointCollection();
        for (int i = 0; i <= 100; i += 5)
        {
            var x = i / 100.0;
            var y = GetCurveY(x);
            points.Add(new Point(i, 100 - y * 100));
        }
        CurvePoints = points;
        _tuningState.UpdateCurve(new FfbCurveSettings
        {
            CurveType = CurveType,
            Bias = CurveBias,
            CustomP1 = CurveCustomP1,
            CustomP2 = CurveCustomP2,
            CustomP3 = CurveCustomP3
        });
    }

    private void UpdateAdvancedState()
    {
        _tuningState.UpdateAdvanced(new AdvancedTuningSettings
        {
            SteeringScale = SteeringScale,
            MinForceBoost = MinForceBoost,
            SlewRate = SlewRate,
            Smoothing = Smoothing,
            NotchFilter = NotchFilter,
            LowPassFilter = LowPassFilter,
            SoftLockStrength = SoftLockStrength,
            SoftLockRange = SoftLockRange,
            OscillationGuardEnabled = OscillationGuardEnabled,
            OscillationGuardStrength = OscillationGuardStrength,
            DampingBoost = DampingBoost,
            FrictionBoost = FrictionBoost
        });
    }

    private double GetCurveY(double x)
    {
        return CurveType switch
        {
            FfbCurveType.Progressive => Math.Pow(x, 1 + (1 - CurveBias)),
            FfbCurveType.Regressive => Math.Pow(x, CurveBias),
            FfbCurveType.Custom => CustomCurve(x),
            _ => x
        };
    }

    private double CustomCurve(double x)
    {
        if (x <= 0.25) return Lerp(0, CurveCustomP1, x / 0.25);
        if (x <= 0.5) return Lerp(CurveCustomP1, CurveCustomP2, (x - 0.25) / 0.25);
        if (x <= 0.75) return Lerp(CurveCustomP2, CurveCustomP3, (x - 0.5) / 0.25);
        return Lerp(CurveCustomP3, 1, (x - 0.75) / 0.25);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public void BeginStrengthUnlock()
    {
        if (IsKidMode)
        {
            return;
        }
        if (StrengthUnlocked)
        {
            return;
        }
        _unlocking = true;
        _unlockStart = DateTime.UtcNow;
        StrengthUnlockProgress = 0;
        _unlockTimer.Start();
    }

    public void CancelStrengthUnlock()
    {
        _unlocking = false;
        _unlockTimer.Stop();
        StrengthUnlockProgress = 0;
    }

    private void TickUnlock()
    {
        if (!_unlocking)
        {
            return;
        }
        var elapsed = (DateTime.UtcNow - _unlockStart).TotalMilliseconds;
        StrengthUnlockProgress = Math.Clamp(elapsed / 2000.0, 0, 1);
        if (elapsed >= 2000)
        {
            StrengthUnlocked = true;
            StrengthMax = 200;
            _unlockTimer.Stop();
            _unlocking = false;
        }
    }

    private void OnTuningStateChanged()
    {
        if (_tuningState.CurrentConfig != null)
        {
            UpdateFromConfig(_tuningState.CurrentConfig);
        }
        if (_tuningState.CurrentAdvanced != null)
        {
            SteeringScale = _tuningState.CurrentAdvanced.SteeringScale;
            MinForceBoost = _tuningState.CurrentAdvanced.MinForceBoost;
            SlewRate = _tuningState.CurrentAdvanced.SlewRate;
            Smoothing = _tuningState.CurrentAdvanced.Smoothing;
            NotchFilter = _tuningState.CurrentAdvanced.NotchFilter;
            LowPassFilter = _tuningState.CurrentAdvanced.LowPassFilter;
            SoftLockStrength = _tuningState.CurrentAdvanced.SoftLockStrength;
            SoftLockRange = _tuningState.CurrentAdvanced.SoftLockRange;
            OscillationGuardEnabled = _tuningState.CurrentAdvanced.OscillationGuardEnabled;
            OscillationGuardStrength = _tuningState.CurrentAdvanced.OscillationGuardStrength;
            DampingBoost = _tuningState.CurrentAdvanced.DampingBoost;
            FrictionBoost = _tuningState.CurrentAdvanced.FrictionBoost;
        }
    }

    private void UpdateFromConfig(FfbConfig cfg)
    {
        GeneralGain = cfg.GeneralGain;
        DamperGain = cfg.DamperGain;
        FrictionGain = cfg.FrictionGain;
        InertiaGain = cfg.InertiaGain;
        SpringGain = cfg.SpringGain;
        ConstantGain = cfg.ConstantGain;
        PeriodicGain = cfg.PeriodicGain;
        CenterGain = cfg.CenterGain;
        StopGain = cfg.StopGain;
        MinTorque = cfg.MinTorque;
    }

    private void UpdateTuningStateFromCurrent()
    {
        _tuningState.UpdateConfig(BuildConfigFromCurrent());
        UpdateAdvancedState();
    }

    private FfbConfig BuildConfigFromCurrent()
    {
        var baseCfg = _tuningState.CurrentConfig ?? new FfbConfig();
        var scaled = (int)Math.Round(GeneralGain * (SteeringScale / 100.0));
        baseCfg.GeneralGain = Math.Clamp(scaled, 0, 200);
        baseCfg.DamperGain = DamperGain;
        baseCfg.FrictionGain = FrictionGain;
        baseCfg.InertiaGain = InertiaGain;
        baseCfg.SpringGain = SpringGain;
        baseCfg.ConstantGain = ConstantGain;
        baseCfg.PeriodicGain = PeriodicGain;
        baseCfg.CenterGain = CenterGain;
        baseCfg.StopGain = StopGain;
        baseCfg.MinTorque = MinTorque;
        return baseCfg;
    }

    /// <summary>
    /// Applies tuning values from natural language. Example:
    /// "strength 95 damping 40 friction 25 steering scale 110".
    /// </summary>
    public (bool Applied, string Summary) ApplyNaturalLanguageTuning(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, "No tuning text provided.");
        }

        var updates = new List<string>();

        if (Regex.IsMatch(text, @"\bbts\b|\bdefault\b", RegexOptions.IgnoreCase))
        {
            ApplyPresetBts();
            updates.Add("Preset=BTS default");
        }
        else if (Regex.IsMatch(text, @"\bcomfort\b", RegexOptions.IgnoreCase))
        {
            ApplyPresetComfort();
            updates.Add("Preset=Comfort");
        }
        else if (Regex.IsMatch(text, @"\bdrift\b", RegexOptions.IgnoreCase))
        {
            ApplyPresetDrift();
            updates.Add("Preset=Drift");
        }
        else if (Regex.IsMatch(text, @"\brace\b", RegexOptions.IgnoreCase))
        {
            ApplyPresetRace();
            updates.Add("Preset=Race");
        }

        foreach (Match match in ValuePattern.Matches(text))
        {
            var key = match.Groups["key"].Value.Trim().ToLowerInvariant();
            if (!int.TryParse(match.Groups["val"].Value, out var value))
            {
                continue;
            }

            switch (key)
            {
                case "strength":
                case "overall":
                case "general":
                    GeneralGain = Math.Clamp(value, 0, StrengthMax);
                    updates.Add($"Strength={GeneralGain}");
                    break;
                case "damping":
                case "damper":
                    DamperGain = Math.Clamp(value, 0, 200);
                    updates.Add($"Damper={DamperGain}");
                    break;
                case "friction":
                    FrictionGain = Math.Clamp(value, 0, 200);
                    updates.Add($"Friction={FrictionGain}");
                    break;
                case "inertia":
                    InertiaGain = Math.Clamp(value, 0, 200);
                    updates.Add($"Inertia={InertiaGain}");
                    break;
                case "spring":
                    SpringGain = Math.Clamp(value, 0, 200);
                    updates.Add($"Spring={SpringGain}");
                    break;
                case "constant":
                    ConstantGain = Math.Clamp(value, 0, 200);
                    updates.Add($"Constant={ConstantGain}");
                    break;
                case "periodic":
                    PeriodicGain = Math.Clamp(value, 0, 200);
                    updates.Add($"Periodic={PeriodicGain}");
                    break;
                case "centering":
                case "center":
                    CenterGain = Math.Clamp(value, 0, 200);
                    updates.Add($"Center={CenterGain}");
                    break;
                case "endstop":
                case "stop":
                    StopGain = Math.Clamp(value, 0, 200);
                    updates.Add($"Endstop={StopGain}");
                    break;
                case "min torque":
                case "minforce":
                    MinTorque = Math.Clamp(value, 0, 100);
                    updates.Add($"MinTorque={MinTorque}");
                    break;
                case "steering scale":
                    SteeringScale = Math.Clamp(value, 0, 200);
                    updates.Add($"SteeringScale={SteeringScale}");
                    break;
                case "slew":
                    SlewRate = Math.Clamp(value, 0, 100);
                    updates.Add($"Slew={SlewRate}");
                    break;
                case "smoothing":
                    Smoothing = Math.Clamp(value, 0, 100);
                    updates.Add($"Smoothing={Smoothing}");
                    break;
                case "notch":
                    NotchFilter = Math.Clamp(value, 0, 100);
                    updates.Add($"Notch={NotchFilter}");
                    break;
                case "lowpass":
                    LowPassFilter = Math.Clamp(value, 0, 100);
                    updates.Add($"LowPass={LowPassFilter}");
                    break;
            }
        }

        if (updates.Count == 0)
        {
            return (false, "No recognized tuning commands found.");
        }

        UpdateTuningStateFromCurrent();
        return (true, $"Applied tuning: {string.Join("; ", updates)}.");
    }
}


