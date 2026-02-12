using System.Text;
using System.Text.Json;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class SnapshotService
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly LoggerService _logger;

    public SnapshotService(LoggerService logger)
    {
        _logger = logger;
        Directory.CreateDirectory(AppPaths.SnapshotsPath);
    }

    public SnapshotEntry CreateSnapshot(SnapshotEntry entry, IReadOnlyList<TelemetrySample>? telemetry = null)
    {
        var folder = Path.Combine(AppPaths.SnapshotsPath, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{entry.Kind}");
        Directory.CreateDirectory(folder);
        var snapshotPath = Path.Combine(folder, "snapshot.json");
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(entry, _options));

        if (telemetry != null && telemetry.Count > 0)
        {
            var csvPath = Path.Combine(folder, "telemetry.csv");
            File.WriteAllText(csvPath, BuildTelemetryCsv(telemetry));
        }

        TryCopyLastFlash(folder);
        TryCopyLatestLog(folder);

        _logger.Info($"Snapshot saved: {folder}");
        return entry;
    }

    public List<SnapshotEntry> LoadSnapshots()
    {
        var list = new List<SnapshotEntry>();
        if (!Directory.Exists(AppPaths.SnapshotsPath))
        {
            return list;
        }

        foreach (var dir in Directory.GetDirectories(AppPaths.SnapshotsPath))
        {
            var path = Path.Combine(dir, "snapshot.json");
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                var json = File.ReadAllText(path);
                var entry = JsonSerializer.Deserialize<SnapshotEntry>(json, _options);
                if (entry != null)
                {
                    list.Add(entry);
                }
            }
            catch
            {
            }
        }

        return list.OrderByDescending(s => s.CreatedUtc).ToList();
    }

    public List<string> Diff(SnapshotEntry? a, SnapshotEntry? b)
    {
        var lines = new List<string>();
        if (a == null || b == null)
        {
            lines.Add("Select two snapshots to compare.");
            return lines;
        }

        if (a.FirmwareVersion != b.FirmwareVersion)
        {
            lines.Add($"Firmware: {a.FirmwareVersion} -> {b.FirmwareVersion}");
        }

        if (a.Config != null && b.Config != null)
        {
            AddDiff(lines, "Rotation", a.Config.RotationDeg, b.Config.RotationDeg);
            AddDiff(lines, "General", a.Config.GeneralGain, b.Config.GeneralGain);
            AddDiff(lines, "Damper", a.Config.DamperGain, b.Config.DamperGain);
            AddDiff(lines, "Friction", a.Config.FrictionGain, b.Config.FrictionGain);
            AddDiff(lines, "Inertia", a.Config.InertiaGain, b.Config.InertiaGain);
            AddDiff(lines, "Spring", a.Config.SpringGain, b.Config.SpringGain);
            AddDiff(lines, "Endstop", a.Config.StopGain, b.Config.StopGain);
        }

        return lines;
    }

    private static void AddDiff(List<string> lines, string name, int a, int b)
    {
        if (a != b)
        {
            lines.Add($"{name}: {a} -> {b}");
        }
    }

    private static string BuildTelemetryCsv(IReadOnlyList<TelemetrySample> samples)
    {
        var sb = new StringBuilder();
        sb.AppendLine("timestamp,angle,velocity,torque,clipping,loop_dt");
        foreach (var s in samples)
        {
            sb.AppendLine($"{s.Timestamp:o},{s.Angle},{s.Velocity},{s.TorqueCommand},{s.Clipping},{s.LoopDtMs}");
        }
        return sb.ToString();
    }

    private static void TryCopyLastFlash(string folder)
    {
        var lastFlash = Path.Combine(AppPaths.AppDataRoot, "last_flash.txt");
        if (File.Exists(lastFlash))
        {
            File.Copy(lastFlash, Path.Combine(folder, "last_flash.txt"), true);
        }
    }

    private static void TryCopyLatestLog(string folder)
    {
        if (!Directory.Exists(AppPaths.LogsPath))
        {
            return;
        }
        var latest = Directory.GetFiles(AppPaths.LogsPath, "log-*.txt")
            .OrderByDescending(f => f)
            .FirstOrDefault();
        if (latest != null)
        {
            File.Copy(latest, Path.Combine(folder, Path.GetFileName(latest)), true);
        }
    }
}
