using System.Globalization;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class DeviceProtocolService
{
    private readonly SerialDeviceService _serial;
    private readonly LoggerService _logger;
    public int EffStateCache { get; private set; } = 1;

    public void UpdateEffStateCache(int value)
    {
        EffStateCache = value;
    }

    public DeviceProtocolService(SerialDeviceService serial, LoggerService logger)
    {
        _serial = serial;
        _logger = logger;
    }

    public static CapabilityFlags ParseCapabilities(string fwVersion)
    {
        fwVersion = fwVersion.ToLowerInvariant();
        var flags = CapabilityFlags.None;
        if (fwVersion.Contains('z')) flags |= CapabilityFlags.HasZIndex;
        if (fwVersion.Contains('w')) flags |= CapabilityFlags.HasAS5600;
        if (fwVersion.Contains('b')) flags |= CapabilityFlags.HasTwoFfbAxis;
        if (fwVersion.Contains('h')) flags |= CapabilityFlags.HasHatSwitch;
        if (fwVersion.Contains('s')) flags |= CapabilityFlags.HasAds1015;
        if (fwVersion.Contains('i')) flags |= CapabilityFlags.HasAvgInputs;
        if (fwVersion.Contains('t')) flags |= CapabilityFlags.HasButtonMatrix;
        if (fwVersion.Contains('f')) flags |= CapabilityFlags.HasXYShifter;
        if (fwVersion.Contains('e')) flags |= CapabilityFlags.HasExtraButtons;
        if (fwVersion.Contains('k')) flags |= CapabilityFlags.HasSplitAxis;
        if (fwVersion.Contains('x')) flags |= CapabilityFlags.HasAnalogFfbAxis;
        if (fwVersion.Contains('n')) flags |= CapabilityFlags.HasShiftRegister;
        if (fwVersion.Contains('r')) flags |= CapabilityFlags.HasSn74;
        if (fwVersion.Contains('l')) flags |= CapabilityFlags.HasLoadCell;
        if (fwVersion.Contains('g')) flags |= CapabilityFlags.HasMcp4725;
        if (fwVersion.Contains('p')) flags |= CapabilityFlags.NoEeprom;
        if (fwVersion.Contains('m')) flags |= CapabilityFlags.ProMicroPins;
        return flags;
    }

    public async Task<DeviceInfoResponse?> TryGetInfoAsync()
    {
        try
        {
            var line = await _serial.SendCommandAsync("I", s => s.StartsWith("fw-v", StringComparison.OrdinalIgnoreCase), 600);
            if (!line.StartsWith("fw-v", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            var fw = parts[0].Trim();
            var response = new DeviceInfoResponse
            {
                FirmwareVersion = fw,
                Capabilities = ParseCapabilities(fw),
                SupportsInfoCommand = true
            };

            var configOffset = 1;
            if (parts.Length >= 18 && uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mask))
            {
                response.CapabilityMask = mask;
                configOffset = 2;
            }

            var config = TryParseConfig(parts, configOffset);
            if (config != null)
            {
                response.Config = config;
                EffStateCache = config.DesktopEffectsByte;
            }

            var calibration = TryParseCalibration(parts, configOffset + 16);
            if (calibration != null)
            {
                response.CalibrationInfo = calibration;
            }

            return response;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> GetFirmwareVersionAsync()
    {
        var line = await _serial.SendCommandAsync("V", s => s.StartsWith("fw-v", StringComparison.OrdinalIgnoreCase));
        return line.Trim();
    }

    public async Task<FfbConfig> GetAllSettingsAsync()
    {
        var line = await _serial.SendCommandAsync("U", s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 10);
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var config = TryParseConfig(parts, 0);
        if (config == null)
        {
            throw new InvalidOperationException($"Unexpected settings response: {line}");
        }

        EffStateCache = config.DesktopEffectsByte;
        return config;
    }

    public async Task SetRotationAsync(int deg)
    {
        await ExecuteWithTelemetrySuppressedAsync(() => _serial.SendCommandAsync($"G {deg}", s => s.Trim() == "1" || s.Trim() == "0"));
    }

    public async Task CenterAsync()
    {
        await ExecuteWithTelemetrySuppressedAsync(() => _serial.SendCommandAsync("C", s => s.Trim() == "1" || s.Trim() == "0"));
    }

    public async Task CalibrateAsync()
    {
        _serial.SendCommandNoWait("R");
    }

    public async Task SaveAsync()
    {
        await ExecuteWithTelemetrySuppressedAsync(() => _serial.SendCommandAsync("A", s => s.Trim() == "1" || s.Trim() == "0"));
    }

    public async Task SetGainAsync(string code, int value)
    {
        await ExecuteWithTelemetrySuppressedAsync(() => _serial.SendCommandAsync($"F{code} {value}", s => s.Trim() == "1"));
    }

    public async Task SetMinTorqueAsync(int value)
    {
        await ExecuteWithTelemetrySuppressedAsync(() => _serial.SendCommandAsync($"FJ {value}", s => s.Trim() == "1"));
    }

    public async Task SetBrakePressureAsync(int value)
    {
        await ExecuteWithTelemetrySuppressedAsync(() => _serial.SendCommandAsync($"B {value}", s => s.Trim() == "1"));
    }

    public async Task SetEffStateAsync(int effState)
    {
        _serial.SendCommandNoWait($"E {effState}");
        EffStateCache = effState;
    }

    public async Task SetTelemetryEnabledAsync(bool enable)
    {
        var eff = EffStateCache;
        if (enable)
        {
            eff |= (1 << 4);
        }
        else
        {
            eff &= ~(1 << 4);
        }
        await SetEffStateAsync(eff);
        _serial.TelemetryEnabled = enable;
        _logger.Info($"Telemetry {(enable ? "enabled" : "disabled")}");
    }

    private async Task<T> ExecuteWithTelemetrySuppressedAsync<T>(Func<Task<T>> action)
    {
        if (!_serial.TelemetryEnabled)
        {
            return await action();
        }

        var eff = EffStateCache & ~(1 << 4);
        _serial.TelemetryEnabled = false;
        _serial.SendCommandNoWait($"E {eff}");
        await Task.Delay(120);

        var result = await action();

        _serial.SendCommandNoWait($"E {EffStateCache}");
        _serial.TelemetryEnabled = true;
        await Task.Delay(60);
        return result;
    }

    private static FfbConfig? TryParseConfig(string[] parts, int offset)
    {
        if (parts.Length - offset < 16)
        {
            return null;
        }

        return new FfbConfig
        {
            RotationDeg = int.Parse(parts[offset + 0], CultureInfo.InvariantCulture),
            GeneralGain = int.Parse(parts[offset + 1], CultureInfo.InvariantCulture),
            DamperGain = int.Parse(parts[offset + 2], CultureInfo.InvariantCulture),
            FrictionGain = int.Parse(parts[offset + 3], CultureInfo.InvariantCulture),
            ConstantGain = int.Parse(parts[offset + 4], CultureInfo.InvariantCulture),
            PeriodicGain = int.Parse(parts[offset + 5], CultureInfo.InvariantCulture),
            SpringGain = int.Parse(parts[offset + 6], CultureInfo.InvariantCulture),
            InertiaGain = int.Parse(parts[offset + 7], CultureInfo.InvariantCulture),
            CenterGain = int.Parse(parts[offset + 8], CultureInfo.InvariantCulture),
            StopGain = int.Parse(parts[offset + 9], CultureInfo.InvariantCulture),
            MinTorque = int.Parse(parts[offset + 10], CultureInfo.InvariantCulture),
            BrakePressureOrBalance = int.Parse(parts[offset + 11], CultureInfo.InvariantCulture),
            DesktopEffectsByte = int.Parse(parts[offset + 12], CultureInfo.InvariantCulture),
            MaxTorque = int.Parse(parts[offset + 13], CultureInfo.InvariantCulture),
            EncoderCpr = int.Parse(parts[offset + 14], CultureInfo.InvariantCulture),
            PwmState = int.Parse(parts[offset + 15], CultureInfo.InvariantCulture)
        };
    }

    private static CalibrationInfo? TryParseCalibration(string[] parts, int offset)
    {
        if (parts.Length - offset < 4)
        {
            return null;
        }

        if (!int.TryParse(parts[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var present))
        {
            return null;
        }

        if (!int.TryParse(parts[offset + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var center))
        {
            return null;
        }

        if (!int.TryParse(parts[offset + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var invert))
        {
            return null;
        }

        if (!int.TryParse(parts[offset + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rotation))
        {
            return null;
        }

        return new CalibrationInfo
        {
            Present = present != 0,
            CenterOffsetRaw = center,
            Inverted = invert != 0,
            RotationDeg = rotation
        };
    }
}
