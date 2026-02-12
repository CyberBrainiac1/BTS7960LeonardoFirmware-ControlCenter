namespace ArduinoFFBControlCenter.Models;

public enum PedalAxisSource
{
    None,
    Y,
    Z,
    Rx,
    Ry,
    Rz,
    Slider0,
    Slider1
}

public class PedalAxisMapping
{
    public PedalAxisSource ThrottleSource { get; set; } = PedalAxisSource.Y;
    public PedalAxisSource BrakeSource { get; set; } = PedalAxisSource.Z;
    public PedalAxisSource ClutchSource { get; set; } = PedalAxisSource.Slider0;
}

public class PedalAxisCalibration
{
    public int Min { get; set; } = 0;
    public int Max { get; set; } = 65535;
    public bool Inverted { get; set; }
}

public class PedalCalibration
{
    public PedalAxisCalibration Throttle { get; set; } = new();
    public PedalAxisCalibration Brake { get; set; } = new();
    public PedalAxisCalibration Clutch { get; set; } = new();
}

public class PedalSample
{
    public double? Throttle { get; set; }
    public double? Brake { get; set; }
    public double? Clutch { get; set; }
    public int RawThrottle { get; set; }
    public int RawBrake { get; set; }
    public int RawClutch { get; set; }
}
