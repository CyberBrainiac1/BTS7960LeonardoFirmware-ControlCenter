namespace ArduinoFFBControlCenter.Models;

public class CalibrationStepResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public AxisSampleWindow? Samples { get; set; }
    public double? DetectedValue { get; set; }
}
