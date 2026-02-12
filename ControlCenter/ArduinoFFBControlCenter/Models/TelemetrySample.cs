namespace ArduinoFFBControlCenter.Models;

public class TelemetrySample
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double Angle { get; set; }
    public double Velocity { get; set; }
    public double TorqueCommand { get; set; }
    public bool Clipping { get; set; }
    public double LoopDtMs { get; set; }
}
