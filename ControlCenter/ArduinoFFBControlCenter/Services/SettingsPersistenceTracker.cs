using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class SettingsPersistenceTracker
{
    public SettingsPersistenceState State { get; private set; } = SettingsPersistenceState.Unknown;
    public FfbConfig? LastSavedToWheel { get; private set; }
    public FfbConfig? LastApplied { get; private set; }
    public string? LastPcProfile { get; private set; }

    public void MarkDeviceLoaded(FfbConfig config)
    {
        LastSavedToWheel = Clone(config);
        LastApplied = Clone(config);
        State = SettingsPersistenceState.SavedToWheel;
    }

    public void MarkApplied(FfbConfig config)
    {
        LastApplied = Clone(config);
        if (LastSavedToWheel != null && SettingsDiff.AreEquivalent(LastSavedToWheel, config))
        {
            State = SettingsPersistenceState.SavedToWheel;
            return;
        }
        State = SettingsPersistenceState.UnsavedChanges;
    }

    public void MarkSavedToWheel(FfbConfig config)
    {
        LastSavedToWheel = Clone(config);
        LastApplied = Clone(config);
        State = SettingsPersistenceState.SavedToWheel;
    }

    public void MarkSavedToPc(string profileName)
    {
        LastPcProfile = profileName;
        State = SettingsPersistenceState.SavedToPc;
    }

    private static FfbConfig Clone(FfbConfig cfg)
    {
        return new FfbConfig
        {
            RotationDeg = cfg.RotationDeg,
            GeneralGain = cfg.GeneralGain,
            DamperGain = cfg.DamperGain,
            FrictionGain = cfg.FrictionGain,
            InertiaGain = cfg.InertiaGain,
            SpringGain = cfg.SpringGain,
            ConstantGain = cfg.ConstantGain,
            PeriodicGain = cfg.PeriodicGain,
            CenterGain = cfg.CenterGain,
            StopGain = cfg.StopGain,
            MinTorque = cfg.MinTorque,
            BrakePressureOrBalance = cfg.BrakePressureOrBalance,
            DesktopEffectsByte = cfg.DesktopEffectsByte,
            MaxTorque = cfg.MaxTorque,
            EncoderCpr = cfg.EncoderCpr,
            PwmState = cfg.PwmState
        };
    }
}
