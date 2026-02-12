namespace ArduinoFFBControlCenter.Models;

public class DeviceInfoResponse
{
    public string FirmwareVersion { get; set; } = "Unknown";
    public CapabilityFlags Capabilities { get; set; } = CapabilityFlags.None;
    public FfbConfig? Config { get; set; }
    public CalibrationInfo? CalibrationInfo { get; set; }
    public bool SupportsInfoCommand { get; set; }
    public uint? CapabilityMask { get; set; }
}
