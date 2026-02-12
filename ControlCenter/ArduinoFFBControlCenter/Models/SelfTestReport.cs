namespace ArduinoFFBControlCenter.Models;

public class SelfTestReport
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public bool EncoderDirectionOk { get; set; }
    public bool MotorDirectionTested { get; set; }
    public bool MotorDirectionOk { get; set; }
    public bool ButtonsOk { get; set; }
    public bool EndstopTested { get; set; }
    public bool EndstopOk { get; set; }
    public string Notes { get; set; } = string.Empty;
}
