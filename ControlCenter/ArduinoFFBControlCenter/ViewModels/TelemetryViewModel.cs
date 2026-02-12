using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using ArduinoFFBControlCenter.Services;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class TelemetryViewModel : ViewModelBase
{
    private readonly TelemetryService _telemetry;
    private readonly DeviceStateService _deviceState;
    private readonly DispatcherTimer _timer;

    [ObservableProperty] private PointCollection anglePoints = new();
    [ObservableProperty] private PointCollection torquePoints = new();
    [ObservableProperty] private PointCollection velocityPoints = new();
    [ObservableProperty] private PointCollection loopDtPoints = new();

    [ObservableProperty] private double sampleRateHz;
    [ObservableProperty] private double torqueRateHz;
    [ObservableProperty] private double clippingPercent;
    [ObservableProperty] private double packetLossPercent;
    [ObservableProperty] private string healthSummary = "Telemetry idle";

    [ObservableProperty] private bool oscillationDetected;
    [ObservableProperty] private bool noiseDetected;

    [ObservableProperty] private bool canShowTelemetry;
    [ObservableProperty] private string telemetryNotice = string.Empty;
    [ObservableProperty] private string insights = string.Empty;

    public TelemetryViewModel(LoggerService logger, TelemetryService telemetry, DeviceStateService deviceState)
    {
        _telemetry = telemetry;
        _deviceState = deviceState;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _timer.Tick += (_, __) => RefreshPoints();
        _timer.Start();
        _telemetry.StatsUpdated += OnStatsUpdated;
        _deviceState.DeviceChanged += OnDeviceChanged;
        TelemetryNotice = "Connect a device to see telemetry.";
    }

    [RelayCommand]
    private void ExportLast60s()
    {
        var samples = _telemetry.GetSamplesSnapshot();
        if (samples.Count == 0)
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddSeconds(-60);
        var recent = samples.Where(s => s.Timestamp >= cutoff).ToList();
        if (recent.Count == 0)
        {
            recent = samples.ToList();
        }

        var path = Path.Combine(AppPaths.AppDataRoot, $"telemetry-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("timestamp,angle,velocity,torque_command,clipping,loop_dt_ms");
        foreach (var s in recent)
        {
            sb.AppendLine($"{s.Timestamp:o},{s.Angle:0.###},{s.Velocity:0.###},{s.TorqueCommand:0.###},{s.Clipping},{s.LoopDtMs:0.###}");
        }
        File.WriteAllText(path, sb.ToString());
    }

    private void RefreshPoints()
    {
        var samples = _telemetry.GetSamplesSnapshot();
        if (samples.Count == 0)
        {
            return;
        }

        var angleMin = samples.Min(s => s.Angle);
        var angleMax = samples.Max(s => s.Angle);
        var torqueMin = samples.Min(s => s.TorqueCommand);
        var torqueMax = samples.Max(s => s.TorqueCommand);
        var velMin = samples.Min(s => s.Velocity);
        var velMax = samples.Max(s => s.Velocity);
        var dtMin = samples.Min(s => s.LoopDtMs);
        var dtMax = samples.Max(s => s.LoopDtMs);

        angleMin = Math.Min(angleMin, angleMax - 1);
        torqueMin = Math.Min(torqueMin, torqueMax - 1);

        var anglePts = new PointCollection();
        var torquePts = new PointCollection();
        var velPts = new PointCollection();
        var dtPts = new PointCollection();

        for (int i = 0; i < samples.Count; i++)
        {
            var x = i * (100.0 / Math.Max(1, samples.Count - 1));
            var angleNorm = (samples[i].Angle - angleMin) / (angleMax - angleMin);
            var torqueNorm = (samples[i].TorqueCommand - torqueMin) / (torqueMax - torqueMin);
            var velNorm = (samples[i].Velocity - velMin) / Math.Max(1, velMax - velMin);
            var dtNorm = (samples[i].LoopDtMs - dtMin) / Math.Max(1, dtMax - dtMin);
            anglePts.Add(new Point(x, 100 - angleNorm * 100));
            torquePts.Add(new Point(x, 100 - torqueNorm * 100));
            velPts.Add(new Point(x, 100 - velNorm * 100));
            dtPts.Add(new Point(x, 100 - dtNorm * 100));
        }

        AnglePoints = anglePts;
        TorquePoints = torquePts;
        VelocityPoints = velPts;
        LoopDtPoints = dtPts;

        Analyze(samples);
    }

    private void OnStatsUpdated(TelemetryStats stats)
    {
        SampleRateHz = stats.SampleRateHz;
        TorqueRateHz = stats.TorqueLineRateHz;
        ClippingPercent = stats.ClippingPercent;
        PacketLossPercent = stats.PacketLossPercent;
        HealthSummary = $"Rate {stats.SampleRateHz:0}Hz, Torque {stats.TorqueLineRateHz:0}Hz, Clip {stats.ClippingPercent:0}%";
    }

    private void Analyze(IReadOnlyList<TelemetrySample> samples)
    {
        var window = samples.TakeLast(300).ToList();
        if (window.Count < 20)
        {
            return;
        }

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

        var avgAbsAngle = window.Average(s => Math.Abs(s.Angle));
        OscillationDetected = zeroCrossings > 20 && avgAbsAngle > 5000;

        var nearZeroTorque = window.Where(s => Math.Abs(s.TorqueCommand) < 50).ToList();
        if (nearZeroTorque.Count > 10)
        {
            var diffs = nearZeroTorque.Zip(nearZeroTorque.Skip(1), (a, b) => Math.Abs(b.Angle - a.Angle)).ToList();
            var avgDiff = diffs.Count > 0 ? diffs.Average() : 0;
            NoiseDetected = avgDiff > 50;
        }

        if (OscillationDetected)
        {
            Insights = "Oscillation detected: increase damper/friction or reduce overall gain.";
        }
        else if (ClippingPercent > 30)
        {
            Insights = "Frequent clipping: reduce overall strength or game gain.";
        }
        else if (NoiseDetected)
        {
            Insights = "Encoder noise detected: check wiring or enable input averaging.";
        }
        else
        {
            Insights = string.Empty;
        }
    }

    private void OnDeviceChanged(DeviceInfo? info)
    {
        if (info == null)
        {
            CanShowTelemetry = false;
            TelemetryNotice = "Connect a device to see telemetry.";
            return;
        }

        if (!info.SupportsTelemetry)
        {
            CanShowTelemetry = false;
            TelemetryNotice = "Firmware does not expose telemetry over serial.";
        }
        else
        {
            CanShowTelemetry = true;
            TelemetryNotice = string.Empty;
        }
    }
}

