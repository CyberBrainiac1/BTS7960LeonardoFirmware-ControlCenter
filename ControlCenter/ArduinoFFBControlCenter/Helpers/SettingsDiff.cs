using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Helpers;

public static class SettingsDiff
{
    public static bool AreEquivalent(FfbConfig a, FfbConfig b)
    {
        return a.RotationDeg == b.RotationDeg
               && a.GeneralGain == b.GeneralGain
               && a.DamperGain == b.DamperGain
               && a.FrictionGain == b.FrictionGain
               && a.InertiaGain == b.InertiaGain
               && a.SpringGain == b.SpringGain
               && a.ConstantGain == b.ConstantGain
               && a.PeriodicGain == b.PeriodicGain
               && a.CenterGain == b.CenterGain
               && a.StopGain == b.StopGain
               && a.MinTorque == b.MinTorque
               && a.BrakePressureOrBalance == b.BrakePressureOrBalance
               && a.MaxTorque == b.MaxTorque
               && a.EncoderCpr == b.EncoderCpr
               && a.PwmState == b.PwmState;
    }

    public static IReadOnlyList<string> DescribeDifferences(FfbConfig a, FfbConfig b)
    {
        var diffs = new List<string>();
        Add(diffs, "Rotation", a.RotationDeg, b.RotationDeg);
        Add(diffs, "General", a.GeneralGain, b.GeneralGain);
        Add(diffs, "Damper", a.DamperGain, b.DamperGain);
        Add(diffs, "Friction", a.FrictionGain, b.FrictionGain);
        Add(diffs, "Inertia", a.InertiaGain, b.InertiaGain);
        Add(diffs, "Spring", a.SpringGain, b.SpringGain);
        Add(diffs, "Constant", a.ConstantGain, b.ConstantGain);
        Add(diffs, "Periodic", a.PeriodicGain, b.PeriodicGain);
        Add(diffs, "Center", a.CenterGain, b.CenterGain);
        Add(diffs, "Endstop", a.StopGain, b.StopGain);
        Add(diffs, "MinTorque", a.MinTorque, b.MinTorque);
        Add(diffs, "Brake/Bal", a.BrakePressureOrBalance, b.BrakePressureOrBalance);
        Add(diffs, "MaxTorque", a.MaxTorque, b.MaxTorque);
        Add(diffs, "Encoder CPR", a.EncoderCpr, b.EncoderCpr);
        Add(diffs, "PWM", a.PwmState, b.PwmState);
        return diffs;
    }

    private static void Add(ICollection<string> diffs, string name, int a, int b)
    {
        if (a != b)
        {
            diffs.Add($"{name}: {a} -> {b}");
        }
    }
}
