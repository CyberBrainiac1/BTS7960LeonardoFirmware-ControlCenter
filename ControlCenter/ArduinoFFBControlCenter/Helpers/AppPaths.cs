using System.IO;

namespace ArduinoFFBControlCenter.Helpers;

public static class AppPaths
{
    public static string AppDataRoot
    {
        get
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArduinoFFBControlCenter");
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public static string ProfilesPath => Path.Combine(AppDataRoot, "profiles");
    public static string WheelProfilesPath => Path.Combine(AppDataRoot, "wheelprofiles");
    public static string LogsPath => Path.Combine(AppDataRoot, "logs");
    public static string SettingsFile => Path.Combine(AppDataRoot, "settings.json");
    public static string SettingsBackupFile => Path.Combine(AppDataRoot, "settings-backup.json");
    public static string TelemetryBufferPath => Path.Combine(AppDataRoot, "telemetry");
    public static string DashboardLayoutFile => Path.Combine(AppDataRoot, "dashboard-layout.json");
    public static string WizardStateFile => Path.Combine(AppDataRoot, "wizard-state.json");
    public static string SnapshotsPath => Path.Combine(AppDataRoot, "snapshots");
}
