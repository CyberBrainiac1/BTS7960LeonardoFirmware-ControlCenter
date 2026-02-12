namespace ArduinoFFBControlCenter.Models;

public class AdvancedTuningSettings
{
    public int SteeringScale { get; set; } = 100;
    public int MinForceBoost { get; set; }
    public int SlewRate { get; set; }
    public int Smoothing { get; set; }
    public int NotchFilter { get; set; }
    public int LowPassFilter { get; set; }
    public int SoftLockStrength { get; set; }
    public int SoftLockRange { get; set; }
    public bool OscillationGuardEnabled { get; set; }
    public int OscillationGuardStrength { get; set; }
    public int DampingBoost { get; set; }
    public int FrictionBoost { get; set; }
}
