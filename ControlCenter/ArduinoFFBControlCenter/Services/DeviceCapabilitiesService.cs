using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class DeviceCapabilitiesService
{
    public const int CalibrationInfoBit = 20;
    public const int CalibrationSetBit = 21;
    public const int TelemetryStreamBit = 22;

    public DeviceCapabilities GetCapabilities(DeviceInfo? info)
    {
        if (info == null)
        {
            return new DeviceCapabilities();
        }

        var supportsSerial = info.SupportsSerialConfig || info.IsDemo;
        var mask = info.CapabilityMask ?? 0;
        var calInfo = info.CalibrationInfo != null || ((mask & (1u << CalibrationInfoBit)) != 0);
        var calSet = ((mask & (1u << CalibrationSetBit)) != 0);

        return new DeviceCapabilities
        {
            SupportsSerialConfig = supportsSerial,
            SupportsSettingsRead = supportsSerial,
            SupportsSettingsWrite = supportsSerial,
            SupportsTelemetry = info.SupportsTelemetry || ((mask & (1u << TelemetryStreamBit)) != 0),
            SupportsEepromSave = !info.Capabilities.HasFlag(CapabilityFlags.NoEeprom),
            SupportsCalibrationInfo = calInfo,
            SupportsCalibrationSet = calSet || supportsSerial
        };
    }
}
