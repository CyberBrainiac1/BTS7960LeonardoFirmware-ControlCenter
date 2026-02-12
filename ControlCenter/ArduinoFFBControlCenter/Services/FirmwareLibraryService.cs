using ArduinoFFBControlCenter.Models;
using System.Linq;

namespace ArduinoFFBControlCenter.Services;

public class FirmwareLibraryService
{
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

        var list = new List<FirmwareHexInfo>();
        var files = Directory.GetFiles(_libraryRoot, "*.hex", SearchOption.AllDirectories);
        foreach (var file in files)
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

        return list.OrderBy(f => f.Name).ToList();
    }
}
