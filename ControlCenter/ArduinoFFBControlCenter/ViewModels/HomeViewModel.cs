using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;
using QRCoder;

namespace ArduinoFFBControlCenter.ViewModels;

public class HomeSnapshotRow
{
    public string IconGlyph { get; set; } = "\uE8A5";
    public string Title { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string DiffSummary { get; set; } = string.Empty;
}

public partial class HomeViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly TelemetryService _telemetry;
    private readonly HidWheelService _hid;
    private readonly DeviceStateService _deviceState;
    private readonly CalibrationService _calibration;
    private readonly DeviceSettingsService _settings;
    private readonly DashboardHostService _dashboardHost;
    private readonly SnapshotService _snapshots;

    [ObservableProperty] private double wheelAngle;
    [ObservableProperty] private double torqueCommand;
    [ObservableProperty] private bool clipping;
    [ObservableProperty] private double clippingPercent;
    [ObservableProperty] private double telemetryRate;
    [ObservableProperty] private bool? oscillationDetected;
    [ObservableProperty] private double oscillationRiskPercent;
    [ObservableProperty] private double encoderNoise;
    [ObservableProperty] private double packetLossPercent = double.NaN;
    [ObservableProperty] private double telemetryStability = 100;
    [ObservableProperty] private double connectionQuality = 100;
    [ObservableProperty] private string firmwareVersion = "—";
    [ObservableProperty] private string port = "—";
    [ObservableProperty] private string vidPid = "—";
    [ObservableProperty] private string productName = "—";
    [ObservableProperty] private string capabilitiesText = "—";
    [ObservableProperty] private string infoSupportText = "—";
    [ObservableProperty] private string calibrationStatus = "—";
    [ObservableProperty] private string calibrationReason = string.Empty;
    [ObservableProperty] private string saveStatus = "—";
    [ObservableProperty] private string phoneDashboardStatus = "Stopped";
    [ObservableProperty] private string phoneDashboardUrl = "-";
    [ObservableProperty] private ImageSource? phoneDashboardQr;
    [ObservableProperty] private string gamePresetStatus = "Not started";

    public ObservableCollection<HomeSnapshotRow> RecentSnapshots { get; } = new();

    public HomeViewModel(
        LoggerService logger,
        HidWheelService hid,
        TelemetryService telemetry,
        DeviceStateService deviceState,
        CalibrationService calibration,
        DeviceSettingsService settings,
        DashboardHostService dashboardHost,
        SnapshotService snapshots)
    {
        _logger = logger;
        _telemetry = telemetry;
        _hid = hid;
        _deviceState = deviceState;
        _calibration = calibration;
        _settings = settings;
        _dashboardHost = dashboardHost;
        _snapshots = snapshots;
        _telemetry.SamplesUpdated += OnSamplesUpdated;
        _telemetry.StatsUpdated += OnStatsUpdated;
        _deviceState.DeviceChanged += OnDeviceChanged;
        _calibration.StatusChanged += OnCalibrationChanged;
        _settings.PersistenceChanged += OnPersistenceChanged;
        _dashboardHost.StateChanged += OnDashboardStateChanged;

        RefreshRecentSnapshots();
        UpdateDashboard(_dashboardHost.State);
    }

    [RelayCommand]
    private async Task StartGamePresetsAsync()
    {
        if (_deviceState.CurrentDevice == null && !_deviceState.IsDemoMode)
        {
            GamePresetStatus = "Connect the wheel first.";
            return;
        }

        try
        {
            var preset = PresetLibraryService.GetGamePresets()
                             .FirstOrDefault(p => p.Name.Contains("BeamNG", StringComparison.OrdinalIgnoreCase))
                         ?? PresetLibraryService.GetWheelPresets().FirstOrDefault();
            if (preset == null)
            {
                GamePresetStatus = "No preset found.";
                return;
            }

            if (_deviceState.IsDemoMode)
            {
                GamePresetStatus = $"Demo mode: would apply {preset.Name}.";
                return;
            }

            if (!_deviceState.CurrentDevice!.SupportsSerialConfig)
            {
                GamePresetStatus = "Firmware does not support serial config commands.";
                return;
            }

            await _settings.ApplyConfigAsync(preset.Config, CancellationToken.None);
            var motorCheck = await _calibration.RunMotorRotationCalibrationAsync(CancellationToken.None);

            if (!motorCheck.Success)
            {
                GamePresetStatus = $"Preset applied, but motor/encoder check failed: {motorCheck.Message}";
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
                FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion
            });

            GamePresetStatus = $"Ready: {preset.Name}. Encoder movement test passed.";
        }
        catch (Exception ex)
        {
            _logger.Warn($"Start game presets failed: {ex.Message}");
            GamePresetStatus = $"Start game presets failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleDashboardAsync()
    {
        if (_dashboardHost.State.IsRunning)
        {
            await _dashboardHost.StopAsync();
        }
        else
        {
            await _dashboardHost.StartAsync();
        }

        UpdateDashboard(_dashboardHost.State);
    }

    [RelayCommand]
    private void RefreshSnapshots()
    {
        RefreshRecentSnapshots();
    }

    private void OnSamplesUpdated()
    {
        var samples = _telemetry.GetSamplesSnapshot();
        var last = samples.LastOrDefault();
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

        var window = samples.TakeLast(250).ToList();
        if (window.Count <= 20)
        {
            return;
        }

        var zeroCrossings = 0;
        var lastSign = Math.Sign(window[0].Velocity);
        for (var i = 1; i < window.Count; i++)
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

        var jitter = 0d;
        if (window.Count > 1)
        {
            var deltas = new List<double>(window.Count - 1);
            for (var i = 1; i < window.Count; i++)
            {
                deltas.Add(Math.Abs(window[i].Angle - window[i - 1].Angle));
            }

            jitter = deltas.Average();
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            var oscillation = zeroCrossings > 20;
            OscillationDetected = oscillation;
            OscillationRiskPercent = oscillation ? 100 : Math.Min(100, zeroCrossings * 4);
            EncoderNoise = Math.Round(Math.Min(100, jitter * 10), 1);
        });
    }

