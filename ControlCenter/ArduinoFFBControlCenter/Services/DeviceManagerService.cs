using System.IO.Ports;
using System.Management;
using System.Linq;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

/// <summary>
/// Handles COM port scanning, device connect/disconnect, and basic auto-detect.
/// </summary>
public class DeviceManagerService
{
    private readonly SerialDeviceService _serial;
    private readonly DeviceProtocolService _protocol;
    private readonly LoggerService _logger;

    public DeviceManagerService(SerialDeviceService serial, DeviceProtocolService protocol, LoggerService logger)
    {
        _serial = serial;
        _protocol = protocol;
        _logger = logger;
    }

    public bool IsConnected => _serial.IsConnected;

    public string[] ScanPorts() => SerialPort.GetPortNames().OrderBy(p => p).ToArray();

    // Connect sequence:
    // 1) open serial port
    // 2) try INFO, fallback to version/settings commands
    // 3) decorate with WMI VID/PID metadata
    public async Task<DeviceInfo> ConnectAsync(string port)
    {
        await _serial.ConnectAsync(port);

        DeviceInfoResponse? infoResponse = null;
        try
        {
            infoResponse = await _protocol.TryGetInfoAsync();
        }
        catch
        {
        }

        string fw = "Unknown";
        if (infoResponse?.FirmwareVersion != null)
        {
            fw = infoResponse.FirmwareVersion;
        }
        else
        {
            try
            {
                fw = await _protocol.GetFirmwareVersionAsync();
            }
            catch
            {
                _logger.Warn("Firmware version not detected (unknown firmware mode).");
            }
        }

        FfbConfig? config = infoResponse?.Config;
        bool supportsSerialConfig = false;
        if (config == null)
        {
            try
            {
                config = await _protocol.GetAllSettingsAsync();
                supportsSerialConfig = true;
            }
            catch
            {
                supportsSerialConfig = false;
            }
        }
        else
        {
            supportsSerialConfig = true;
        }

        var info = new DeviceInfo
        {
            Port = port,
            FirmwareVersion = fw,
            Capabilities = infoResponse?.Capabilities ?? DeviceProtocolService.ParseCapabilities(fw),
            CapabilityMask = infoResponse?.CapabilityMask,
            CalibrationInfo = infoResponse?.CalibrationInfo,
            SupportsInfoCommand = infoResponse?.SupportsInfoCommand ?? false,
            SupportsSerialConfig = supportsSerialConfig,
            SupportsTelemetry = supportsSerialConfig
        };

        var wmi = FindPortInfo(port);
        info.Vid = wmi.vid;
        info.Pid = wmi.pid;
        info.ProductName = wmi.name;

        _logger.Info($"Device connected: {fw} on {port}");
        if (config != null)
        {
            _protocol.UpdateEffStateCache(config.DesktopEffectsByte);
            _ = _protocol.SetTelemetryEnabledAsync(true);
        }

        return info;
    }

    public void Disconnect()
    {
        _serial.Disconnect();
    }

    // Simple probe: send V command and look for fw-v response.
    public async Task<string?> AutoDetectPortAsync()
    {
        foreach (var port in ScanPorts())
        {
            try
            {
                using var sp = new SerialPort(port, 115200, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 400,
                    WriteTimeout = 400,
                    NewLine = "\r\n"
                };
                sp.Open();
                sp.Write("V\r");
                var line = sp.ReadLine();
                if (line.StartsWith("fw-v", StringComparison.OrdinalIgnoreCase))
                {
                    return port;
                }
            }
            catch
            {
                // ignore and try next
            }
            await Task.Delay(50);
        }
        return null;
    }

    private static (string? name, string? vid, string? pid) FindPortInfo(string port)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
            foreach (ManagementObject device in searcher.Get())
            {
                var name = device["Name"]?.ToString();
                if (name == null || !name.Contains($"({port})"))
                {
                    continue;
                }

                var hardwareIds = device["HardwareID"] as string[];
                if (hardwareIds != null)
                {
                    foreach (var id in hardwareIds)
                    {
                        var vidIndex = id.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
                        var pidIndex = id.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
                        if (vidIndex >= 0 && pidIndex >= 0)
                        {
                            var vid = id.Substring(vidIndex + 4, 4);
                            var pid = id.Substring(pidIndex + 4, 4);
                            return (name, vid, pid);
                        }
                    }
                }
                return (name, null, null);
            }
        }
        catch
        {
        }

        return (null, null, null);
    }
}
