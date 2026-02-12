using System.IO.Compression;
using System.Text.Json;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class DiagnosticsService
{
    private readonly LoggerService _logger;
    private readonly SettingsService _settingsService;

    public DiagnosticsService(LoggerService logger, SettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public string CreateSupportBundle(string destinationPath, DeviceInfo? device, FfbConfig? config, IEnumerable<TelemetrySample>? samples)
    {
        var tempDir = Path.Combine(AppPaths.AppDataRoot, "support-temp");
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
        Directory.CreateDirectory(tempDir);

        var meta = new
        {
            Timestamp = DateTime.UtcNow,
            Device = device,
            Config = config,
            Os = Environment.OSVersion.ToString(),
            Machine = Environment.MachineName
        };
        File.WriteAllText(Path.Combine(tempDir, "meta.json"), JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

        if (Directory.Exists(AppPaths.LogsPath))
        {
            var logDest = Path.Combine(tempDir, "logs");
            Directory.CreateDirectory(logDest);
            foreach (var file in Directory.GetFiles(AppPaths.LogsPath))
            {
                File.Copy(file, Path.Combine(logDest, Path.GetFileName(file)), true);
            }
        }

        if (File.Exists(AppPaths.SettingsFile))
        {
            File.Copy(AppPaths.SettingsFile, Path.Combine(tempDir, "settings.json"), true);
        }
        if (File.Exists(AppPaths.WizardStateFile))
        {
            File.Copy(AppPaths.WizardStateFile, Path.Combine(tempDir, "wizard-state.json"), true);
        }
        if (File.Exists(AppPaths.DashboardLayoutFile))
        {
            File.Copy(AppPaths.DashboardLayoutFile, Path.Combine(tempDir, "dashboard_layout.json"), true);
        }

        if (samples != null)
        {
            var telemetryCsv = Path.Combine(tempDir, "telemetry_last_60s.csv");
            using var writer = new StreamWriter(telemetryCsv);
            writer.WriteLine("timestamp,angle,velocity,torque,clipping,loop_dt_ms");
            foreach (var s in samples)
            {
                writer.WriteLine($"{s.Timestamp:O},{s.Angle},{s.Velocity},{s.TorqueCommand},{s.Clipping},{s.LoopDtMs}");
            }
        }

        var portInfo = string.Join(Environment.NewLine, System.IO.Ports.SerialPort.GetPortNames().OrderBy(p => p));
        File.WriteAllText(Path.Combine(tempDir, "ports.txt"), portInfo);

        var lastFlash = Path.Combine(AppPaths.AppDataRoot, "last_flash.txt");
        if (File.Exists(lastFlash))
        {
            File.Copy(lastFlash, Path.Combine(tempDir, "last_flash.txt"), true);
        }

        if (Directory.Exists(AppPaths.ProfilesPath))
        {
            var profDest = Path.Combine(tempDir, "profiles");
            Directory.CreateDirectory(profDest);
            foreach (var file in Directory.GetFiles(AppPaths.ProfilesPath, "*.json"))
            {
                File.Copy(file, Path.Combine(profDest, Path.GetFileName(file)), true);
            }
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }
        ZipFile.CreateFromDirectory(tempDir, destinationPath);
        Directory.Delete(tempDir, true);
        _logger.Info($"Support bundle created: {destinationPath}");
        return destinationPath;
    }
}
