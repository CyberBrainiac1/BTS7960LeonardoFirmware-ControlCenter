using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class HidTelemetryService
{
    private readonly HidWheelService _hid;
    private readonly LoggerService _logger;

    public HidTelemetryService(HidWheelService hid, LoggerService logger)
    {
        _hid = hid;
        _logger = logger;
    }

    public bool IsAttached => _hid.IsAttached;

    public AxisSampleWindow SampleAxisWindow(TimeSpan duration, CancellationToken ct)
    {
        var samples = new List<int>();
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start) < duration && !ct.IsCancellationRequested)
        {
            samples.Add((int)_hid.WheelAngle);
            Thread.Sleep(10);
        }

        return BuildWindow(samples);
    }

    public async Task<AxisSampleWindow> SampleAxisWindowAsync(TimeSpan duration, CancellationToken ct)
    {
        var samples = new List<int>();
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start) < duration && !ct.IsCancellationRequested)
        {
            samples.Add((int)_hid.WheelAngle);
            await Task.Delay(10, ct);
        }

        return BuildWindow(samples);
    }

    public async Task<IReadOnlyList<int>> SampleRawAsync(TimeSpan duration, CancellationToken ct)
    {
        var samples = new List<int>();
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start) < duration && !ct.IsCancellationRequested)
        {
            samples.Add((int)_hid.WheelAngle);
            await Task.Delay(10, ct);
        }

        return samples;
    }

    public AxisSampleWindow BuildWindow(IReadOnlyList<int> samples)
    {
        if (samples.Count == 0)
        {
            return new AxisSampleWindow(0, 0, 0, 0, 0);
        }

        var range = AxisNormalization.DetectRange(samples);
        var normalized = samples.Select(s => AxisNormalization.Normalize(s, range)).ToList();
        var mean = normalized.Average();
        var min = normalized.Min();
        var max = normalized.Max();
        var variance = normalized.Select(v => (v - mean) * (v - mean)).Average();
        var std = Math.Sqrt(variance);
        return new AxisSampleWindow(mean, min, max, std, samples.Count);
    }
}
