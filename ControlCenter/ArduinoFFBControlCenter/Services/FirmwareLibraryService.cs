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
        var allHex = LoadHexCandidates().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (allHex.Count == 0)
        {
            return new List<FirmwareHexInfo>();
        }

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
            var rel = TryGetDisplayPath(file);
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

    private IEnumerable<string> LoadHexCandidates()
    {
        if (Directory.Exists(_libraryRoot))
        {
            foreach (var file in Directory.GetFiles(_libraryRoot, "*.hex", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }

        foreach (var path in GetExamplesFirmwarePaths())
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(path, "*.hex", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> GetExamplesFirmwarePaths()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "Examples", "Arduino-FFB-wheel-master", "Arduino-FFB-wheel-master", "brWheel_my", "leonardo hex"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Examples", "Arduino-FFB-wheel-master", "Arduino-FFB-wheel-master", "brWheel_my", "leonardo hex"))
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return candidate;
        }
    }

    private string TryGetDisplayPath(string fullPath)
    {
        if (fullPath.StartsWith(_libraryRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(_libraryRoot, fullPath);
        }

        var exampleRoot = Path.Combine(Environment.CurrentDirectory, "Examples");
        if (fullPath.StartsWith(exampleRoot, StringComparison.OrdinalIgnoreCase))
        {
            return "Examples/" + Path.GetRelativePath(exampleRoot, fullPath).Replace('\\', '/');
        }

        return fullPath;
    }
}
