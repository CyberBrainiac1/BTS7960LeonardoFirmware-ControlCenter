using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;
using Xunit;

namespace ArduinoFFBControlCenter.Tests;

public class SettingsDiffTests
{
    [Fact]
    public void AreEquivalent_ReturnsTrue_WhenConfigsMatch()
    {
        var a = new FfbConfig { RotationDeg = 900, GeneralGain = 100 };
        var b = new FfbConfig { RotationDeg = 900, GeneralGain = 100 };

        Assert.True(SettingsDiff.AreEquivalent(a, b));
    }

    [Fact]
    public void AreEquivalent_ReturnsFalse_WhenConfigsDiffer()
    {
        var a = new FfbConfig { RotationDeg = 900, GeneralGain = 100 };
        var b = new FfbConfig { RotationDeg = 900, GeneralGain = 80 };

        Assert.False(SettingsDiff.AreEquivalent(a, b));
    }
}
