namespace ArduinoFFBControlCenter.Models;

public class CalibrationRecord
{
    public string? DeviceId { get; set; }
    public string? FirmwareVersion { get; set; }
    public int RotationDeg { get; set; }
    public int EncoderCpr { get; set; }
    public int CenterOffsetRaw { get; set; }
    public bool Inverted { get; set; }
    public bool StoredOnWheel { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
