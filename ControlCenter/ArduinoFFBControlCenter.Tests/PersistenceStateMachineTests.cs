using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;
using Xunit;

namespace ArduinoFFBControlCenter.Tests;

public class PersistenceStateMachineTests
{
    [Fact]
    public void Tracker_Transitions_AsExpected()
    {
        var tracker = new SettingsPersistenceTracker();
        var baseCfg = new FfbConfig { RotationDeg = 900, GeneralGain = 100 };
        var changedCfg = new FfbConfig { RotationDeg = 900, GeneralGain = 80 };

        Assert.Equal(SettingsPersistenceState.Unknown, tracker.State);

        tracker.MarkDeviceLoaded(baseCfg);
        Assert.Equal(SettingsPersistenceState.SavedToWheel, tracker.State);

        tracker.MarkApplied(changedCfg);
        Assert.Equal(SettingsPersistenceState.UnsavedChanges, tracker.State);

        tracker.MarkSavedToWheel(changedCfg);
        Assert.Equal(SettingsPersistenceState.SavedToWheel, tracker.State);

        tracker.MarkSavedToPc("PC Backup");
        Assert.Equal(SettingsPersistenceState.SavedToPc, tracker.State);
    }
}
