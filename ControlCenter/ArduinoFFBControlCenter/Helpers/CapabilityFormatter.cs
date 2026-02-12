using System.Collections.Generic;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Helpers;

public static class CapabilityFormatter
{
    public static string Format(DeviceInfo info)
    {
        if (!info.SupportsInfoCommand && info.Capabilities == CapabilityFlags.None)
        {
            return "Unknown (INFO unsupported)";
        }

        if (info.Capabilities == CapabilityFlags.None)
        {
            return "None reported";
        }

        var list = new List<string>();

        if (info.Capabilities.HasFlag(CapabilityFlags.HasZIndex)) list.Add("Z-index");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasAS5600)) list.Add("AS5600");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasTwoFfbAxis)) list.Add("2-FFB axis");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasHatSwitch)) list.Add("Hat switch");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasAds1015)) list.Add("ADS1015");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasAvgInputs)) list.Add("Avg inputs");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasButtonMatrix)) list.Add("Button matrix");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasXYShifter)) list.Add("XY shifter");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasExtraButtons)) list.Add("Extra buttons");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasSplitAxis)) list.Add("Split axis");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasAnalogFfbAxis)) list.Add("Analog FFB axis");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasShiftRegister)) list.Add("Shift register");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasSn74)) list.Add("SN74ALS166");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasLoadCell)) list.Add("Load cell");
        if (info.Capabilities.HasFlag(CapabilityFlags.HasMcp4725)) list.Add("MCP4725");
        if (info.Capabilities.HasFlag(CapabilityFlags.NoEeprom)) list.Add("No EEPROM");
        if (info.Capabilities.HasFlag(CapabilityFlags.ProMicroPins)) list.Add("Pro Micro pins");

        return string.Join(", ", list);
    }
}
