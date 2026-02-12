using System.Text.Json;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class AppSettings
{
    public string? LastPort { get; set; }
    public string? LastDeviceId { get; set; }
    public string? LastDeviceName { get; set; }
    public bool AutoConnect { get; set; } = true;
    public bool TelemetryEnabled { get; set; } = true;
    public string? LastFirmwareHex { get; set; }
    public string? LastKnownGoodHex { get; set; }
    public string? LastFlashStatus { get; set; }
    public DateTime? LastFlashUtc { get; set; }
    public string? LastProfileName { get; set; }
    public bool AutoApplyLastProfile { get; set; }
    public string? LastNavKey { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public FfbConfig? LastTuningConfig { get; set; }
    public FfbCurveSettings? LastCurve { get; set; }
    public AdvancedTuningSettings? LastAdvanced { get; set; }
    public CalibrationRecord? LastCalibration { get; set; }
    public bool BypassCalibrationWarning { get; set; }
    public bool DashboardEnabled { get; set; }
    public int DashboardPort { get; set; } = 10500;
    public string? DashboardPin { get; set; }
    public bool DashboardRequirePin { get; set; } = true;
    public bool DashboardAdvancedRemote { get; set; }
    public bool BeginnerMode { get; set; } = true;
    public bool KidMode { get; set; }
    public bool DemoMode { get; set; }
    public PedalAxisMapping PedalMapping { get; set; } = new();
    public PedalCalibration PedalCalibration { get; set; } = new();
}

public class SettingsService
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    public event Action<AppSettings>? Saved;

    public AppSettings Load()
    {
        if (!File.Exists(AppPaths.SettingsFile))
        {
            return new AppSettings();
        }
        var json = File.ReadAllText(AppPaths.SettingsFile);
        return JsonSerializer.Deserialize<AppSettings>(json, _options) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _options);
        File.WriteAllText(AppPaths.SettingsFile, json);
        Saved?.Invoke(settings);
    }
}
