namespace ArduinoFFBControlCenter.Models;

public class PresetProfile
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FfbConfig Config { get; set; } = new();
}
