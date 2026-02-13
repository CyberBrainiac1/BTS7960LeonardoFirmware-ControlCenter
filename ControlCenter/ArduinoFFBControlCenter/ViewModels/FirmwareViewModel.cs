using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class FirmwareViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly FirmwareFlasherService _flasher;
    private readonly FirmwareLibraryService _library;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly DeviceManagerService _deviceManager;
    private readonly DeviceProtocolService _protocol;
    private readonly SnapshotService _snapshots;

    public ObservableCollection<FirmwareHexInfo> FirmwareOptions { get; } = new();
    public ObservableCollection<string> AvailablePorts { get; } = new();

    [ObservableProperty]
    private FirmwareHexInfo? selectedFirmware;

    [ObservableProperty]
    private string? selectedPort;

    [ObservableProperty]
    private string flashLog = string.Empty;

    [ObservableProperty]
    private bool isFlashing;

    [ObservableProperty]
    private string? customHexPath;

    [ObservableProperty]
    private string flashStatus = "Idle";

    [ObservableProperty]
    private string flashHint = string.Empty;

    [ObservableProperty]
    private string lastGoodHexName = "None";

    [ObservableProperty]
    private bool canRollback;

    public FirmwareViewModel(LoggerService logger, FirmwareFlasherService flasher, FirmwareLibraryService library, SettingsService settingsService, AppSettings settings, DeviceManagerService deviceManager, DeviceProtocolService protocol, SnapshotService snapshots)
    {
        _logger = logger;
        _flasher = flasher;
        _library = library;
        _settingsService = settingsService;
        _settings = settings;
        _deviceManager = deviceManager;
        _protocol = protocol;
        _snapshots = snapshots;

        ReloadLibrary();
        ScanPorts();
        UpdateLastGood();
    }

    [RelayCommand]
    private void ReloadLibrary()
    {
        FirmwareOptions.Clear();
        foreach (var hex in _library.LoadLibrary())
        {
            FirmwareOptions.Add(hex);
        }

        if (!string.IsNullOrWhiteSpace(_settings.LastFirmwareHex))
        {
            SelectedFirmware = FirmwareOptions.FirstOrDefault(f => f.Path == _settings.LastFirmwareHex) ?? FirmwareOptions.FirstOrDefault();
        }
        else
        {
            SelectedFirmware = FirmwareOptions.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void BrowseCustomHex()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "HEX files (*.hex)|*.hex|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            CustomHexPath = dialog.FileName;
            var custom = new FirmwareHexInfo
            {
                Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                Path = dialog.FileName,
                Notes = "Custom HEX"
            };
            FirmwareOptions.Insert(0, custom);
            SelectedFirmware = custom;
        }
    }

    [RelayCommand]
    private void ScanPorts()
    {
        AvailablePorts.Clear();
        foreach (var port in _deviceManager.ScanPorts())
        {
            AvailablePorts.Add(port);
        }
        if (!string.IsNullOrWhiteSpace(_settings.LastPort))
        {
            SelectedPort = _settings.LastPort;
        }
    }

    [RelayCommand]
    private async Task AutoDetectPortAsync()
    {
        var detected = await _deviceManager.AutoDetectPortAsync();
        if (!string.IsNullOrWhiteSpace(detected))
        {
            SelectedPort = detected;
            _logger.Info($"Auto-detected port: {detected}");
        }
        else
        {
            _logger.Warn("Auto-detect failed. Select port manually.");
        }
    }

    [RelayCommand]
    private async Task FlashSelectedAsync()
    {
        if (SelectedFirmware == null)
        {
            _logger.Warn("Select a HEX file first.");
            return;
        }
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            _logger.Warn("Select a COM port first.");
            return;
        }

        FlashLog = string.Empty;
        IsFlashing = true;
        FlashStatus = "Flashing...";
        FlashHint = string.Empty;
        var progress = new Progress<string>(line =>
        {
            FlashLog += line + "\n";
        });

        var result = await _flasher.FlashWithRetryAsync(SelectedFirmware.Path, SelectedPort, progress, CancellationToken.None);
        IsFlashing = false;

        if (result.Success)
        {
            _settings.LastFirmwareHex = SelectedFirmware.Path;
            _settings.LastPort = SelectedPort;
            _settings.LastKnownGoodHex = SelectedFirmware.Path;
            _settings.LastFlashStatus = "Success";
            _settings.LastFlashUtc = DateTime.UtcNow;
            _settingsService.Save(_settings);
            UpdateLastGood();
            FlashStatus = "Flash complete.";
            FlashHint = "Verifying firmware...";
            _snapshots.CreateSnapshot(new SnapshotEntry
            {
                Kind = SnapshotKind.Flash,
                Label = $"Flash {SelectedFirmware.Name}",
                FirmwareHexPath = SelectedFirmware.Path,
                FirmwareVersion = SelectedFirmware.Name
            });
            if (!_deviceManager.IsConnected)
            {
                try
                {
                    await Task.Delay(1500);
                    var verifyPort = await _deviceManager.AutoDetectPortAsync();
                    if (!string.IsNullOrWhiteSpace(verifyPort))
                    {
                        var info = await _deviceManager.ConnectAsync(verifyPort);
                        _deviceManager.Disconnect();
                        FlashHint = $"Verified firmware: {info.FirmwareVersion}";
                    }
                    else
                    {
                        FlashHint = "Flash complete. Firmware verify skipped (no port).";
                    }
                }
                catch
                {
                    FlashHint = "Flash complete. Firmware verify skipped.";
                }
            }
            return;
        }

        _settings.LastFlashStatus = result.UserMessage;
        _settings.LastFlashUtc = DateTime.UtcNow;
        _settingsService.Save(_settings);
        FlashStatus = result.UserMessage;
        FlashHint = string.IsNullOrWhiteSpace(result.SuggestedAction) ? "See log for details." : result.SuggestedAction;
    }

    [RelayCommand]
    private async Task ManualRecoveryAsync()
    {
        if (SelectedFirmware == null || string.IsNullOrWhiteSpace(SelectedPort))
        {
            _logger.Warn("Select HEX and COM port first.");
            return;
        }

        FlashLog = string.Empty;
        IsFlashing = true;
        FlashStatus = "Manual Recovery...";
        FlashHint = "Press reset twice quickly, then wait for bootloader.";
        var progress = new Progress<string>(line => FlashLog += line + "\n");

        var result = await _flasher.FlashWithRetryAsync(SelectedFirmware.Path, SelectedPort, progress, CancellationToken.None, skipReset: true);
        IsFlashing = false;
        FlashStatus = result.Success ? "Manual recovery complete." : result.UserMessage;
        FlashHint = result.Success ? string.Empty : result.SuggestedAction;
    }

    [RelayCommand]
    private async Task ResetBoardAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            _logger.Warn("Select a COM port first.");
            return;
        }

        if (_deviceManager.IsConnected)
        {
            _deviceManager.Disconnect();
            await Task.Delay(150);
        }

        IsFlashing = true;
        FlashStatus = "Resetting board...";
        FlashHint = string.Empty;
        FlashLog = string.Empty;
        var progress = new Progress<string>(line => FlashLog += line + "\n");

        var result = await _flasher.ResetBoardAsync(SelectedPort, progress, CancellationToken.None);
        IsFlashing = false;
        FlashStatus = result.UserMessage;
        FlashHint = result.Success
            ? $"Bootloader detected on {result.BootloaderPort}. Wait for the normal COM port to reappear."
            : result.SuggestedAction;

        ScanPorts();
        if (!string.IsNullOrWhiteSpace(_settings.LastPort) && AvailablePorts.Contains(_settings.LastPort))
        {
            SelectedPort = _settings.LastPort;
        }
    }

    [RelayCommand]
    private async Task RollbackAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.LastKnownGoodHex) || !File.Exists(_settings.LastKnownGoodHex))
        {
            _logger.Warn("No last-known-good HEX found.");
            return;
        }

        var hex = _settings.LastKnownGoodHex;
        SelectedFirmware = FirmwareOptions.FirstOrDefault(f => f.Path == hex) ?? new FirmwareHexInfo
        {
            Name = Path.GetFileNameWithoutExtension(hex),
            Path = hex,
            Notes = "Last Known Good"
        };

        await FlashSelectedAsync();
    }

    [RelayCommand]
    private async Task FactoryRestoreAsync()
    {
        var stable = FirmwareOptions.FirstOrDefault(f => f.Name.Contains("v250", StringComparison.OrdinalIgnoreCase));
        if (stable == null)
        {
            _logger.Warn("No stable v250 HEX found in library.");
            return;
        }
        SelectedFirmware = stable;
        await FlashSelectedAsync();
    }

    [RelayCommand]
    private void OpenLegacyGui()
    {
        var legacyPath = Path.Combine(AppContext.BaseDirectory, "Assets", "LegacyGUI", "wheel_control.exe");
        if (!File.Exists(legacyPath))
        {
            _logger.Warn("Legacy GUI not found in Assets/LegacyGUI.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = legacyPath,
            UseShellExecute = true
        });
    }

    private void UpdateLastGood()
    {
        if (!string.IsNullOrWhiteSpace(_settings.LastKnownGoodHex) && File.Exists(_settings.LastKnownGoodHex))
        {
            LastGoodHexName = Path.GetFileName(_settings.LastKnownGoodHex);
            CanRollback = true;
        }
        else
        {
            LastGoodHexName = "None";
            CanRollback = false;
        }
    }
}
