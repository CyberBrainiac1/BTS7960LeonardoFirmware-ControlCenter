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
    public FfbConfig? LastTuningConfig { get; set; } = new();
    public FfbCurveSettings? LastCurve { get; set; } = new();
    public AdvancedTuningSettings? LastAdvanced { get; set; } = new AdvancedTuningSettings
    {
        SteeringScale = 100,
        MinForceBoost = 0,
        SlewRate = 0,
        Smoothing = 0,
        NotchFilter = 0,
        LowPassFilter = 0,
        SoftLockStrength = 80,
        SoftLockRange = 100,
        OscillationGuardEnabled = true,
        OscillationGuardStrength = 20,
        DampingBoost = 10,
        FrictionBoost = 10
    };
    public CalibrationRecord? LastCalibration { get; set; }
    public bool BypassCalibrationWarning { get; set; }
    public bool DashboardEnabled { get; set; }
    public int DashboardPort { get; set; } = 10500;
    public string? DashboardPin { get; set; }
    public bool DashboardRequirePin { get; set; } = true;
    public bool DashboardAdvancedRemote { get; set; }
    public string? OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string? OllamaModel { get; set; }
    public bool OllamaIncludeScreenCapture { get; set; } = true;
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
        AppSettings settings;
        if (!File.Exists(AppPaths.SettingsFile))
        {
            settings = new AppSettings();
            EnsureDefaults(settings);
            return settings;
        }
        var json = File.ReadAllText(AppPaths.SettingsFile);
        settings = JsonSerializer.Deserialize<AppSettings>(json, _options) ?? new AppSettings();
        EnsureDefaults(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        EnsureDefaults(settings);
        var json = JsonSerializer.Serialize(settings, _options);
        File.WriteAllText(AppPaths.SettingsFile, json);
        Saved?.Invoke(settings);
    }

    private static void EnsureDefaults(AppSettings settings)
    {
        settings.LastTuningConfig ??= new FfbConfig();
        settings.LastCurve ??= new FfbCurveSettings();
        settings.LastAdvanced ??= new AdvancedTuningSettings
        {
            SteeringScale = 100,
            SoftLockStrength = 80,
            SoftLockRange = 100,
            OscillationGuardEnabled = true,
            OscillationGuardStrength = 20,
            DampingBoost = 10,
            FrictionBoost = 10
        };
        settings.PedalMapping ??= new PedalAxisMapping();
        settings.PedalCalibration ??= new PedalCalibration();
        if (string.IsNullOrWhiteSpace(settings.OllamaEndpoint))
        {
            settings.OllamaEndpoint = "http://localhost:11434";
        }

        if (settings.DashboardPort <= 0 || settings.DashboardPort > 65535)
        {
            settings.DashboardPort = 10500;
        }
    }
}
