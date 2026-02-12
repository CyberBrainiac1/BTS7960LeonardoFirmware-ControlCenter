namespace ArduinoFFBControlCenter.Models;

public class CalibrationAssessment
{
    public bool NeedsCalibration { get; set; }
    public bool IsSupported { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool StoredOnWheel { get; set; }
}
