using System;
using System.Linq;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class DashboardTelemetryService
{
    private readonly TelemetryService _telemetry;
    private readonly DeviceStateService _deviceState;
    private readonly CalibrationService _calibration;
    private readonly DeviceSettingsService _settings;
    private readonly TuningStateService _tuningState;
    private readonly SimTelemetryService _simTelemetry;
    private readonly PedalTelemetryService _pedals;

    public DashboardTelemetryService(
        TelemetryService telemetry,
        DeviceStateService deviceState,
        CalibrationService calibration,
        DeviceSettingsService settings,
        TuningStateService tuningState,
        SimTelemetryService simTelemetry,
        PedalTelemetryService pedals)
    {
        _telemetry = telemetry;
        _deviceState = deviceState;
        _calibration = calibration;
        _settings = settings;
        _tuningState = tuningState;
        _simTelemetry = simTelemetry;
        _pedals = pedals;
    }

    public DashboardTelemetryFrame GetSnapshot()
    {
        var sample = _telemetry.GetSamplesSnapshot().LastOrDefault();
        var stats = _telemetry.CurrentStats;
        var config = _tuningState.CurrentConfig;
        var rotation = config?.RotationDeg ?? 0;

        var angleRaw = sample?.Angle ?? 0;
        var angleNorm = NormalizeAngle(angleRaw);
        var angleDeg = rotation > 0 ? angleNorm * rotation / 2.0 : angleRaw;

        var sim = _simTelemetry.Provider.GetLatest();
        var pedalSample = _pedals.GetSample();

        return new DashboardTelemetryFrame
        {
            TimestampUtc = DateTime.UtcNow,
            WheelAngle = angleDeg,
            WheelAngleNorm = angleNorm,
            WheelVelocity = sample?.Velocity ?? 0,
            TorqueCommand = sample?.TorqueCommand ?? 0,
            ClippingPercent = stats.ClippingPercent,
            LoopDtMs = sample?.LoopDtMs ?? 0,
            TelemetryRateHz = stats.SampleRateHz,
            IsConnected = _deviceState.CurrentDevice != null,
            CalibrationStatus = _calibration.CurrentAssessment.IsSupported
                ? _calibration.CurrentAssessment.NeedsCalibration ? "Not calibrated" : "Calibrated"
                : "Unknown",
            SaveStatus = _settings.PersistenceState.ToString(),
            RotationDeg = rotation,
            VehicleSpeed = sim?.VehicleSpeed,
            Gear = sim?.Gear,
            Rpm = sim?.Rpm,
            Throttle = sim?.Throttle ?? pedalSample.Throttle,
            Brake = sim?.Brake ?? pedalSample.Brake,
            Clutch = sim?.Clutch ?? pedalSample.Clutch,
            SimProvider = _simTelemetry.Provider.Name
        };
    }

    private static double NormalizeAngle(double raw)
    {
        if (raw < 0)
        {
            return Math.Clamp(raw / 32768.0, -1, 1);
        }

        var norm = (raw - 32768.0) / 32768.0;
        return Math.Clamp(norm, -1, 1);
    }
}
