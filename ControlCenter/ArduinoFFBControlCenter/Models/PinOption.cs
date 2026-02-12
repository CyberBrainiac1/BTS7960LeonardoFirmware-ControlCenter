namespace ArduinoFFBControlCenter.Models;

public class PinOption
{
    public string Name { get; set; } = string.Empty;
    public bool IsPwm { get; set; }
    public bool IsInterrupt { get; set; }
    public bool IsAnalog { get; set; }
}
