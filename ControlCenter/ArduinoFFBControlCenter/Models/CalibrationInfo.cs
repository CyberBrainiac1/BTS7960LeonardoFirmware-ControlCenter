namespace ArduinoFFBControlCenter.Models;

public class CalibrationInfo
{
    public bool Present { get; set; }
    public int RotationDeg { get; set; }
    public int CenterOffsetRaw { get; set; }
    public bool Inverted { get; set; }
}
