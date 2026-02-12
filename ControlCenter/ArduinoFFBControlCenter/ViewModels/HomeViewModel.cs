using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly TelemetryService _telemetry;
    private readonly HidWheelService _hid;
    private readonly DeviceStateService _deviceState;
    private readonly CalibrationService _calibration;
    private readonly DeviceSettingsService _settings;

    [ObservableProperty]
    private double wheelAngle;

    [ObservableProperty]
    private double torqueCommand;

    [ObservableProperty]
    private bool clipping;

    [ObservableProperty]
    private double telemetryRate;

    [ObservableProperty]
    private bool oscillationDetected;

    [ObservableProperty]
    private string firmwareVersion = "Unknown";

    [ObservableProperty]
    private string port = "-";

    [ObservableProperty]
    private string vidPid = "-";

    [ObservableProperty]
    private string productName = "-";

    [ObservableProperty]
    private string capabilitiesText = "-";

    [ObservableProperty]
    private string infoSupportText = "not detected";

    [ObservableProperty]
    private string calibrationStatus = "Unknown";

    [ObservableProperty]
    private string calibrationReason = string.Empty;

    [ObservableProperty]
    private string saveStatus = "Unknown";

    public HomeViewModel(LoggerService logger, HidWheelService hid, TelemetryService telemetry, DeviceStateService deviceState, CalibrationService calibration, DeviceSettingsService settings)
    {
        _telemetry = telemetry;
        _hid = hid;
        _deviceState = deviceState;
        _calibration = calibration;
        _settings = settings;
        _telemetry.SamplesUpdated += OnSamplesUpdated;
        _telemetry.StatsUpdated += OnStatsUpdated;
        _deviceState.DeviceChanged += OnDeviceChanged;
        _calibration.StatusChanged += OnCalibrationChanged;
        _settings.PersistenceChanged += OnPersistenceChanged;
    }

    private void OnSamplesUpdated()
    {
        var last = _telemetry.GetSamplesSnapshot().LastOrDefault();
        if (last == null)
        {
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            WheelAngle = last.Angle;
            TorqueCommand = last.TorqueCommand;
            Clipping = last.Clipping;
        });

        var window = _telemetry.GetSamplesSnapshot().TakeLast(300).ToList();
        if (window.Count > 20)
        {
            var zeroCrossings = 0;
            var lastSign = Math.Sign(window[0].Velocity);
            for (int i = 1; i < window.Count; i++)
            {
                var sign = Math.Sign(window[i].Velocity);
                if (sign != 0 && lastSign != 0 && sign != lastSign)
                {
                    zeroCrossings++;
                }
                if (sign != 0)
                {
                    lastSign = sign;
                }
            }
            OscillationDetected = zeroCrossings > 20;
        }
    }

    private void OnStatsUpdated(Models.TelemetryStats stats)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            TelemetryRate = stats.SampleRateHz;
        });
    }

    private void OnDeviceChanged(Models.DeviceInfo? info)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (info == null)
            {
                FirmwareVersion = "Unknown";
                Port = "-";
                VidPid = "-";
                ProductName = "-";
                CapabilitiesText = "-";
                InfoSupportText = "not detected";
                CalibrationStatus = "Unknown";
                CalibrationReason = string.Empty;
                SaveStatus = "Unknown";
                return;
            }

            FirmwareVersion = info.FirmwareVersion;
            Port = info.Port;
            VidPid = info.Vid != null && info.Pid != null ? $"{info.Vid}:{info.Pid}" : "-";
            ProductName = info.ProductName ?? "Arduino FFB Wheel";
            CapabilitiesText = CapabilityFormatter.Format(info);
            InfoSupportText = info.SupportsInfoCommand ? "supported" : "not detected";
        });
    }

    private void OnCalibrationChanged(Models.CalibrationAssessment assessment)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            CalibrationStatus = !assessment.IsSupported
                ? "Unknown"
                : assessment.NeedsCalibration ? "Not calibrated (check)" : "Calibrated (ok)";
            CalibrationReason = assessment.Reason;
        });
    }

    private void OnPersistenceChanged(Models.SettingsPersistenceState state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SaveStatus = state switch
            {
                Models.SettingsPersistenceState.SavedToWheel => "Saved to Wheel",
                Models.SettingsPersistenceState.SavedToPc => "Saved to PC",
                Models.SettingsPersistenceState.UnsavedChanges => "Unsaved changes",
                _ => "Unknown"
            };
        });
    }
}
