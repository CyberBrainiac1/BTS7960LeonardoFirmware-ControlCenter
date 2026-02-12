using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class ProfilesViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly ProfileService _profiles;
    private readonly DeviceSettingsService _settings;
    private readonly DeviceStateService _deviceState;
    private readonly TuningStateService _tuningState;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _appSettings;
    private readonly WheelProfileService _wheelProfiles;
    private readonly SnapshotService _snapshots;
    private readonly DashboardLayoutService _layoutService;
    private readonly FirmwareFlasherService _flasher;
    private readonly DispatcherTimer _autoSwitchTimer;
    private string? _lastAutoProfile;

    public ObservableCollection<Profile> Profiles { get; } = new();
    public ObservableCollection<PresetProfile> GamePresets { get; } = new();
    public ObservableCollection<string> DiffLines { get; } = new();
    public ObservableCollection<WheelProfilePackage> Gallery { get; } = new();
    public ObservableCollection<string> ImportWarnings { get; } = new();
    public ObservableCollection<string> ImportDiffLines { get; } = new();

    [ObservableProperty] private Profile? selectedProfile;
    [ObservableProperty] private WheelProfilePackage? selectedGallery;
    [ObservableProperty] private string newProfileName = "";
    [ObservableProperty] private string? newProfileNotes;
    [ObservableProperty] private Profile? compareLeft;
    [ObservableProperty] private Profile? compareRight;
    [ObservableProperty] private string gameExecutables = "";
    [ObservableProperty] private bool autoSwitchEnabled;
    [ObservableProperty] private string autoSwitchStatus = "Auto-switch is off";
    [ObservableProperty] private PresetProfile? selectedGamePreset;
    [ObservableProperty] private bool canUseSerialConfig;
    [ObservableProperty] private string serialConfigNotice = string.Empty;
    [ObservableProperty] private bool autoApplyLastProfile;
    [ObservableProperty] private bool includeFirmwareInExport;

    partial void OnSelectedGalleryChanged(WheelProfilePackage? value)
    {
        if (value == null)
        {
            ImportWarnings.Clear();
            ImportDiffLines.Clear();
            return;
        }
        BuildImportWarnings(value);
        BuildImportDiff(value);
    }

    public ProfilesViewModel(LoggerService logger,
        ProfileService profiles,
        DeviceSettingsService settings,
        DeviceStateService deviceState,
        TuningStateService tuningState,
        SettingsService settingsService,
        AppSettings appSettings,
        WheelProfileService wheelProfiles,
        SnapshotService snapshots,
        DashboardLayoutService layoutService,
        FirmwareFlasherService flasher)
    {
        _logger = logger;
        _profiles = profiles;
        _settings = settings;
        _deviceState = deviceState;
        _tuningState = tuningState;
        _settingsService = settingsService;
        _appSettings = appSettings;
        _wheelProfiles = wheelProfiles;
        _snapshots = snapshots;
        _layoutService = layoutService;
        _flasher = flasher;
        SerialConfigNotice = "Connect a device to apply or capture profiles.";
        Refresh();
        RefreshGallery();

        foreach (var p in PresetLibraryService.GetGamePresets())
        {
            GamePresets.Add(p);
        }

        _autoSwitchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _autoSwitchTimer.Tick += (_, __) => AutoSwitchTick();
        _autoSwitchTimer.Start();

        _deviceState.DeviceChanged += OnDeviceChanged;

        AutoApplyLastProfile = _appSettings.AutoApplyLastProfile;
    }

    [RelayCommand]
    private void Refresh()
    {
        Profiles.Clear();
        foreach (var p in _profiles.LoadProfiles())
        {
            Profiles.Add(p);
        }
    }

    [RelayCommand]
    private void RefreshGallery()
    {
        Gallery.Clear();
        if (!Directory.Exists(AppPaths.WheelProfilesPath))
        {
            Directory.CreateDirectory(AppPaths.WheelProfilesPath);
        }
        foreach (var file in Directory.GetFiles(AppPaths.WheelProfilesPath, "*.wheelprofile"))
        {
            try
            {
                var pkg = _wheelProfiles.Import(file);
                Gallery.Add(pkg);
            }
            catch
            {
            }
        }
    }

    [RelayCommand]
    private async Task CaptureFromDeviceAsync()
    {
        if (!EnsureSerialConfig())
        {
            return;
        }
        try
        {
            var cfg = await _settings.LoadFromDeviceAsync(CancellationToken.None);
            if (cfg == null)
            {
                _logger.Warn("Unable to read device settings.");
                return;
            }
            var profile = new Profile
            {
                Name = string.IsNullOrWhiteSpace(NewProfileName) ? $"Profile {DateTime.Now:HHmmss}" : NewProfileName,
                Notes = NewProfileNotes,
                Config = cfg,
                Curve = _tuningState.CurrentCurve,
                Advanced = _tuningState.CurrentAdvanced,
                FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion,
                GameExecutables = ParseGameExecutables(GameExecutables)
            };
            _profiles.SaveProfile(profile);
            Refresh();
            _logger.Info("Profile saved.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Capture failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ApplySelectedAsync()
    {
        if (SelectedProfile == null)
        {
            _logger.Warn("Select a profile first.");
            return;
        }
        if (!EnsureSerialConfig())
        {
            return;
        }

        try
        {
            await ApplyProfileAsync(SelectedProfile);
            _logger.Info("Profile applied.");
            _snapshots.CreateSnapshot(new SnapshotEntry
            {
                Kind = SnapshotKind.ApplyProfile,
                ProfileName = SelectedProfile.Name,
                Config = SelectedProfile.Config,
                Curve = SelectedProfile.Curve,
                Advanced = SelectedProfile.Advanced,
                FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion
            });
        }
        catch (Exception ex)
        {
            _logger.Error($"Apply profile failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CompareSelected()
    {
        DiffLines.Clear();
        if (CompareLeft == null || CompareRight == null)
        {
            DiffLines.Add("Select two profiles to compare.");
            return;
        }

        var a = CompareLeft.Config;
        var b = CompareRight.Config;
        AddDiff(DiffLines, "Rotation", a.RotationDeg, b.RotationDeg);
        AddDiff(DiffLines, "General", a.GeneralGain, b.GeneralGain);
        AddDiff(DiffLines, "Damper", a.DamperGain, b.DamperGain);
        AddDiff(DiffLines, "Friction", a.FrictionGain, b.FrictionGain);
        AddDiff(DiffLines, "Inertia", a.InertiaGain, b.InertiaGain);
        AddDiff(DiffLines, "Spring", a.SpringGain, b.SpringGain);
        AddDiff(DiffLines, "Constant", a.ConstantGain, b.ConstantGain);
        AddDiff(DiffLines, "Periodic", a.PeriodicGain, b.PeriodicGain);
        AddDiff(DiffLines, "Center", a.CenterGain, b.CenterGain);
        AddDiff(DiffLines, "Endstop", a.StopGain, b.StopGain);
        AddDiff(DiffLines, "MinTorque", a.MinTorque, b.MinTorque);
    }

    [RelayCommand]
    private async Task ApplyGamePresetAsync()
    {
        if (SelectedGamePreset == null)
        {
            _logger.Warn("Select a game preset first.");
            return;
        }
        if (!EnsureSerialConfig())
        {
            return;
        }

        await ApplyProfileAsync(new Profile { Name = SelectedGamePreset.Name, Config = SelectedGamePreset.Config });
        _logger.Info($"Applied preset: {SelectedGamePreset.Name}");
    }

    [RelayCommand]
    private async Task RestoreDefaultsAsync()
    {
        if (!EnsureSerialConfig())
        {
            return;
        }

        var preset = PresetLibraryService.GetWheelPresets().FirstOrDefault();
        if (preset == null)
        {
            _logger.Warn("No default preset available.");
            return;
        }
        await ApplyProfileAsync(new Profile { Name = preset.Name, Config = preset.Config });
        _logger.Info("Restored default preset.");
    }

    [RelayCommand]
    private void ExportSelected()
    {
        if (SelectedProfile == null)
        {
            _logger.Warn("Select a profile first.");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Wheel Profile (*.wheelprofile)|*.wheelprofile",
            FileName = $"{SelectedProfile.Name}.wheelprofile"
        };
        if (dialog.ShowDialog() == true)
        {
            _wheelProfiles.Export(SelectedProfile, dialog.FileName, _appSettings.LastKnownGoodHex, IncludeFirmwareInExport);
            _logger.Info("Wheel profile exported.");
        }
    }

    [RelayCommand]
    private void ImportWheelProfile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Wheel Profile (*.wheelprofile)|*.wheelprofile"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var pkg = _wheelProfiles.Import(dialog.FileName);
            var dest = Path.Combine(AppPaths.WheelProfilesPath, Path.GetFileName(dialog.FileName));
            File.Copy(dialog.FileName, dest, true);
            pkg.SourcePath = dest;
            Gallery.Add(pkg);
            SelectedGallery = pkg;
            BuildImportWarnings(pkg);
            BuildImportDiff(pkg);
            _logger.Info("Wheel profile imported.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Import failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ApplyGalleryAsync()
    {
        if (SelectedGallery?.Profile == null)
        {
            _logger.Warn("Select a gallery profile first.");
            return;
        }
        if (!EnsureSerialConfig())
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedGallery.FirmwareHexPath) && File.Exists(SelectedGallery.FirmwareHexPath))
        {
            var result = System.Windows.MessageBox.Show(
                "This wheelprofile includes firmware. Flash it before applying settings?",
                "Firmware Included",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.No);

            if (result == System.Windows.MessageBoxResult.Yes && _deviceState.CurrentDevice != null)
            {
                var progress = new Progress<string>(_ => { });
                await _flasher.FlashWithRetryAsync(SelectedGallery.FirmwareHexPath, _deviceState.CurrentDevice.Port, progress, CancellationToken.None);
            }
        }

        await ApplyProfileAsync(SelectedGallery.Profile);
        if (SelectedGallery.Layout != null)
        {
            _layoutService.Save(SelectedGallery.Layout);
        }
        _snapshots.CreateSnapshot(new SnapshotEntry
        {
            Kind = SnapshotKind.ApplyProfile,
            ProfileName = SelectedGallery.Profile.Name,
            Config = SelectedGallery.Profile.Config,
            Curve = SelectedGallery.Profile.Curve,
            Advanced = SelectedGallery.Profile.Advanced,
            FirmwareVersion = SelectedGallery.Profile.FirmwareVersion
        });
        _logger.Info("Gallery profile applied.");
    }

    [RelayCommand]
    private void DeleteGallery()
    {
        if (SelectedGallery?.SourcePath == null)
        {
            return;
        }
        try
        {
            File.Delete(SelectedGallery.SourcePath);
            RefreshGallery();
        }
        catch (Exception ex)
        {
            _logger.Error($"Delete failed: {ex.Message}");
        }
    }

    private async Task ApplyProfileAsync(Profile profile)
    {
        var cfg = profile.Config;
        await _settings.ApplyConfigAsync(cfg, CancellationToken.None);
        _tuningState.UpdateConfig(cfg);
        _appSettings.LastProfileName = profile.Name;
        _settingsService.Save(_appSettings);
    }

    private void AddDiff(ObservableCollection<string> target, string name, int a, int b)
    {
        if (a != b)
        {
            target.Add($"{name}: {a} -> {b}");
        }
    }

    private bool EnsureSerialConfig()
    {
        if (_deviceState.IsDemoMode)
        {
            _logger.Info("Demo mode: serial apply skipped.");
            return false;
        }
        if (_deviceState.CurrentDevice == null)
        {
            _logger.Warn("Connect a device first.");
            return false;
        }
        if (!_deviceState.CurrentDevice.SupportsSerialConfig && !_deviceState.CurrentDevice.IsDemo)
        {
            _logger.Warn("Serial config not supported by firmware.");
            return false;
        }
        return true;
    }

    private void OnDeviceChanged(DeviceInfo? info)
    {
        if (info == null)
        {
            CanUseSerialConfig = false;
            SerialConfigNotice = "Connect a device to apply or capture profiles.";
            return;
        }
        CanUseSerialConfig = info.SupportsSerialConfig || info.IsDemo;
        SerialConfigNotice = CanUseSerialConfig ? string.Empty : "Serial config not supported by firmware.";
    }

    partial void OnAutoApplyLastProfileChanged(bool value)
    {
        _appSettings.AutoApplyLastProfile = value;
        _settingsService.Save(_appSettings);
    }

    private List<string> ParseGameExecutables(string input)
    {
        return input.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().TrimEnd(".exe".ToCharArray()))
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void AutoSwitchTick()
    {
        if (!AutoSwitchEnabled || _deviceState.CurrentDevice == null || !_deviceState.CurrentDevice.SupportsSerialConfig)
        {
            AutoSwitchStatus = "Auto-switch is off";
            return;
        }

        var processes = System.Diagnostics.Process.GetProcesses();
        foreach (var profile in Profiles)
        {
            foreach (var exe in profile.GameExecutables)
            {
                var match = processes.FirstOrDefault(p => string.Equals(p.ProcessName, exe, StringComparison.OrdinalIgnoreCase));
                if (match != null && _lastAutoProfile != profile.Name)
                {
                    _ = ApplyProfileAsync(profile);
                    _lastAutoProfile = profile.Name;
                    AutoSwitchStatus = $"Auto-switched to {profile.Name}";
                    return;
                }
            }
        }
    }

    private void BuildImportWarnings(WheelProfilePackage pkg)
    {
        ImportWarnings.Clear();
        if (pkg.Manifest.OptionLetters != null && _deviceState.CurrentDevice != null)
        {
            if (!_deviceState.CurrentDevice.FirmwareVersion.Contains(pkg.Manifest.OptionLetters, StringComparison.OrdinalIgnoreCase))
            {
                ImportWarnings.Add("Firmware option letters differ from the connected wheel.");
            }
        }

        if (_deviceState.CurrentDevice == null)
        {
            ImportWarnings.Add("No device connected; profile will apply on next connect.");
        }
    }

    private void BuildImportDiff(WheelProfilePackage pkg)
    {
        ImportDiffLines.Clear();
        if (pkg.Profile?.Config == null || _tuningState.CurrentConfig == null)
        {
            ImportDiffLines.Add("No current config to compare.");
            return;
        }
        var a = _tuningState.CurrentConfig;
        var b = pkg.Profile.Config;
        AddDiff(ImportDiffLines, "Rotation", a.RotationDeg, b.RotationDeg);
        AddDiff(ImportDiffLines, "General", a.GeneralGain, b.GeneralGain);
        AddDiff(ImportDiffLines, "Damper", a.DamperGain, b.DamperGain);
        AddDiff(ImportDiffLines, "Friction", a.FrictionGain, b.FrictionGain);
        AddDiff(ImportDiffLines, "Inertia", a.InertiaGain, b.InertiaGain);
        AddDiff(ImportDiffLines, "Spring", a.SpringGain, b.SpringGain);
        AddDiff(ImportDiffLines, "Endstop", a.StopGain, b.StopGain);
    }
}
