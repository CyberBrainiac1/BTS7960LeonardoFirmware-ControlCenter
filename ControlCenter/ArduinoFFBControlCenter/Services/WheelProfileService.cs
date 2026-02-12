using System.IO.Compression;
using System.Text.Json;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class WheelProfileService
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly DashboardLayoutService _layoutService;

    public WheelProfileService(DashboardLayoutService layoutService)
    {
        _layoutService = layoutService;
        Directory.CreateDirectory(AppPaths.WheelProfilesPath);
    }

    public string Export(Profile profile, string outputPath, string? firmwareHexPath, bool includeFirmwareHex = false)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"wheelprofile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var manifest = new WheelProfileManifest
        {
            Name = profile.Name,
            Description = profile.Notes,
            FirmwareVersion = profile.FirmwareVersion,
            OptionLetters = ExtractOptionLetters(profile.FirmwareVersion)
        };

        File.WriteAllText(Path.Combine(tempDir, "manifest.json"), JsonSerializer.Serialize(manifest, _options));
        File.WriteAllText(Path.Combine(tempDir, "settings.json"), JsonSerializer.Serialize(profile, _options));
        File.WriteAllText(Path.Combine(tempDir, "dashboard_layout.json"), JsonSerializer.Serialize(_layoutService.Load(), _options));

        if (includeFirmwareHex && !string.IsNullOrWhiteSpace(firmwareHexPath) && File.Exists(firmwareHexPath))
        {
            File.Copy(firmwareHexPath, Path.Combine(tempDir, "firmware.hex"), true);
        }

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
        ZipFile.CreateFromDirectory(tempDir, outputPath);
        Directory.Delete(tempDir, true);
        return outputPath;
    }

    public WheelProfilePackage Import(string path)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"wheelprofile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        ZipFile.ExtractToDirectory(path, tempDir);

        var manifestPath = Path.Combine(tempDir, "manifest.json");
        var settingsPath = Path.Combine(tempDir, "settings.json");
        var layoutPath = Path.Combine(tempDir, "dashboard_layout.json");
        var firmwarePath = Path.Combine(tempDir, "firmware.hex");

        var package = new WheelProfilePackage { SourcePath = path };
        if (File.Exists(manifestPath))
        {
            package.Manifest = JsonSerializer.Deserialize<WheelProfileManifest>(File.ReadAllText(manifestPath), _options) ?? new WheelProfileManifest();
        }
        if (File.Exists(settingsPath))
        {
            package.Profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(settingsPath), _options);
        }
        if (File.Exists(layoutPath))
        {
            package.Layout = JsonSerializer.Deserialize<DashboardLayout>(File.ReadAllText(layoutPath), _options);
        }
        if (File.Exists(firmwarePath))
        {
            var dest = Path.Combine(AppPaths.WheelProfilesPath, $"firmware-{Guid.NewGuid():N}.hex");
            File.Copy(firmwarePath, dest, true);
            package.FirmwareHexPath = dest;
        }

        Directory.Delete(tempDir, true);
        return package;
    }

    private static string? ExtractOptionLetters(string? firmwareVersion)
    {
        if (string.IsNullOrWhiteSpace(firmwareVersion))
        {
            return null;
        }
        var idx = firmwareVersion.IndexOf('v');
        if (idx < 0) return null;
        var letters = new string(firmwareVersion.Where(char.IsLetter).ToArray());
        return string.IsNullOrWhiteSpace(letters) ? null : letters;
    }
}
