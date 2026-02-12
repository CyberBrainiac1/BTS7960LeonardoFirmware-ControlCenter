using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public static class PresetLibraryService
{
    public static List<PresetProfile> GetWheelPresets()
    {
        return new List<PresetProfile>
        {
            new PresetProfile
            {
                Name = "BTS7960 Safe Defaults",
                Description = "Conservative baseline for BTS7960 H-bridge.",
                Config = new FfbConfig
                {
                    GeneralGain = 100,
                    DamperGain = 40,
                    FrictionGain = 40,
                    InertiaGain = 30,
                    SpringGain = 90,
                    ConstantGain = 100,
                    PeriodicGain = 100,
                    CenterGain = 70,
                    StopGain = 100,
                    MinTorque = 0,
                    RotationDeg = 900
                }
            },
            new PresetProfile
            {
                Name = "BTS7960 Punchy",
                Description = "Stronger response, still safe for BTS7960.",
                Config = new FfbConfig
                {
                    GeneralGain = 120,
                    DamperGain = 35,
                    FrictionGain = 30,
                    InertiaGain = 25,
                    SpringGain = 100,
                    ConstantGain = 120,
                    PeriodicGain = 120,
                    CenterGain = 60,
                    StopGain = 120,
                    MinTorque = 0,
                    RotationDeg = 900
                }
            }
        };
    }

    public static List<PresetProfile> GetGamePresets()
    {
        return new List<PresetProfile>
        {
            new PresetProfile
            {
                Name = "Assetto Corsa (Baseline)",
                Description = "Balanced profile for AC. Adjust in-game gain to taste.",
                Config = new FfbConfig
                {
                    GeneralGain = 90,
                    DamperGain = 40,
                    FrictionGain = 30,
                    InertiaGain = 25,
                    SpringGain = 80,
                    ConstantGain = 90,
                    PeriodicGain = 90,
                    CenterGain = 60,
                    StopGain = 100,
                    MinTorque = 0,
                    RotationDeg = 900
                }
            },
            new PresetProfile
            {
                Name = "ACC (Baseline)",
                Description = "Smooth, stable profile for ACC.",
                Config = new FfbConfig
                {
                    GeneralGain = 85,
                    DamperGain = 45,
                    FrictionGain = 35,
                    InertiaGain = 25,
                    SpringGain = 75,
                    ConstantGain = 85,
                    PeriodicGain = 85,
                    CenterGain = 60,
                    StopGain = 100,
                    MinTorque = 0,
                    RotationDeg = 900
                }
            },
            new PresetProfile
            {
                Name = "iRacing (Baseline)",
                Description = "Direct feel with controlled damping.",
                Config = new FfbConfig
                {
                    GeneralGain = 95,
                    DamperGain = 40,
                    FrictionGain = 25,
                    InertiaGain = 20,
                    SpringGain = 80,
                    ConstantGain = 95,
                    PeriodicGain = 95,
                    CenterGain = 55,
                    StopGain = 110,
                    MinTorque = 0,
                    RotationDeg = 900
                }
            },
            new PresetProfile
            {
                Name = "rFactor2 (Baseline)",
                Description = "Higher damping for stability.",
                Config = new FfbConfig
                {
                    GeneralGain = 90,
                    DamperGain = 50,
                    FrictionGain = 35,
                    InertiaGain = 30,
                    SpringGain = 85,
                    ConstantGain = 90,
                    PeriodicGain = 90,
                    CenterGain = 65,
                    StopGain = 110,
                    MinTorque = 0,
                    RotationDeg = 900
                }
            }
        };
    }
}
