namespace ArduinoFFBControlCenter.Models;

public class WheelProfileManifest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Version { get; set; } = "1.0";
    public string? FirmwareVersion { get; set; }
    public string? OptionLetters { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new();
}

public class WheelProfilePackage
{
    public WheelProfileManifest Manifest { get; set; } = new();
    public Profile? Profile { get; set; }
    public DashboardLayout? Layout { get; set; }
    public string? FirmwareHexPath { get; set; }
    public string? SourcePath { get; set; }
}
