using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;
using Xunit;

namespace ArduinoFFBControlCenter.Tests;

public class WheelProfileServiceTests
{
    [Fact]
    public void ExportImport_RoundTripProfile()
    {
        var layoutService = new DashboardLayoutService();
        var service = new WheelProfileService(layoutService);
        var profile = new Profile
        {
            Name = "TestProfile",
            Notes = "Unit test",
            Config = new FfbConfig { GeneralGain = 88, RotationDeg = 900 }
        };

        var tempFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.wheelprofile");
        service.Export(profile, tempFile, null, includeFirmwareHex: false);

        var imported = service.Import(tempFile);
        Assert.Equal("TestProfile", imported.Manifest.Name);
        Assert.NotNull(imported.Profile);
        Assert.Equal(88, imported.Profile!.Config.GeneralGain);

        File.Delete(tempFile);
    }
}
