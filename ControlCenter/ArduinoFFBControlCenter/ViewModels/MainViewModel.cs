using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly SerialDeviceService _serial;
    private readonly DeviceProtocolService _protocol;
    private readonly DeviceManagerService _deviceManager;
    private readonly HidWheelService _hid;
    private readonly TelemetryService _telemetry;
    private readonly ProfileService _profiles;
    private readonly FirmwareFlasherService _flasher;
    private readonly FirmwareLibraryService _firmwareLibrary;
    private readonly DiagnosticsService _diagnostics;
    private readonly DeviceStateService _deviceState;
    private readonly UiModeService _uiMode;
    private readonly TuningStateService _tuningState;
    private readonly DeviceCapabilitiesService _capabilities;
    private readonly HidTelemetryService _hidTelemetry;
    private readonly DeviceSettingsService _deviceSettings;
    private readonly CalibrationService _calibration;
    private readonly WizardStateService _wizardState;
    private readonly CustomFirmwareBuilderService _customBuilder;
    private readonly PedalTelemetryService _pedals;
    private readonly DashboardLayoutService _dashboardLayout;
    private readonly SimTelemetryService _simTelemetry;
    private readonly DashboardTelemetryService _dashboardTelemetry;
    private readonly DashboardHostService _dashboardHost;
    private readonly SnapshotService _snapshots;
    private readonly WheelProfileService _wheelProfiles;
    private readonly SelfTestService _selfTest;
    private readonly ScreenCaptureService _screenCapture;
    private readonly OllamaService _ollama;

    public ObservableCollection<NavItem> NavigationItems { get; } = new();
    public ObservableCollection<string> AvailablePorts { get; } = new();

    [ObservableProperty]
    private NavItem? selectedNavItem;

    [ObservableProperty]
    private ViewModelBase? currentViewModel;

    [ObservableProperty]
    private string statusText = "Disconnected";

    [ObservableProperty]
    private Brush statusBrush = Brushes.Gray;

    [ObservableProperty]
    private string deviceSummary = "No device connected";

    [ObservableProperty]
    private string? selectedPort;

    [ObservableProperty]
    private DeviceInfo? currentDevice;

    [ObservableProperty]
    private string connectButtonText = "Connect";

    [ObservableProperty]
    private string deviceFirmwareVersion = "Unknown";

    [ObservableProperty]
    private string devicePort = "-";

    [ObservableProperty]
    private string deviceVidPid = "-";

    [ObservableProperty]
    private string deviceProduct = "-";

    [ObservableProperty]
    private string deviceCapabilitiesText = "-";

    [ObservableProperty]
    private string firmwareUpdateStatus = "Unknown";

    [ObservableProperty]
    private string lastFlashStatus = "No flash yet";

    [ObservableProperty]
    private bool isBeginnerMode = true;

    [ObservableProperty]
    private bool isKidMode;

    [ObservableProperty]
    private bool isDemoMode;

    public MainViewModel()
    {
        _logger = new LoggerService();
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _settingsService.Saved += OnSettingsSaved;
        _serial = new SerialDeviceService(_logger);
        _protocol = new DeviceProtocolService(_serial, _logger);
        _deviceManager = new DeviceManagerService(_serial, _protocol, _logger);
        _hid = new HidWheelService(_logger);
        _telemetry = new TelemetryService(_hid, _serial, _logger);
        _profiles = new ProfileService();
        _flasher = new FirmwareFlasherService(_logger);
        _firmwareLibrary = new FirmwareLibraryService(Path.Combine(AppContext.BaseDirectory, "Assets", "FirmwareLibrary", "Leonardo"));
        _diagnostics = new DiagnosticsService(_logger, _settingsService);
        _deviceState = new DeviceStateService();
        _uiMode = new UiModeService();
        _tuningState = new TuningStateService();
        _capabilities = new DeviceCapabilitiesService();
        _hidTelemetry = new HidTelemetryService(_hid, _logger);
        _deviceSettings = new DeviceSettingsService(_logger, _protocol, _deviceState, _profiles, _settingsService, _settings, _tuningState, _capabilities);
        _calibration = new CalibrationService(_logger, _deviceState, _capabilities, _hidTelemetry, _settingsService, _settings, _deviceSettings);
        _wizardState = new WizardStateService();
        _customBuilder = new CustomFirmwareBuilderService();
        _pedals = new PedalTelemetryService(_hid, _settingsService, _settings);
        _dashboardLayout = new DashboardLayoutService();
        _simTelemetry = new SimTelemetryService();
        _dashboardTelemetry = new DashboardTelemetryService(_telemetry, _deviceState, _calibration, _deviceSettings, _tuningState, _simTelemetry, _pedals);
        _dashboardHost = new DashboardHostService(_logger, _settingsService, _settings, _dashboardLayout, _dashboardTelemetry, _deviceSettings, _profiles, _calibration, _deviceState);
        _snapshots = new SnapshotService(_logger);
        _wheelProfiles = new WheelProfileService(_dashboardLayout);
        _selfTest = new SelfTestService(_hid, _hidTelemetry, _logger);
        _screenCapture = new ScreenCaptureService();
        _ollama = new OllamaService(_logger);

        BuildNavigation();
        ScanPorts();

        _serial.Disconnected += OnSerialDisconnected;

        if (!string.IsNullOrWhiteSpace(_settings.LastPort))
        {
            SelectedPort = _settings.LastPort;
        }

        if (!string.IsNullOrWhiteSpace(_settings.LastFlashStatus))
        {
            LastFlashStatus = _settings.LastFlashStatus!;
        }

        IsBeginnerMode = _settings.BeginnerMode;
        _uiMode.SetBeginnerMode(IsBeginnerMode);
        IsKidMode = false;
        _settings.KidMode = false;
        _uiMode.SetKidMode(false);
        _settingsService.Save(_settings);
        IsDemoMode = _settings.DemoMode;
        if (IsDemoMode)
        {
            EnterDemoMode();
        }

        if (_settings.LastTuningConfig != null)
        {
            _tuningState.UpdateConfig(_settings.LastTuningConfig);
        }
        if (_settings.LastCurve != null)
        {
            _tuningState.UpdateCurve(_settings.LastCurve);
        }
        if (_settings.LastAdvanced != null)
        {
            _tuningState.UpdateAdvanced(_settings.LastAdvanced);
        }
        _tuningState.StateChanged += PersistTuningState;

        if (_settings.DashboardEnabled)
        {
            _ = StartDashboardAsync();
        }
    }

    private async Task StartDashboardAsync()
    {
        try
        {
            await _dashboardHost.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.Error($"Dashboard failed to start: {ex.Message}");
        }
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value?.TargetViewModel != null)
        {
            CurrentViewModel = value.TargetViewModel;
            _settings.LastNavKey = value.Key;
            _settingsService.Save(_settings);
        }
    }

    private void BuildNavigation()
    {
        var home = new HomeViewModel(_logger, _hid, _telemetry, _deviceState, _calibration, _deviceSettings);
        var wizard = new SetupWizardViewModel(_logger, _deviceManager, _flasher, _firmwareLibrary, _profiles, _deviceState, _tuningState, _calibration, _deviceSettings, _wizardState, _customBuilder, _settingsService, _settings, _snapshots);
        var calibration = new CalibrationViewModel(_logger, _calibration, _deviceSettings, _deviceState, _capabilities, _tuningState, _snapshots);
        var firmware = new FirmwareViewModel(_logger, _flasher, _firmwareLibrary, _settingsService, _settings, _deviceManager, _protocol, _snapshots);
        var ffb = new FfbTuningViewModel(_logger, _deviceSettings, _deviceState, _uiMode, _tuningState, _capabilities, _snapshots);
        var steering = new SteeringViewModel(_logger, _deviceSettings, _deviceState, _tuningState);
        var buttons = new ButtonsViewModel(_logger, _hid, _deviceState);
        var pedals = new PedalsViewModel(_pedals, _hid, _wizardState, _settings);
        var profiles = new ProfilesViewModel(_logger, _profiles, _deviceSettings, _deviceState, _tuningState, _settingsService, _settings, _wheelProfiles, _snapshots, _dashboardLayout, _flasher);
        var telemetry = new TelemetryViewModel(_logger, _telemetry, _deviceState, _hid, _pedals, _tuningState);
        var snapshots = new SnapshotsViewModel(_logger, _snapshots, _flasher, _deviceSettings, _deviceState, _tuningState, _telemetry);
        var selfTest = new SelfTestViewModel(_logger, _selfTest, _snapshots, _telemetry, _deviceState);
        var phoneDashboard = new PhoneDashboardViewModel(_logger, _dashboardHost, _settingsService, _settings);
        var ollama = new OllamaViewModel(_logger, _ollama, _screenCapture, _settingsService, _settings);
        var lab = new LabToolsViewModel(_logger, _hid, _deviceState, _calibration, _settingsService, _settings);
        var diagnostics = new DiagnosticsViewModel(_logger, _diagnostics, _settingsService, _settings, _deviceState, _telemetry, _tuningState, _wizardState);

        NavigationItems.Add(new NavItem { Key = "home", Title = "Home Dashboard", TargetViewModel = home, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[0]) });
        NavigationItems.Add(new NavItem { Key = "wizard", Title = "Setup Wizard", TargetViewModel = wizard, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[1]) });
        NavigationItems.Add(new NavItem { Key = "calibration", Title = "Calibration", TargetViewModel = calibration, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[2]) });
        NavigationItems.Add(new NavItem { Key = "firmware", Title = "Firmware", TargetViewModel = firmware, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[3]) });
        NavigationItems.Add(new NavItem { Key = "ffb", Title = "FFB Settings", TargetViewModel = ffb, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[4]) });
        NavigationItems.Add(new NavItem { Key = "steering", Title = "Steering", TargetViewModel = steering, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[5]) });
        NavigationItems.Add(new NavItem { Key = "pedals", Title = "Pedals", TargetViewModel = pedals, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[6]) });
        NavigationItems.Add(new NavItem { Key = "buttons", Title = "Buttons", TargetViewModel = buttons, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[7]) });
        NavigationItems.Add(new NavItem { Key = "profiles", Title = "Profiles", TargetViewModel = profiles, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[8]) });
        NavigationItems.Add(new NavItem { Key = "telemetry", Title = "Telemetry", TargetViewModel = telemetry, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[9]) });
        NavigationItems.Add(new NavItem { Key = "snapshots", Title = "Snapshots", TargetViewModel = snapshots, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[10]) });
        NavigationItems.Add(new NavItem { Key = "selftest", Title = "Self-Test", TargetViewModel = selfTest, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[11]) });
        NavigationItems.Add(new NavItem { Key = "phone", Title = "Phone Dashboard", TargetViewModel = phoneDashboard, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[12]) });
        NavigationItems.Add(new NavItem { Key = "ollama", Title = "AI Side View", TargetViewModel = ollama, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[13]) });
        NavigationItems.Add(new NavItem { Key = "lab", Title = "Lab Tools", TargetViewModel = lab, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[14]) });
        NavigationItems.Add(new NavItem { Key = "diagnostics", Title = "Diagnostics", TargetViewModel = diagnostics, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[15]) });

        SelectedNavItem = NavigationItems[0];

        if (!string.IsNullOrWhiteSpace(_settings.LastNavKey))
        {
            var match = NavigationItems.FirstOrDefault(n => string.Equals(n.Key, _settings.LastNavKey, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SelectedNavItem = match;
            }
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
    }

    [RelayCommand]
    private async Task ToggleConnectAsync()
    {
        if (IsDemoMode)
        {
            _logger.Warn("Demo mode is active. Disable demo mode to connect.");
            return;
        }

        if (_serial.IsConnected)
        {
            _telemetry.Stop();
            _hid.Stop();
            _deviceManager.Disconnect();
            StatusText = "Disconnected";
            StatusBrush = Brushes.Gray;
            DeviceSummary = "No device connected";
            ConnectButtonText = "Connect";
            CurrentDevice = null;
            _deviceState.SetDevice(null);
            UpdateDeviceFields(null);
            _ = _calibration.AssessAsync(null, CancellationToken.None);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            _logger.Warn("Select a COM port first.");
            return;
        }

        try
        {
            CurrentDevice = await _deviceManager.ConnectAsync(SelectedPort);
            StatusText = "Connected";
            StatusBrush = Brushes.LimeGreen;
            DeviceSummary = $"{CurrentDevice.FirmwareVersion} ({CurrentDevice.Port})";
            ConnectButtonText = "Disconnect";
            _deviceState.SetDevice(CurrentDevice);
            UpdateDeviceFields(CurrentDevice);
            _settings.LastPort = SelectedPort;
            _settingsService.Save(_settings);

            await _deviceSettings.SyncOnConnectAsync(() =>
            {
                var result = MessageBox.Show(
                    "Wheel settings differ from your last profile. Use wheel settings?",
                    "Settings Conflict",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);
                return result == MessageBoxResult.No ? SettingsConflictChoice.ApplyProfile : SettingsConflictChoice.UseWheel;
            }, CancellationToken.None);

            if (_tuningState.CurrentConfig != null)
            {
                _telemetry.UpdateMaxTorque(_tuningState.CurrentConfig.MaxTorque);
            }

            var devices = _hid.EnumerateDevices();
            var target = devices.FirstOrDefault(d => d.ProductName.Contains("Arduino", StringComparison.OrdinalIgnoreCase)) ?? devices.FirstOrDefault();
            if (target != null && _hid.Attach(target.InstanceGuid))
            {
                _hid.Start();
            }
            _telemetry.Start();

            _ = _calibration.AssessAsync(_tuningState.CurrentConfig, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Error($"Connect failed: {ex.Message}");
            StatusText = "Error";
            StatusBrush = Brushes.OrangeRed;
            ConnectButtonText = "Connect";
        }
    }

    partial void OnIsBeginnerModeChanged(bool value)
    {
        _uiMode.SetBeginnerMode(value);
        _settings.BeginnerMode = value;
        _settingsService.Save(_settings);
    }

    partial void OnIsKidModeChanged(bool value)
    {
        _uiMode.SetKidMode(value);
        _settings.KidMode = value;
        _settingsService.Save(_settings);
    }

    partial void OnIsDemoModeChanged(bool value)
    {
        _settings.DemoMode = value;
        _settingsService.Save(_settings);
        if (value)
        {
            EnterDemoMode();
        }
        else
        {
            ExitDemoMode();
        }
    }

    [RelayCommand]
    private void NavigateTo(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var match = NavigationItems.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                    ?? NavigationItems.FirstOrDefault(item => item.Title.StartsWith(key, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            SelectedNavItem = match;
        }
    }

    private void OnSerialDisconnected()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _telemetry.Stop();
            _hid.Stop();
            _deviceManager.Disconnect();
            StatusText = "Disconnected";
            StatusBrush = Brushes.Gray;
            DeviceSummary = "No device connected";
            ConnectButtonText = "Connect";
            CurrentDevice = null;
            _deviceState.SetDevice(null);
            UpdateDeviceFields(null);
            _ = _calibration.AssessAsync(null, CancellationToken.None);
        });
    }

    private void EnterDemoMode()
    {
        if (_serial.IsConnected)
        {
            _deviceManager.Disconnect();
        }
        var demo = new DeviceInfo
        {
            Port = "DEMO",
            FirmwareVersion = "fw-v250demo",
            Capabilities = CapabilityFlags.HasAS5600 | CapabilityFlags.HasButtonMatrix | CapabilityFlags.HasHatSwitch,
            SupportsInfoCommand = true,
            SupportsSerialConfig = true,
            SupportsTelemetry = true,
            IsDemo = true,
            ProductName = "Arduino FFB Wheel (Demo)"
        };
        StatusText = "Demo Mode";
        StatusBrush = Brushes.SteelBlue;
        DeviceSummary = "Demo device";
        ConnectButtonText = "Connect";
        CurrentDevice = demo;
        _deviceState.SetDemoMode(true, demo);
        UpdateDeviceFields(demo);
        _telemetry.SetDemoMode(true);
        _telemetry.Start();
        _ = _calibration.AssessAsync(_tuningState.CurrentConfig, CancellationToken.None);
    }

    private void ExitDemoMode()
    {
        _telemetry.SetDemoMode(false);
        _telemetry.Stop();
        _deviceState.SetDemoMode(false);
        _deviceState.SetDevice(null);
        StatusText = "Disconnected";
        StatusBrush = Brushes.Gray;
        DeviceSummary = "No device connected";
        CurrentDevice = null;
        UpdateDeviceFields(null);
        _ = _calibration.AssessAsync(null, CancellationToken.None);
    }

    private void UpdateDeviceFields(DeviceInfo? info)
    {
        if (info == null)
        {
            DeviceFirmwareVersion = "Unknown";
            DevicePort = "-";
            DeviceVidPid = "-";
            DeviceProduct = "-";
            DeviceCapabilitiesText = "-";
            FirmwareUpdateStatus = "Unknown";
            return;
        }

        DeviceFirmwareVersion = info.FirmwareVersion;
        DevicePort = info.Port;
        DeviceVidPid = info.Vid != null && info.Pid != null ? $"{info.Vid}:{info.Pid}" : "-";
        DeviceProduct = info.ProductName ?? "Arduino FFB Wheel";
        DeviceCapabilitiesText = CapabilityFormatter.Format(info);
        _settings.LastDeviceId = info.Vid != null && info.Pid != null ? $"{info.Vid}:{info.Pid}" : info.Port;
        _settings.LastDeviceName = info.ProductName ?? "Arduino FFB Wheel";
        _settingsService.Save(_settings);

        var match = _firmwareLibrary.LoadLibrary().FirstOrDefault(f => f.Name.Contains(info.FirmwareVersion, StringComparison.OrdinalIgnoreCase));
        FirmwareUpdateStatus = match != null ? "Library match" : "Unknown";
    }

    private void OnSettingsSaved(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.LastFlashStatus))
        {
            LastFlashStatus = settings.LastFlashStatus!;
        }
    }

    private void PersistTuningState()
    {
        _settings.LastTuningConfig = _tuningState.CurrentConfig;
        _settings.LastCurve = _tuningState.CurrentCurve;
        _settings.LastAdvanced = _tuningState.CurrentAdvanced;
        _settingsService.Save(_settings);
    }

    public void ApplyWindowState(Window window)
    {
        if (_settings.WindowWidth.HasValue) window.Width = _settings.WindowWidth.Value;
        if (_settings.WindowHeight.HasValue) window.Height = _settings.WindowHeight.Value;
        if (_settings.WindowLeft.HasValue) window.Left = _settings.WindowLeft.Value;
        if (_settings.WindowTop.HasValue) window.Top = _settings.WindowTop.Value;
    }

    public void CaptureWindowState(Window window)
    {
        _settings.WindowWidth = window.Width;
        _settings.WindowHeight = window.Height;
        _settings.WindowLeft = window.Left;
        _settings.WindowTop = window.Top;
        _settingsService.Save(_settings);
    }
}
