using System.Linq;
using System.Text.Json;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class ButtonLabelService
{
    private readonly string _path = Path.Combine(AppPaths.AppDataRoot, "button_labels.json");
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public Dictionary<int, string> Load()
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<int, string>();
        }
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<Dictionary<int, string>>(json, _options) ?? new Dictionary<int, string>();
        }
        catch
        {
            return new Dictionary<int, string>();
        }
    }

    public void Save(IEnumerable<ButtonState> buttons)
    {
        var map = buttons.ToDictionary(b => b.Index, b => b.Label ?? $"Button {b.Index}");
        var json = JsonSerializer.Serialize(map, _options);
        File.WriteAllText(_path, json);
    }
}
