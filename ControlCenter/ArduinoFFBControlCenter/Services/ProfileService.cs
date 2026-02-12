using System.Text.Json;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class ProfileService
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public ProfileService()
    {
        Directory.CreateDirectory(AppPaths.ProfilesPath);
    }

    public List<Profile> LoadProfiles()
    {
        var list = new List<Profile>();
        if (!Directory.Exists(AppPaths.ProfilesPath))
        {
            return list;
        }

        foreach (var file in Directory.GetFiles(AppPaths.ProfilesPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<Profile>(json, _options);
                if (profile != null)
                {
                    list.Add(profile);
                }
            }
            catch
            {
            }
        }

        return list.OrderBy(p => p.Name).ToList();
    }

    public void SaveProfile(Profile profile)
    {
        var safe = string.Join("_", profile.Name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(AppPaths.ProfilesPath, $"{safe}.json");
        if (File.Exists(path))
        {
            try
            {
                var existingJson = File.ReadAllText(path);
                var existing = JsonSerializer.Deserialize<Profile>(existingJson, _options);
                if (existing != null)
                {
                    profile.Version = Math.Max(existing.Version + 1, 2);
                    profile.CreatedUtc = existing.CreatedUtc;
                }
            }
            catch
            {
            }
        }
        profile.UpdatedUtc = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(profile, _options);
        File.WriteAllText(path, json);
    }

    public Profile? LoadProfileByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(AppPaths.ProfilesPath, $"{safe}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Profile>(json, _options);
        }
        catch
        {
            return null;
        }
    }
}
