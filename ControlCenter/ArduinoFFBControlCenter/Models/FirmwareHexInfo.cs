namespace ArduinoFFBControlCenter.Models;

public class FirmwareHexInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Options { get; set; }
    public string? Notes { get; set; }
}
