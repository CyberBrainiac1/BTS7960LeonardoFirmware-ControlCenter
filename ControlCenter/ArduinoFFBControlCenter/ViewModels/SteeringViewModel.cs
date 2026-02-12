using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class SteeringViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly DeviceSettingsService _settings;
    private readonly DeviceStateService _deviceState;
    private readonly TuningStateService _tuningState;

    [ObservableProperty] private int rotationDeg = 1080;
    [ObservableProperty] private bool canUseSerialConfig;
    [ObservableProperty] private string serialConfigNotice = string.Empty;

    public SteeringViewModel(LoggerService logger, DeviceSettingsService settings, DeviceStateService deviceState, TuningStateService tuningState)
    {
        _logger = logger;
        _settings = settings;
        _deviceState = deviceState;
        _tuningState = tuningState;
        SerialConfigNotice = "Connect a device to enable steering controls.";
        _deviceState.DeviceChanged += OnDeviceChanged;
        _tuningState.StateChanged += OnTuningStateChanged;
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
                RotationDeg = cfg.RotationDeg;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Load failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ApplyRotationAsync()
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
            await _settings.ApplyRotationAsync(RotationDeg, CancellationToken.None);
            _logger.Info("Rotation updated.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Set rotation failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CenterAsync()
    {
        if (!CanUseSerialConfig)
        {
            _logger.Warn("Serial config not available.");
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            _logger.Info("Demo mode: center skipped.");
            return;
        }
        try
        {
            await _settings.CenterAsync(CancellationToken.None);
            _logger.Info("Center command sent.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Center failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CalibrateAsync()
    {
        if (!CanUseSerialConfig)
        {
            _logger.Warn("Serial config not available.");
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            _logger.Info("Demo mode: calibration skipped.");
            return;
        }
        try
        {
            await _settings.CalibrateAsync(CancellationToken.None);
            _logger.Info("Calibration started.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Calibration failed: {ex.Message}");
        }
    }

    private void OnDeviceChanged(DeviceInfo? info)
    {
        if (info == null)
        {
            CanUseSerialConfig = false;
            SerialConfigNotice = "Connect a device to enable steering controls.";
            return;
        }

        CanUseSerialConfig = info.SupportsSerialConfig || info.IsDemo;
        SerialConfigNotice = CanUseSerialConfig ? string.Empty : "Serial config not supported by firmware.";
    }

    private void OnTuningStateChanged()
    {
        if (_tuningState.CurrentConfig != null)
        {
            RotationDeg = _tuningState.CurrentConfig.RotationDeg;
        }
    }
}
