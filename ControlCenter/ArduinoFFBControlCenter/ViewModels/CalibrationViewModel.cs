using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class CalibrationViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly CalibrationService _calibration;
    private readonly DeviceSettingsService _settings;
    private readonly DeviceStateService _deviceState;
    private readonly DeviceCapabilitiesService _caps;
    private readonly TuningStateService _tuningState;
    private readonly SnapshotService _snapshots;

    [ObservableProperty] private int stepIndex;
    [ObservableProperty] private string stepTitle = "Safety";
    [ObservableProperty] private string stepHint = "Set a safe low strength before calibration.";
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string calibrationStatus = "Unknown";
    [ObservableProperty] private string calibrationReason = string.Empty;

    [ObservableProperty] private string centerStatus = "Not captured";
    [ObservableProperty] private string directionStatus = "Not tested";
    [ObservableProperty] private string rotationStatus = "Not set";
    [ObservableProperty] private string saveStatus = "Not saved";
    [ObservableProperty] private string lastCalibrationTimestamp = "Unknown";

    [ObservableProperty] private bool invertDirection;
    [ObservableProperty] private int rotationDeg = 900;

    [ObservableProperty] private bool canUseSerialConfig;
    [ObservableProperty] private string serialConfigNotice = string.Empty;

    [ObservableProperty] private bool canSaveToWheel;
    [ObservableProperty] private string saveNotice = string.Empty;

    public CalibrationViewModel(LoggerService logger, CalibrationService calibration, DeviceSettingsService settings, DeviceStateService deviceState, DeviceCapabilitiesService caps, TuningStateService tuningState, SnapshotService snapshots)
    {
        _logger = logger;
        _calibration = calibration;
        _settings = settings;
        _deviceState = deviceState;
        _caps = caps;
        _tuningState = tuningState;
        _snapshots = snapshots;

        SerialConfigNotice = "Connect a device to calibrate.";
        _deviceState.DeviceChanged += OnDeviceChanged;
        _calibration.StatusChanged += OnCalibrationStatus;
        _tuningState.StateChanged += OnTuningStateChanged;

        UpdateStepText();
        UpdateFromConfig(_tuningState.CurrentConfig);
    }

    [RelayCommand]
    private void Back()
    {
        StepIndex = Math.Max(0, StepIndex - 1);
        UpdateStepText();
    }

    [RelayCommand]
    private void Next()
    {
        StepIndex = Math.Min(6, StepIndex + 1);
        UpdateStepText();
    }

    [RelayCommand]
    private async Task AssessAsync()
    {
        IsBusy = true;
        try
        {
            var assessment = await _calibration.AssessAsync(_tuningState.CurrentConfig, CancellationToken.None);
            CalibrationStatus = assessment.NeedsCalibration ? "Not calibrated" : "Calibrated";
            CalibrationReason = assessment.Reason;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CaptureCenterAsync()
    {
        if (!CanUseSerialConfig && !_deviceState.IsDemoMode)
        {
            CenterStatus = "Serial config required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _calibration.CaptureCenterAsync(CancellationToken.None);
            CenterStatus = result.Message;
            _ = _calibration.AssessAsync(_tuningState.CurrentConfig, CancellationToken.None);
        }
        catch (Exception ex)
        {
            CenterStatus = $"Center failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DetectDirectionAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _calibration.DetectDirectionAsync(CancellationToken.None);
            DirectionStatus = result.Message;
            if (result.DetectedValue is double delta && delta < 0)
            {
                InvertDirection = true;
            }
        }
        catch (Exception ex)
        {
            DirectionStatus = $"Direction check failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyRotationAsync()
    {
        if (!CanUseSerialConfig && !_deviceState.IsDemoMode)
        {
            RotationStatus = "Serial config required.";
            return;
        }

        IsBusy = true;
        try
        {
            await _settings.ApplyRotationAsync(RotationDeg, CancellationToken.None);
            RotationStatus = "Rotation applied.";
        }
        catch (Exception ex)
        {
            RotationStatus = $"Rotation failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task VerifyRotationAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _calibration.VerifyRotationAsync(CancellationToken.None);
            RotationStatus = result.Message;
        }
        catch (Exception ex)
        {
            RotationStatus = $"Verify failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            if (CanSaveToWheel && !_deviceState.IsDemoMode)
            {
                await _settings.SaveToWheelAsync(CancellationToken.None);
                SaveStatus = "Saved to wheel EEPROM.";
            }
            else
            {
                var profile = new Profile
                {
                    Name = $"Calibration Backup {DateTime.Now:yyyyMMdd-HHmm}",
                    Notes = "Auto-saved calibration (PC only)",
                    Config = _tuningState.CurrentConfig ?? new FfbConfig(),
                    FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion
                };
                _settings.SaveToPc(profile);
                SaveStatus = "Saved to PC (EEPROM unavailable).";
            }

            if (_deviceState.CurrentDevice != null)
            {
                var cpr = _tuningState.CurrentConfig?.EncoderCpr;
                _calibration.UpdateCalibrationMetadata(_deviceState.CurrentDevice, RotationDeg, InvertDirection, storedOnWheel: CanSaveToWheel, encoderCpr: cpr);
            }
            LastCalibrationTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            _ = _calibration.AssessAsync(_tuningState.CurrentConfig, CancellationToken.None);

            _snapshots.CreateSnapshot(new SnapshotEntry
            {
                Kind = SnapshotKind.Calibration,
                Label = "Calibration saved",
                Config = _tuningState.CurrentConfig,
                FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion
            });
        }
        catch (Exception ex)
        {
            SaveStatus = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnDeviceChanged(DeviceInfo? info)
    {
        if (info == null)
        {
            CanUseSerialConfig = false;
            SerialConfigNotice = "Connect a device to calibrate.";
            CanSaveToWheel = false;
            SaveNotice = "Connect a device to save.";
            return;
        }

        var caps = _caps.GetCapabilities(info);
        CanUseSerialConfig = caps.SupportsSerialConfig || info.IsDemo;
        SerialConfigNotice = CanUseSerialConfig ? string.Empty : "Serial config not supported by firmware.";

        CanSaveToWheel = caps.SupportsEepromSave && caps.SupportsSerialConfig;
        SaveNotice = CanSaveToWheel ? string.Empty : "Save to wheel disabled (no EEPROM).";
        _ = _calibration.AssessAsync(_tuningState.CurrentConfig, CancellationToken.None);
    }

    private void OnCalibrationStatus(CalibrationAssessment assessment)
    {
        CalibrationStatus = !assessment.IsSupported
            ? "Unknown"
            : assessment.NeedsCalibration ? "Not calibrated" : "Calibrated";
        CalibrationReason = assessment.Reason;
    }

    private void OnTuningStateChanged()
    {
        UpdateFromConfig(_tuningState.CurrentConfig);
    }

    private void UpdateFromConfig(FfbConfig? config)
    {
        if (config == null)
        {
            return;
        }
        RotationDeg = config.RotationDeg;
    }

    private void UpdateStepText()
    {
        StepTitle = StepIndex switch
        {
            0 => "Safety",
            1 => "Center",
            2 => "Direction",
            3 => "Rotation",
            4 => "Endstops",
            5 => "Save",
            6 => "Summary",
            _ => StepTitle
        };

        StepHint = StepIndex switch
        {
            0 => "Set strength to a low value before calibration.",
            1 => "Let go of the wheel and capture center.",
            2 => "Turn the wheel right slowly and test direction.",
            3 => "Set degrees of rotation and verify range.",
            4 => "Optional: test soft lock/endstop behavior.",
            5 => "Save calibration to wheel or PC.",
            6 => "Review and finish.",
            _ => StepHint
        };
    }
}
