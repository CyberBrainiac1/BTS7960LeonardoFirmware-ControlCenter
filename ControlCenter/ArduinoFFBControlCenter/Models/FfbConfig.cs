namespace ArduinoFFBControlCenter.Models;

public class FfbConfig
{
    public int RotationDeg { get; set; } = 1080;
    public int GeneralGain { get; set; } = 100;
    public int DamperGain { get; set; } = 50;
    public int FrictionGain { get; set; } = 50;
    public int ConstantGain { get; set; } = 100;
    public int PeriodicGain { get; set; } = 100;
    public int SpringGain { get; set; } = 100;
    public int InertiaGain { get; set; } = 50;
    public int CenterGain { get; set; } = 70;
    public int StopGain { get; set; } = 100;
    public int MinTorque { get; set; } = 0;
    public int BrakePressureOrBalance { get; set; } = 128;
    public int DesktopEffectsByte { get; set; } = 1;
    public int MaxTorque { get; set; } = 2047;
    public int EncoderCpr { get; set; } = 2400;
    public int PwmState { get; set; } = 9;
}
