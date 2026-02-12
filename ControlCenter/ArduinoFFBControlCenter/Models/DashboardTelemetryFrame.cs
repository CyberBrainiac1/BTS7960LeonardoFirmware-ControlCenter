namespace ArduinoFFBControlCenter.Models;

public class DashboardTelemetryFrame
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public double WheelAngle { get; set; }
    public double WheelAngleNorm { get; set; }
    public double WheelVelocity { get; set; }
    public double TorqueCommand { get; set; }
    public double ClippingPercent { get; set; }
    public double LoopDtMs { get; set; }
    public double TelemetryRateHz { get; set; }
    public bool IsConnected { get; set; }
    public string CalibrationStatus { get; set; } = "Unknown";
    public string SaveStatus { get; set; } = "Unknown";
    public int RotationDeg { get; set; }

    public double? VehicleSpeed { get; set; }
    public int? Gear { get; set; }
    public int? Rpm { get; set; }
    public double? Throttle { get; set; }
    public double? Brake { get; set; }
    public double? Clutch { get; set; }
    public string SimProvider { get; set; } = "None";
}
