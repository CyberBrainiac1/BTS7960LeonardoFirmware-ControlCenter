using ArduinoFFBControlCenter.Models;
using System.Linq;

namespace ArduinoFFBControlCenter.Services;

/// <summary>
/// Samples wheel telemetry (HID angle + serial torque lines) at a fixed cadence.
/// Also computes rolling health stats for UI indicators.
/// </summary>
public class TelemetryService
{
    private readonly HidWheelService _hid;
    private readonly SerialDeviceService _serial;
    private readonly LoggerService _logger;
    private readonly object _lock = new();
    private readonly List<TelemetrySample> _samples = new();
    private const int MaxSamples = 12000; // ~60s at 200Hz
    private CancellationTokenSource? _cts;
    private double _lastAngle;
    private double _latestTorque;
    private int _maxTorque = 2047;
    private bool _demoMode;
    private int _torqueLineCount;
    private DateTime _torqueLineWindowStart = DateTime.UtcNow;
    private DateTime _lastStatUpdate = DateTime.UtcNow;

    public event Action? SamplesUpdated;
    public event Action<TelemetryStats>? StatsUpdated;

    public TelemetryStats CurrentStats { get; private set; } = new();

    public TelemetryService(HidWheelService hid, SerialDeviceService serial, LoggerService logger)
    {
        _hid = hid;
        _serial = serial;
        _logger = logger;
        _serial.TelemetryLineReceived += OnTorqueLine;
    }

    public void UpdateMaxTorque(int value)
    {
        _maxTorque = Math.Max(1, value);
    }

    public void SetDemoMode(bool enabled)
    {
        _demoMode = enabled;
    }

    public IReadOnlyList<TelemetrySample> GetSamplesSnapshot()
    {
        lock (_lock)
        {
            return _samples.ToList();
        }
    }

    public void Start()
    {
        // Idempotent start.
        if (_cts != null)
        {
            return;
        }
        _cts = new CancellationTokenSource();
        _lastAngle = _hid.WheelAngle;
        Task.Run(() => SampleLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private void OnTorqueLine(int torque)
    {
        _latestTorque = torque;
        _torqueLineCount++;
    }

    // Main sampler loop (~5ms cadence).
    private async Task SampleLoopAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var last = sw.Elapsed.TotalMilliseconds;

        while (!ct.IsCancellationRequested)
        {
            var now = sw.Elapsed.TotalMilliseconds;
            var dt = now - last;
            if (dt <= 0)
            {
                await Task.Delay(2, ct);
                continue;
            }

            var angle = _hid.WheelAngle;
            if (_demoMode)
            {
                var t = sw.Elapsed.TotalSeconds;
                angle = Math.Sin(t * 1.2) * 32767;
                _latestTorque = Math.Sin(t * 1.8) * _maxTorque;
            }

            var velocity = (angle - _lastAngle) / dt;
            _lastAngle = angle;

            var sample = new TelemetrySample
            {
                Timestamp = DateTime.UtcNow,
                Angle = angle,
                Velocity = velocity,
                TorqueCommand = _latestTorque,
                Clipping = Math.Abs(_latestTorque) >= _maxTorque * 0.9,
                LoopDtMs = dt
            };

            lock (_lock)
            {
                _samples.Add(sample);
                if (_samples.Count > MaxSamples)
                {
                    _samples.RemoveAt(0);
                }
            }

            SamplesUpdated?.Invoke();
            last = now;

            if ((DateTime.UtcNow - _lastStatUpdate).TotalMilliseconds > 1000)
            {
                UpdateStats();
                _lastStatUpdate = DateTime.UtcNow;
            }

            await Task.Delay(5, ct);
        }
    }

    // Computes per-second stats from recent sample window.
    private void UpdateStats()
    {
        TelemetrySample[] window;
        lock (_lock)
        {
            window = _samples.TakeLast(300).ToArray();
        }

        if (window.Length == 0)
        {
            return;
        }

        var avgDt = window.Average(s => s.LoopDtMs);
        var clip = window.Count(s => s.Clipping);
        var clipPct = (double)clip / window.Length * 100.0;

        var now = DateTime.UtcNow;
        var seconds = (now - _torqueLineWindowStart).TotalSeconds;
        if (seconds >= 1.0)
        {
            var rate = _torqueLineCount / seconds;
            _torqueLineCount = 0;
            _torqueLineWindowStart = now;
            CurrentStats.TorqueLineRateHz = rate;
            var expected = 100.0;
            var loss = expected > 0 ? Math.Max(0, (expected - rate) / expected * 100.0) : 0;
            CurrentStats.PacketLossPercent = loss;
        }

        CurrentStats.SampleRateHz = avgDt > 0 ? 1000.0 / avgDt : 0;
        CurrentStats.AvgLoopDtMs = avgDt;
        CurrentStats.ClippingPercent = clipPct;
        StatsUpdated?.Invoke(CurrentStats);
    }
}
