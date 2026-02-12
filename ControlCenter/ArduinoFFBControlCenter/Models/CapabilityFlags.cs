namespace ArduinoFFBControlCenter.Models;

[Flags]
public enum CapabilityFlags
{
    None = 0,
    HasZIndex = 1 << 0,
    HasAS5600 = 1 << 1,
    HasTwoFfbAxis = 1 << 2,
    HasHatSwitch = 1 << 3,
    HasAds1015 = 1 << 4,
    HasAvgInputs = 1 << 5,
    HasButtonMatrix = 1 << 6,
    HasXYShifter = 1 << 7,
    HasExtraButtons = 1 << 8,
    HasSplitAxis = 1 << 9,
    HasAnalogFfbAxis = 1 << 10,
    HasShiftRegister = 1 << 11,
    HasSn74 = 1 << 12,
    HasLoadCell = 1 << 13,
    HasMcp4725 = 1 << 14,
    NoEeprom = 1 << 15,
    ProMicroPins = 1 << 16
}
