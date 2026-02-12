using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;
using ArduinoFFBControlCenter.Helpers;
using Xunit;

namespace ArduinoFFBControlCenter.Tests;

public class SnapshotServiceTests
{
    [Fact]
    public void CreateSnapshot_WritesSnapshotAndLoads()
    {
        var logger = new LoggerService();
        var service = new SnapshotService(logger);
        var entry = new SnapshotEntry
        {
            Kind = SnapshotKind.Manual,
            Label = "Test snapshot",
            Config = new FfbConfig { GeneralGain = 90 }
        };
        var samples = new List<TelemetrySample>
        {
            new() { Timestamp = DateTime.UtcNow, Angle = 123, Velocity = 1.2, TorqueCommand = 45, Clipping = false, LoopDtMs = 2.0 }
        };

        service.CreateSnapshot(entry, samples);
        var list = service.LoadSnapshots();
        Assert.Contains(list, s => s.Label == "Test snapshot");

        // Cleanup most recent snapshot directory
        var latestDir = Directory.GetDirectories(AppPaths.SnapshotsPath).OrderByDescending(d => d).FirstOrDefault();
        if (latestDir != null)
        {
            Directory.Delete(latestDir, true);
        }
    }
}
