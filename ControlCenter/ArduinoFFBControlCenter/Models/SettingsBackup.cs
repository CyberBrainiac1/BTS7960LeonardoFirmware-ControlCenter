namespace ArduinoFFBControlCenter.Models;

public class SettingsBackup
{
    public FfbConfig Config { get; set; } = new();
    public string? FirmwareVersion { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
