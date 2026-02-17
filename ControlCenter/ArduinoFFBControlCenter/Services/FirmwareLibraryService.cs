using ArduinoFFBControlCenter.Models;
using System.Linq;

namespace ArduinoFFBControlCenter.Services;

public class FirmwareLibraryService
{
    private const string RecommendedFileName = "wheel_default_recommended.hex";
    private readonly string _libraryRoot;

    public FirmwareLibraryService(string libraryRoot)
    {
        _libraryRoot = libraryRoot;
    }

    public List<FirmwareHexInfo> LoadLibrary()
    {
        if (!Directory.Exists(_libraryRoot))
        {
            return new List<FirmwareHexInfo>();
        }

        var allHex = Directory.GetFiles(_libraryRoot, "*.hex", SearchOption.AllDirectories).ToList();
        var recommendedPath = EnsureRecommendedHex(allHex);

        var list = new List<FirmwareHexInfo>();

        if (!string.IsNullOrWhiteSpace(recommendedPath) && File.Exists(recommendedPath))
        {
            list.Add(new FirmwareHexInfo
            {
                Name = "Recommended - BTS7960 Default Stable",
                Path = recommendedPath,
                Notes = "One-click default. Good baseline for most Leonardo + BTS7960 builds."
            });
        }

        foreach (var file in allHex
                     .Where(f => !string.Equals(f, recommendedPath, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(f => Path.GetFileNameWithoutExtension(f)))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var rel = Path.GetRelativePath(_libraryRoot, file);
            list.Add(new FirmwareHexInfo
            {
                Name = name,
                Path = file,
                Notes = rel
            });
        }

        return list;
    }

    public FirmwareHexInfo? GetRecommended()
    {
        var library = LoadLibrary();
        return library.FirstOrDefault(f => f.Name.StartsWith("Recommended", StringComparison.OrdinalIgnoreCase))
               ?? library.FirstOrDefault();
    }

    private string? EnsureRecommendedHex(List<string> allHex)
    {
        if (!Directory.Exists(_libraryRoot))
        {
            return null;
        }

        var recommendedDir = Path.Combine(_libraryRoot, "recommended");
        Directory.CreateDirectory(recommendedDir);
        var recommendedPath = Path.Combine(recommendedDir, RecommendedFileName);

        if (File.Exists(recommendedPath))
        {
            return recommendedPath;
        }

        // Preferred source: latest plain v250 build with no extra option suffixes.
        var source = allHex.FirstOrDefault(path =>
                path.EndsWith("brWheel_my.ino.leonardo_v250.hex", StringComparison.OrdinalIgnoreCase))
            ?? allHex.FirstOrDefault(path =>
                path.Contains("v250", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileNameWithoutExtension(path).Contains("v250a", StringComparison.OrdinalIgnoreCase))
            ?? allHex.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            return null;
        }

        File.Copy(source, recommendedPath, true);
        return recommendedPath;
    }
}
