namespace ArduinoFFBControlCenter.Models;

public class SimTelemetrySample
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public double? VehicleSpeed { get; set; }
    public int? Gear { get; set; }
    public int? Rpm { get; set; }
    public double? Throttle { get; set; }
    public double? Brake { get; set; }
    public double? Clutch { get; set; }
}
