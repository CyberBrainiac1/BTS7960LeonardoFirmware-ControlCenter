using ArduinoFFBControlCenter.Services;
using Xunit;

namespace ArduinoFFBControlCenter.Tests;

public class SettingsPersistenceTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsSettings()
    {
        var service = new SettingsService();
        var settings = new AppSettings
        {
            LastDeviceId = "VID:PID",
            LastFirmwareHex = "test.hex",
            BeginnerMode = false,
            KidMode = true
        };
        service.Save(settings);
        var loaded = service.Load();
        Assert.Equal("VID:PID", loaded.LastDeviceId);
        Assert.Equal("test.hex", loaded.LastFirmwareHex);
        Assert.False(loaded.BeginnerMode);
        Assert.True(loaded.KidMode);
    }
}
