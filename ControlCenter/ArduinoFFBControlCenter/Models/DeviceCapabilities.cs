namespace ArduinoFFBControlCenter.Models;

public class DeviceCapabilities
{
    public bool SupportsSerialConfig { get; set; }
    public bool SupportsSettingsRead { get; set; }
    public bool SupportsSettingsWrite { get; set; }
    public bool SupportsTelemetry { get; set; }
    public bool SupportsEepromSave { get; set; }
    public bool SupportsCalibrationInfo { get; set; }
    public bool SupportsCalibrationSet { get; set; }
}
