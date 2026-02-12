namespace ArduinoFFBControlCenter.Models;

public class Profile
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
    public string? Notes { get; set; }
    public FfbConfig Config { get; set; } = new();
    public FfbCurveSettings Curve { get; set; } = new();
    public AdvancedTuningSettings Advanced { get; set; } = new();
    public List<string> GameExecutables { get; set; } = new();
    public string? FirmwareVersion { get; set; }
    public List<string> Tags { get; set; } = new();
}
