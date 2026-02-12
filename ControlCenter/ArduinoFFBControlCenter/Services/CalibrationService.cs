using ArduinoFFBControlCenter.Helpers;
using System.Linq;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class CalibrationService
{
    private readonly LoggerService _logger;
    private readonly DeviceStateService _deviceState;
    private readonly DeviceCapabilitiesService _caps;
    private readonly HidTelemetryService _hidTelemetry;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly DeviceSettingsService _deviceSettings;

    public CalibrationAssessment CurrentAssessment { get; private set; } = new();

    public event Action<CalibrationAssessment>? StatusChanged;

    public CalibrationService(LoggerService logger,
        DeviceStateService deviceState,
        DeviceCapabilitiesService caps,
        HidTelemetryService hidTelemetry,
        SettingsService settingsService,
        AppSettings settings,
        DeviceSettingsService deviceSettings)
    {
        _logger = logger;
        _deviceState = deviceState;
        _caps = caps;
        _hidTelemetry = hidTelemetry;
        _settingsService = settingsService;
        _settings = settings;
        _deviceSettings = deviceSettings;
    }

    public async Task<CalibrationAssessment> AssessAsync(FfbConfig? config, CancellationToken ct)
    {
        var device = _deviceState.CurrentDevice;
        var assessment = new CalibrationAssessment();

        if (device == null)
        {
            assessment.IsSupported = false;
            assessment.NeedsCalibration = false;
            assessment.Reason = "No device connected.";
            UpdateAssessment(assessment);
            return assessment;
        }

        if (device.IsDemo)
        {
            assessment.IsSupported = true;
            assessment.NeedsCalibration = false;
            assessment.Reason = "Demo mode.";
            UpdateAssessment(assessment);
            return assessment;
        }

        var caps = _caps.GetCapabilities(device);
        assessment.IsSupported = caps.SupportsSerialConfig || device.SupportsTelemetry || _hidTelemetry.IsAttached;

        if (caps.SupportsCalibrationInfo && device.CalibrationInfo != null)
        {
            var cal = device.CalibrationInfo;
            assessment.StoredOnWheel = true;
            if (!cal.Present)
            {
                assessment.NeedsCalibration = true;
                assessment.Reason = "Calibration not stored on wheel.";
                UpdateAssessment(assessment);
                return assessment;
            }

            if (config != null && Math.Abs(config.RotationDeg - cal.RotationDeg) > 1)
            {
                assessment.NeedsCalibration = true;
                assessment.Reason = "Rotation changed since calibration.";
                UpdateAssessment(assessment);
                return assessment;
            }

            if (config != null && (config.RotationDeg < 180 || config.RotationDeg > 2000))
            {
                assessment.NeedsCalibration = true;
                assessment.Reason = "Rotation range looks invalid.";
                UpdateAssessment(assessment);
                return assessment;
            }

            assessment.NeedsCalibration = false;
            assessment.Reason = "Calibration stored on wheel.";
            UpdateAssessment(assessment);
            return assessment;
        }

        if (config != null && (config.RotationDeg < 180 || config.RotationDeg > 2000))
        {
            assessment.NeedsCalibration = true;
            assessment.Reason = "Rotation range looks invalid.";
            UpdateAssessment(assessment);
            return assessment;
        }

        if (_settings.LastCalibration != null && _settings.LastCalibration.RotationDeg > 0 && config != null)
        {
            if (Math.Abs(_settings.LastCalibration.RotationDeg - config.RotationDeg) > 1)
            {
                assessment.NeedsCalibration = true;
                assessment.Reason = "Rotation changed since last calibration.";
                UpdateAssessment(assessment);
                return assessment;
            }
        }

        if (_settings.LastCalibration != null && _settings.LastCalibration.EncoderCpr > 0 && config != null)
        {
            if (Math.Abs(_settings.LastCalibration.EncoderCpr - config.EncoderCpr) > 0)
            {
                assessment.NeedsCalibration = true;
                assessment.Reason = "Encoder CPR changed since last calibration.";
                UpdateAssessment(assessment);
                return assessment;
            }
        }

        if (!_hidTelemetry.IsAttached)
        {
            assessment.IsSupported = false;
            assessment.NeedsCalibration = false;
            assessment.Reason = "HID input not detected.";
            UpdateAssessment(assessment);
            return assessment;
        }

        var samples = await _hidTelemetry.SampleAxisWindowAsync(TimeSpan.FromSeconds(2), ct);
        assessment = CalibrationInference.Assess(samples);
        assessment.StoredOnWheel = _settings.LastCalibration?.StoredOnWheel ?? false;
        UpdateAssessment(assessment);
        return assessment;
    }

    public async Task<CalibrationStepResult> CaptureCenterAsync(CancellationToken ct)
    {
        var result = new CalibrationStepResult();
        var device = _deviceState.CurrentDevice;
        if (device == null)
        {
            result.Success = false;
            result.Message = "No device connected.";
            return result;
        }

        var caps = _caps.GetCapabilities(device);
        var samples = await _hidTelemetry.SampleAxisWindowAsync(TimeSpan.FromSeconds(1.5), ct);
        result.Samples = samples;
        result.DetectedValue = samples.Mean;

        if (caps.SupportsSerialConfig && !device.IsDemo)
        {
            try
            {
                await _deviceSettings.CenterAsync(ct);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Center command failed: {ex.Message}";
                return result;
            }
        }

        var verify = CalibrationInference.Assess(await _hidTelemetry.SampleAxisWindowAsync(TimeSpan.FromSeconds(1), ct));
        var ok = !verify.NeedsCalibration;
        result.Success = ok;
        result.Message = ok ? "Center captured." : "Center still offset. Try again.";

        UpdateCalibrationRecord(device, samples.Mean, storedOnWheel: false, rotationDeg: device.CalibrationInfo?.RotationDeg);
        return result;
    }

    public async Task<CalibrationStepResult> DetectDirectionAsync(CancellationToken ct)
    {
        var raw = await _hidTelemetry.SampleRawAsync(TimeSpan.FromSeconds(1.5), ct);
        var window = _hidTelemetry.BuildWindow(raw.ToList());
        var result = new CalibrationStepResult
        {
            Samples = window,
            DetectedValue = window.Mean
        };

        if (raw.Count < 4)
        {
            result.Success = false;
            result.Message = "No movement detected. Turn the wheel right and try again.";
            return result;
        }

        var delta = raw.Last() - raw.First();
        if (Math.Abs(delta) < 200)
        {
            result.Success = false;
            result.Message = "No movement detected. Turn the wheel right and try again.";
            return result;
        }

        result.Success = true;
        result.Message = delta > 0 ? "Direction looks correct." : "Direction may be inverted.";
        result.DetectedValue = delta;
        return result;
    }

    public async Task<CalibrationStepResult> VerifyRotationAsync(CancellationToken ct)
    {
        var samples = await _hidTelemetry.SampleAxisWindowAsync(TimeSpan.FromSeconds(3), ct);
        var result = new CalibrationStepResult { Samples = samples };

        var range = samples.Max - samples.Min;
        if (range >= 1.6)
        {
            result.Success = true;
            result.Message = "Full range detected.";
        }
        else
        {
            result.Success = false;
            result.Message = "Range looks short. Turn to endstops and try again.";
        }

        return result;
    }

    public void UpdateCalibrationRecord(DeviceInfo device, double centerNormalized, bool storedOnWheel, int? rotationDeg = null, bool? inverted = null, int? encoderCpr = null)
    {
        var record = _settings.LastCalibration ?? new CalibrationRecord();
        record.DeviceId = device.Vid != null && device.Pid != null ? $"{device.Vid}:{device.Pid}" : device.Port;
        record.FirmwareVersion = device.FirmwareVersion;
        record.CenterOffsetRaw = (int)(centerNormalized * 32767);
        if (rotationDeg.HasValue && rotationDeg.Value > 0)
        {
            record.RotationDeg = rotationDeg.Value;
        }
        if (encoderCpr.HasValue && encoderCpr.Value > 0)
        {
            record.EncoderCpr = encoderCpr.Value;
        }
        if (inverted.HasValue)
        {
            record.Inverted = inverted.Value;
        }
        record.StoredOnWheel = storedOnWheel;
        record.TimestampUtc = DateTime.UtcNow;
        _settings.LastCalibration = record;
        _settingsService.Save(_settings);
    }

    public void UpdateCalibrationMetadata(DeviceInfo device, int? rotationDeg, bool? inverted, bool? storedOnWheel = null, int? encoderCpr = null)
    {
        var record = _settings.LastCalibration ?? new CalibrationRecord();
        record.DeviceId = device.Vid != null && device.Pid != null ? $"{device.Vid}:{device.Pid}" : device.Port;
        record.FirmwareVersion = device.FirmwareVersion;
        if (rotationDeg.HasValue && rotationDeg.Value > 0)
        {
            record.RotationDeg = rotationDeg.Value;
        }
        if (encoderCpr.HasValue && encoderCpr.Value > 0)
        {
            record.EncoderCpr = encoderCpr.Value;
        }
        if (inverted.HasValue)
        {
            record.Inverted = inverted.Value;
        }
        if (storedOnWheel.HasValue)
        {
            record.StoredOnWheel = storedOnWheel.Value;
        }
        record.TimestampUtc = DateTime.UtcNow;
        _settings.LastCalibration = record;
        _settingsService.Save(_settings);
    }

    private void UpdateAssessment(CalibrationAssessment assessment)
    {
        CurrentAssessment = assessment;
        StatusChanged?.Invoke(assessment);
    }
}
