using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

// Thin aliases to satisfy service naming requirements without changing existing architecture.
public class FlashingService : FirmwareFlasherService
{
    public FlashingService(LoggerService logger) : base(logger) { }
}

public class CapabilitiesService : DeviceCapabilitiesService { }

public class SettingsSyncService
{
    private readonly DeviceSettingsService _inner;
    public SettingsSyncService(DeviceSettingsService inner)
    {
        _inner = inner;
    }

    public Task<FfbConfig?> LoadFromDeviceAsync(CancellationToken ct) => _inner.LoadFromDeviceAsync(ct);
    public Task ApplyConfigAsync(FfbConfig cfg, CancellationToken ct) => _inner.ApplyConfigAsync(cfg, ct);
    public Task SaveToWheelAsync(CancellationToken ct) => _inner.SaveToWheelAsync(ct);
}

public class LoggingService : LoggerService { }
