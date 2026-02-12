using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class SnapshotsViewModel : ViewModelBase
{
    private readonly SnapshotService _snapshots;
    private readonly FirmwareFlasherService _flasher;
    private readonly DeviceSettingsService _settings;
    private readonly DeviceStateService _deviceState;
    private readonly TuningStateService _tuningState;
    private readonly TelemetryService _telemetry;
    private readonly LoggerService _logger;

    public ObservableCollection<SnapshotEntry> SnapshotList { get; } = new();
    public ObservableCollection<string> DiffLines { get; } = new();
    public ObservableCollection<string> Filters { get; } = new();

    [ObservableProperty] private SnapshotEntry? selectedSnapshot;
    [ObservableProperty] private SnapshotEntry? compareLeft;
    [ObservableProperty] private SnapshotEntry? compareRight;
    [ObservableProperty] private string selectedFilter = "All";

    public SnapshotsViewModel(LoggerService logger,
        SnapshotService snapshots,
        FirmwareFlasherService flasher,
        DeviceSettingsService settings,
        DeviceStateService deviceState,
        TuningStateService tuningState,
        TelemetryService telemetry)
    {
        _logger = logger;
        _snapshots = snapshots;
        _flasher = flasher;
        _settings = settings;
        _deviceState = deviceState;
        _tuningState = tuningState;
        _telemetry = telemetry;

        Filters.Add("All");
        foreach (var kind in Enum.GetNames(typeof(SnapshotKind)))
        {
            Filters.Add(kind);
        }

        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        SnapshotList.Clear();
        var list = _snapshots.LoadSnapshots();
        foreach (var entry in list)
        {
            if (SelectedFilter != "All" && !string.Equals(entry.Kind.ToString(), SelectedFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            SnapshotList.Add(entry);
        }
    }

    [RelayCommand]
    private void CompareSelected()
    {
        DiffLines.Clear();
        foreach (var line in _snapshots.Diff(CompareLeft, CompareRight))
        {
            DiffLines.Add(line);
        }
    }

    [RelayCommand]
    private void CreateManualSnapshot()
    {
        var entry = new SnapshotEntry
        {
            Kind = SnapshotKind.Manual,
            Label = "Manual snapshot",
            FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion,
            Config = _tuningState.CurrentConfig,
            Curve = _tuningState.CurrentCurve,
            Advanced = _tuningState.CurrentAdvanced
        };
        _snapshots.CreateSnapshot(entry, _telemetry.GetSamplesSnapshot());
        Refresh();
    }

    [RelayCommand]
    private async Task RevertSettingsAsync()
    {
        if (SelectedSnapshot?.Config == null)
        {
            _logger.Warn("Snapshot has no settings.");
            return;
        }
        _snapshots.CreateSnapshot(new SnapshotEntry
        {
            Kind = SnapshotKind.Revert,
            Label = "Pre-revert (settings)",
            Config = _tuningState.CurrentConfig,
            Curve = _tuningState.CurrentCurve,
            Advanced = _tuningState.CurrentAdvanced,
            FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion
        }, _telemetry.GetSamplesSnapshot());

        await _settings.ApplyConfigAsync(SelectedSnapshot.Config, CancellationToken.None);
        _tuningState.UpdateConfig(SelectedSnapshot.Config);
        _logger.Info("Settings reverted.");
    }

    [RelayCommand]
    private async Task RevertFirmwareAsync()
    {
        if (SelectedSnapshot?.FirmwareHexPath == null || !File.Exists(SelectedSnapshot.FirmwareHexPath))
        {
            _logger.Warn("Snapshot has no firmware HEX attached.");
            return;
        }
        if (_deviceState.CurrentDevice == null)
        {
            _logger.Warn("Connect a device first.");
            return;
        }

        var progress = new Progress<string>(_ => { });
        await _flasher.FlashWithRetryAsync(SelectedSnapshot.FirmwareHexPath, _deviceState.CurrentDevice.Port, progress, CancellationToken.None);
    }

    [RelayCommand]
    private async Task RevertBothAsync()
    {
        await RevertSettingsAsync();
        await RevertFirmwareAsync();
    }

    partial void OnSelectedFilterChanged(string value)
    {
        Refresh();
    }
}
