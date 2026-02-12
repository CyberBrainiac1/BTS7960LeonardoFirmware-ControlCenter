using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class PedalTelemetryService
{
    private readonly HidWheelService _hid;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;

    public PedalTelemetryService(HidWheelService hid, SettingsService settingsService, AppSettings settings)
    {
        _hid = hid;
        _settingsService = settingsService;
        _settings = settings;
    }

    public PedalAxisMapping Mapping => _settings.PedalMapping ?? new PedalAxisMapping();
    public PedalCalibration Calibration => _settings.PedalCalibration ?? new PedalCalibration();

    public PedalSample GetSample()
    {
        var sample = new PedalSample();
        sample.RawThrottle = GetRaw(Mapping.ThrottleSource);
        sample.RawBrake = GetRaw(Mapping.BrakeSource);
        sample.RawClutch = GetRaw(Mapping.ClutchSource);
        sample.Throttle = Mapping.ThrottleSource == PedalAxisSource.None ? null : Normalize(sample.RawThrottle, Calibration.Throttle);
        sample.Brake = Mapping.BrakeSource == PedalAxisSource.None ? null : Normalize(sample.RawBrake, Calibration.Brake);
        sample.Clutch = Mapping.ClutchSource == PedalAxisSource.None ? null : Normalize(sample.RawClutch, Calibration.Clutch);
        return sample;
    }

    public int GetRaw(PedalAxisSource source)
    {
        return source switch
        {
            PedalAxisSource.Y => _hid.AxisY,
            PedalAxisSource.Z => _hid.AxisZ,
            PedalAxisSource.Rx => _hid.AxisRx,
            PedalAxisSource.Ry => _hid.AxisRy,
            PedalAxisSource.Rz => _hid.AxisRz,
            PedalAxisSource.Slider0 => _hid.Slider0,
            PedalAxisSource.Slider1 => _hid.Slider1,
            _ => 0
        };
    }

    public void UpdateMapping(PedalAxisMapping mapping)
    {
        _settings.PedalMapping = mapping;
        _settingsService.Save(_settings);
    }

    public void UpdateCalibration(PedalCalibration calibration)
    {
        _settings.PedalCalibration = calibration;
        _settingsService.Save(_settings);
    }

    public async Task<PedalAxisSource?> DetectAxisAsync(TimeSpan duration, CancellationToken ct)
    {
        var sources = new[]
        {
            PedalAxisSource.Y,
            PedalAxisSource.Z,
            PedalAxisSource.Rx,
            PedalAxisSource.Ry,
            PedalAxisSource.Rz,
            PedalAxisSource.Slider0,
            PedalAxisSource.Slider1
        };

        var mins = sources.ToDictionary(s => s, _ => int.MaxValue);
        var maxs = sources.ToDictionary(s => s, _ => int.MinValue);

        var stop = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < stop && !ct.IsCancellationRequested)
        {
            foreach (var s in sources)
            {
                var val = GetRaw(s);
                mins[s] = Math.Min(mins[s], val);
                maxs[s] = Math.Max(maxs[s], val);
            }
            await Task.Delay(10, ct);
        }

        PedalAxisSource? best = null;
        var bestRange = 0;
        foreach (var s in sources)
        {
            var range = maxs[s] - mins[s];
            if (range > bestRange)
            {
                bestRange = range;
                best = s;
            }
        }

        return bestRange > 500 ? best : null;
    }

    private static double? Normalize(int raw, PedalAxisCalibration cal)
    {
        var range = cal.Max - cal.Min;
        if (range == 0)
        {
            return null;
        }
        var value = (raw - cal.Min) / (double)range;
        value = Math.Clamp(value, 0, 1);
        if (cal.Inverted)
        {
            value = 1 - value;
        }
        return value;
    }
}
