using System.Text.Json;
using System.Linq;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class DeviceSettingsService
{
    private readonly LoggerService _logger;
    private readonly DeviceProtocolService _protocol;
    private readonly DeviceStateService _deviceState;
    private readonly ProfileService _profiles;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly TuningStateService _tuningState;
    private readonly DeviceCapabilitiesService _caps;
    private readonly SettingsPersistenceTracker _tracker = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public event Action<SettingsPersistenceState>? PersistenceChanged;
    public event Action<FfbConfig?>? DeviceConfigLoaded;

    public FfbConfig? CurrentConfig { get; private set; }
    public SettingsPersistenceState PersistenceState => _tracker.State;

    public DeviceSettingsService(
        LoggerService logger,
        DeviceProtocolService protocol,
        DeviceStateService deviceState,
        ProfileService profiles,
        SettingsService settingsService,
        AppSettings settings,
        TuningStateService tuningState,
        DeviceCapabilitiesService caps)
    {
        _logger = logger;
        _protocol = protocol;
        _deviceState = deviceState;
        _profiles = profiles;
        _settingsService = settingsService;
        _settings = settings;
        _tuningState = tuningState;
        _caps = caps;
    }

    public bool CanUseSerialConfig()
    {
        var caps = _caps.GetCapabilities(_deviceState.CurrentDevice);
        return caps.SupportsSerialConfig;
    }

    public bool CanSaveToWheel()
    {
        var caps = _caps.GetCapabilities(_deviceState.CurrentDevice);
        return caps.SupportsSerialConfig && caps.SupportsEepromSave;
    }

    public async Task<FfbConfig?> LoadFromDeviceAsync(CancellationToken ct)
    {
        if (!CanUseSerialConfig())
        {
            return null;
        }
        if (_deviceState.IsDemoMode)
        {
            return null;
        }

        var config = await ExecuteWithRetryAsync(() => _protocol.GetAllSettingsAsync(), "Read settings", ct, 1, 1600);
        if (config != null)
        {
            CurrentConfig = config;
            _tuningState.UpdateConfig(config);
            _tracker.MarkDeviceLoaded(config);
            DeviceConfigLoaded?.Invoke(config);
            NotifyPersistence();
        }
        return config;
    }

    public async Task ApplyConfigAsync(FfbConfig config, CancellationToken ct)
    {
        if (!CanUseSerialConfig())
        {
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            return;
        }

        await ExecuteWithRetryAsync(() => _protocol.SetRotationAsync(config.RotationDeg), "Rotation", ct, 1, 1500);
        await ExecuteWithRetryAsync(() => _protocol.SetGainAsync("G", config.GeneralGain), "General gain", ct, 1, 1200);
        await ExecuteWithRetryAsync(() => _protocol.SetGainAsync("D", config.DamperGain), "Damper gain", ct, 1, 1200);
        await ExecuteWithRetryAsync(() => _protocol.SetGainAsync("F", config.FrictionGain), "Friction gain", ct, 1, 1200);
        await ExecuteWithRetryAsync(() => _protocol.SetGainAsync("I", config.InertiaGain), "Inertia gain", ct, 1, 1200);
        await ExecuteWithRetryAsync(() => _protocol.SetGainAsync("M", config.SpringGain), "Spring gain", ct, 1, 1200);
        await ExecuteWithRetryAsync(() => _protocol.SetGainAsync("C", config.ConstantGain), "Constant gain", ct, 1, 1200);
        await ExecuteWithRetryAsync(() => _protocol.SetGainAsync("S", config.PeriodicGain), "Periodic gain", ct, 1, 1200);
        await ExecuteWithRetryAsync(() => _protocol.SetGainAsync("A", config.CenterGain), "Center gain", ct, 1, 1200);
        await ExecuteWithRetryAsync(() => _protocol.SetGainAsync("B", config.StopGain), "Endstop gain", ct, 1, 1200);
        await ExecuteWithRetryAsync(() => _protocol.SetMinTorqueAsync(config.MinTorque), "Min torque", ct, 1, 1200);
        await ExecuteWithRetryAsync(() => _protocol.SetBrakePressureAsync(config.BrakePressureOrBalance), "Brake/Bal", ct, 1, 1200);

        CurrentConfig = config;
        _tuningState.UpdateConfig(config);
        _tracker.MarkApplied(config);
        NotifyPersistence();
    }

    public async Task ApplyRotationAsync(int rotationDeg, CancellationToken ct)
    {
        if (!CanUseSerialConfig())
        {
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            return;
        }

        await ExecuteWithRetryAsync(() => _protocol.SetRotationAsync(rotationDeg), "Rotation", ct, 1, 1500);
        if (CurrentConfig != null)
        {
            CurrentConfig.RotationDeg = rotationDeg;
            _tuningState.UpdateConfig(CurrentConfig);
            _tracker.MarkApplied(CurrentConfig);
            NotifyPersistence();
        }
    }

    public async Task CenterAsync(CancellationToken ct)
    {
        if (!CanUseSerialConfig())
        {
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            return;
        }

        await ExecuteWithRetryAsync(() => _protocol.CenterAsync(), "Center", ct, 1, 1200);
    }

    public Task CalibrateAsync(CancellationToken ct)
    {
        if (!CanUseSerialConfig())
        {
            return Task.CompletedTask;
        }
        if (_deviceState.IsDemoMode)
        {
            return Task.CompletedTask;
        }

        _ = _protocol.CalibrateAsync();
        return Task.Delay(200, ct);
    }

    public async Task SaveToWheelAsync(CancellationToken ct)
    {
        if (!CanSaveToWheel())
        {
            return;
        }
        if (_deviceState.IsDemoMode)
        {
            return;
        }

        await BackupCurrentAsync(ct);
        await ExecuteWithRetryAsync(() => _protocol.SaveAsync(), "Save EEPROM", ct, 1, 1600);
        if (CurrentConfig != null)
        {
            _tracker.MarkSavedToWheel(CurrentConfig);
            NotifyPersistence();
        }

        await Task.Delay(120, ct);
        await LoadFromDeviceAsync(ct);
    }

    public void SaveToPc(Profile profile)
    {
        _profiles.SaveProfile(profile);
        _settings.LastProfileName = profile.Name;
        _settingsService.Save(_settings);
        CurrentConfig = profile.Config;
        _tracker.MarkSavedToPc(profile.Name);
        NotifyPersistence();
    }

    public SettingsBackup? LoadBackup()
    {
        if (!File.Exists(AppPaths.SettingsBackupFile))
        {
            return null;
        }
        var json = File.ReadAllText(AppPaths.SettingsBackupFile);
        return JsonSerializer.Deserialize<SettingsBackup>(json, _jsonOptions);
    }

    public async Task RestoreBackupAsync(CancellationToken ct)
    {
        var backup = LoadBackup();
        if (backup == null)
        {
            _logger.Warn("No backup found.");
            return;
        }

        await ApplyConfigAsync(backup.Config, ct);
        _logger.Info("Backup settings applied.");
    }

    public async Task SyncOnConnectAsync(Func<SettingsConflictChoice>? resolveConflict, CancellationToken ct)
    {
        var device = _deviceState.CurrentDevice;
        if (device == null)
        {
            return;
        }

        var caps = _caps.GetCapabilities(device);
        if (caps.SupportsSettingsRead && !device.IsDemo)
        {
            var config = await LoadFromDeviceAsync(ct);
            if (config != null && !string.IsNullOrWhiteSpace(_settings.LastProfileName))
            {
                var profile = _profiles.LoadProfileByName(_settings.LastProfileName!);
                if (profile != null && !SettingsDiff.AreEquivalent(profile.Config, config))
                {
                    var choice = resolveConflict?.Invoke() ?? SettingsConflictChoice.UseWheel;
                    if (choice == SettingsConflictChoice.ApplyProfile)
                    {
                        await ApplyConfigAsync(profile.Config, ct);
                        _logger.Info("Applied last profile after conflict prompt.");
                    }
                }
            }
        }
        else
        {
            var profile = !string.IsNullOrWhiteSpace(_settings.LastProfileName)
                ? _profiles.LoadProfileByName(_settings.LastProfileName!)
                : _profiles.LoadProfiles().FirstOrDefault();
            if (profile != null)
            {
                CurrentConfig = profile.Config;
                _tuningState.UpdateConfig(profile.Config);
                _tracker.MarkSavedToPc(profile.Name);
                NotifyPersistence();
                if (_settings.AutoApplyLastProfile)
                {
                    _logger.Info("Loaded last profile (PC-only). Device settings unavailable.");
                }
            }
        }
    }

    private async Task BackupCurrentAsync(CancellationToken ct)
    {
        if (!CanUseSerialConfig())
        {
            return;
        }

        try
        {
            var cfg = await ExecuteWithRetryAsync(() => _protocol.GetAllSettingsAsync(), "Backup read", ct, 1, 1600);
            if (cfg == null)
            {
                return;
            }

            var backup = new SettingsBackup
            {
                Config = cfg,
                FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion
            };
            var json = JsonSerializer.Serialize(backup, _jsonOptions);
            File.WriteAllText(AppPaths.SettingsBackupFile, json);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Backup failed: {ex.Message}");
        }
    }

    private void NotifyPersistence()
    {
        PersistenceChanged?.Invoke(_tracker.State);
    }

    private async Task<T?> ExecuteWithRetryAsync<T>(Func<Task<T>> action, string label, CancellationToken ct, int retries, int timeoutMs)
    {
        Exception? last = null;
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var task = action();
                var completed = await Task.WhenAny(task, Task.Delay(timeoutMs, ct));
                if (completed != task)
                {
                    throw new TimeoutException($"{label} timed out.");
                }

                return await task;
            }
            catch (Exception ex)
            {
                last = ex;
                _logger.Warn($"{label} failed (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(120, ct);
            }
        }

        if (last != null)
        {
            _logger.Error($"{label} failed: {last.Message}");
        }

        return default;
    }
    private async Task ExecuteWithRetryAsync(Func<Task> action, string label, CancellationToken ct, int retries, int timeoutMs)
    {
        Exception? last = null;
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var task = action();
                var completed = await Task.WhenAny(task, Task.Delay(timeoutMs, ct));
                if (completed != task)
                {
                    throw new TimeoutException($"{label} timed out.");
                }

                await task;
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                _logger.Warn($"{label} failed (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(120, ct);
            }
        }

        if (last != null)
        {
            _logger.Error($"{label} failed: {last.Message}");
            throw last;
        }
    }
}