    private void OnStatsUpdated(TelemetryStats stats)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            TelemetryRate = stats.SampleRateHz;
            ClippingPercent = stats.ClippingPercent;
            PacketLossPercent = stats.PacketLossPercent;
            var expected = 200d;
            var stability = expected <= 0 ? 0 : Math.Min(100, (stats.SampleRateHz / expected) * 100);
            TelemetryStability = Math.Round(stability, 1);
            ConnectionQuality = Math.Round((TelemetryStability * 0.7) + ((100 - EncoderNoise) * 0.3), 1);
        });
    }

    private void OnDeviceChanged(DeviceInfo? info)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (info == null)
            {
                FirmwareVersion = "—";
                Port = "—";
                VidPid = "—";
                ProductName = "—";
                CapabilitiesText = "—";
                InfoSupportText = "—";
                CalibrationStatus = "—";
                CalibrationReason = string.Empty;
                SaveStatus = "—";
                ClippingPercent = double.NaN;
                TelemetryRate = double.NaN;
                OscillationDetected = null;
                OscillationRiskPercent = double.NaN;
                EncoderNoise = double.NaN;
                PacketLossPercent = double.NaN;
                TelemetryStability = double.NaN;
                ConnectionQuality = double.NaN;
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

    private void OnCalibrationChanged(CalibrationAssessment assessment)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            CalibrationStatus = !assessment.IsSupported
                ? "—"
                : assessment.NeedsCalibration ? "Not calibrated" : "Calibrated";
            CalibrationReason = assessment.Reason;
        });
    }

    private void OnPersistenceChanged(SettingsPersistenceState state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SaveStatus = state switch
            {
                SettingsPersistenceState.SavedToWheel => "Saved to Wheel",
                SettingsPersistenceState.SavedToPc => "Saved to PC",
                SettingsPersistenceState.UnsavedChanges => "Unsaved changes",
                _ => "—"
            };

            RefreshRecentSnapshots();
        });
    }

    private void OnDashboardStateChanged(DashboardHostState state)
    {
        Application.Current.Dispatcher.Invoke(() => UpdateDashboard(state));
    }

    private void UpdateDashboard(DashboardHostState state)
    {
        if (!state.IsRunning || string.IsNullOrWhiteSpace(state.PrimaryAddress))
        {
            PhoneDashboardStatus = "Stopped";
            PhoneDashboardUrl = "-";
            PhoneDashboardQr = null;
            return;
        }

        PhoneDashboardStatus = "Running";
        PhoneDashboardUrl = $"http://{state.PrimaryAddress}:{state.Port}";
        PhoneDashboardQr = BuildQr(PhoneDashboardUrl);
    }

    private void RefreshRecentSnapshots()
    {
        var all = _snapshots.LoadSnapshots();
        RecentSnapshots.Clear();
        var newest = all.Take(3).ToList();
        for (var i = 0; i < newest.Count; i++)
        {
            var current = newest[i];
            var older = i + 1 < all.Count ? all[i + 1] : null;
            RecentSnapshots.Add(new HomeSnapshotRow
            {
                IconGlyph = GlyphFor(current.Kind),
                Title = string.IsNullOrWhiteSpace(current.Label) ? current.Kind.ToString() : current.Label!,
                Timestamp = current.CreatedUtc.ToLocalTime().ToString("MMM dd, HH:mm"),
                DiffSummary = BuildSummary(current, older)
            });
        }
    }

    private static string BuildSummary(SnapshotEntry current, SnapshotEntry? previous)
    {
        if (previous == null)
        {
            return current.FirmwareVersion != null ? $"FW {current.FirmwareVersion}" : "Initial snapshot";
        }

        var changes = new List<string>();
        if (current.Config != null && previous.Config != null)
        {
            if (current.Config.GeneralGain != previous.Config.GeneralGain)
            {
                changes.Add($"Strength {(current.Config.GeneralGain - previous.Config.GeneralGain):+0;-0;0}%");
            }
            if (current.Config.DamperGain != previous.Config.DamperGain)
            {
                changes.Add($"Damping {(current.Config.DamperGain - previous.Config.DamperGain):+0;-0;0}%");
            }
            if (current.Config.FrictionGain != previous.Config.FrictionGain)
            {
                changes.Add($"Friction {(current.Config.FrictionGain - previous.Config.FrictionGain):+0;-0;0}%");
            }
        }

        if (!string.Equals(current.FirmwareVersion, previous.FirmwareVersion, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add($"FW {previous.FirmwareVersion ?? "?"} -> {current.FirmwareVersion ?? "?"}");
        }

        return changes.Count == 0 ? "No major changes" : string.Join(" | ", changes.Take(3));
    }

    private static string GlyphFor(SnapshotKind kind)
    {
        return kind switch
        {
            SnapshotKind.Flash => "\uE9CA",
            SnapshotKind.SaveToWheel => "\uE74E",
            SnapshotKind.ApplyProfile => "\uEB51",
            SnapshotKind.Calibration => "\uE9D9",
            SnapshotKind.SelfTest => "\uE9D9",
            SnapshotKind.Revert => "\uE72B",
            _ => "\uE8A5"
        };
    }

    private static ImageSource? BuildQr(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var code = new PngByteQRCode(data);
        var bytes = code.GetGraphic(4);

        using var ms = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = ms;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
