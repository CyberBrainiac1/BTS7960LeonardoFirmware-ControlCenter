using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class DeviceStateService
{
    public DeviceInfo? CurrentDevice { get; private set; }
    public bool IsDemoMode { get; private set; }

    public event Action<DeviceInfo?>? DeviceChanged;
    public event Action<bool>? DemoModeChanged;

    public void SetDevice(DeviceInfo? device)
    {
        CurrentDevice = device;
        DeviceChanged?.Invoke(device);
    }

    public void SetDemoMode(bool enabled, DeviceInfo? demoDevice = null)
    {
        IsDemoMode = enabled;
        if (enabled && demoDevice != null)
        {
            CurrentDevice = demoDevice;
            DeviceChanged?.Invoke(demoDevice);
        }
        DemoModeChanged?.Invoke(enabled);
    }
}
