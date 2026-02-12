namespace ArduinoFFBControlCenter.Models;

public class DeviceInfo
{
    public string Port { get; set; } = string.Empty;
    public string? Vid { get; set; }
    public string? Pid { get; set; }
    public string? ProductName { get; set; }
    public string FirmwareVersion { get; set; } = "Unknown";
    public CapabilityFlags Capabilities { get; set; } = CapabilityFlags.None;
    public uint? CapabilityMask { get; set; }
    public CalibrationInfo? CalibrationInfo { get; set; }
    public bool SupportsInfoCommand { get; set; }
    public bool SupportsSerialConfig { get; set; }
    public bool SupportsTelemetry { get; set; }
    public bool IsDemo { get; set; }
}
