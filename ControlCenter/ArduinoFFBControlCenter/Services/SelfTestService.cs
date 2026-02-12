using System;
using System.Linq;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class SelfTestService
{
    private readonly HidWheelService _hid;
    private readonly HidTelemetryService _hidTelemetry;
    private readonly LoggerService _logger;
    private readonly FfbTestService _ffb;

    public SelfTestService(HidWheelService hid, HidTelemetryService hidTelemetry, LoggerService logger)
    {
        _hid = hid;
        _hidTelemetry = hidTelemetry;
        _logger = logger;
        _ffb = new FfbTestService(_logger);
    }

    public async Task<SelfTestReport> RunAsync(CancellationToken ct)
    {
        var report = new SelfTestReport();

        // Encoder direction: ask user to turn right and check delta.
        try
        {
            var raw = await _hidTelemetry.SampleRawAsync(TimeSpan.FromSeconds(1.5), ct);
            if (raw.Count >= 2)
            {
                var delta = raw.Last() - raw.First();
                report.EncoderDirectionOk = delta > 0;
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Encoder test failed: {ex.Message}");
        }

        // Button test: detect any press within 2 seconds
        var initial = _hid.Buttons.ToArray();
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (_hid.Buttons.Where((t, i) => t != initial[i]).Any(b => b))
            {
                report.ButtonsOk = true;
                break;
            }
            await Task.Delay(100, ct);
        }

        // Motor direction test via DirectInput constant force (low power).
        try
        {
            var devices = _hid.EnumerateDevices();
            var target = devices.FirstOrDefault(d => d.ProductName.Contains("Arduino", StringComparison.OrdinalIgnoreCase)) ?? devices.FirstOrDefault();
            if (target != null && _ffb.Attach(target.InstanceGuid))
            {
                report.MotorDirectionTested = true;
                var before = _hid.WheelAngle;
                _ffb.PlayConstant(1500);
                await Task.Delay(600, ct);
                _ffb.StopAll();
                await Task.Delay(200, ct);
                var after = _hid.WheelAngle;
                var delta = after - before;
                if (Math.Abs(delta) < 200)
                {
                    report.MotorDirectionOk = false;
                    AppendNote(report, "Motor direction test inconclusive: no movement detected.");
                }
                else
                {
                    report.MotorDirectionOk = delta > 0;
                    if (!report.MotorDirectionOk)
                    {
                        AppendNote(report, "Motor direction appears reversed. Swap motor leads or invert in firmware.");
                    }
                }
            }
            else
            {
                AppendNote(report, "Motor direction test skipped: FFB device not detected.");
            }
        }
        catch (Exception ex)
        {
            AppendNote(report, $"Motor direction test failed: {ex.Message}");
        }

        // Endstop test (range check).
        try
        {
            if (_hid.IsAttached)
            {
                report.EndstopTested = true;
                var window = await _hidTelemetry.SampleAxisWindowAsync(TimeSpan.FromSeconds(2), ct);
                var range = window.Max - window.Min;
                report.EndstopOk = range >= 1.6;
                if (!report.EndstopOk)
                {
                    AppendNote(report, "Endstop test: turn wheel to both ends and retry.");
                }
            }
        }
        catch (Exception ex)
        {
            AppendNote(report, $"Endstop test failed: {ex.Message}");
        }

        return report;
    }

    private static void AppendNote(SelfTestReport report, string message)
    {
        if (string.IsNullOrWhiteSpace(report.Notes))
        {
            report.Notes = message;
        }
        else
        {
            report.Notes += " " + message;
        }
    }
}
