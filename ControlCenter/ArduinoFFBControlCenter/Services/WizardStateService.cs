using System.Text.Json;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class WizardStateService
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public WizardState Load()
    {
        try
        {
            if (!File.Exists(AppPaths.WizardStateFile))
            {
                return new WizardState();
            }
            var json = File.ReadAllText(AppPaths.WizardStateFile);
            return JsonSerializer.Deserialize<WizardState>(json, _options) ?? new WizardState();
        }
        catch
        {
            return new WizardState();
        }
    }

    public void Save(WizardState state)
    {
        var json = JsonSerializer.Serialize(state, _options);
        File.WriteAllText(AppPaths.WizardStateFile, json);
    }
}
