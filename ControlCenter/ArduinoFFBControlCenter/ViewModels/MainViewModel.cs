using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;
using ArduinoFFBControlCenter.Views;

namespace ArduinoFFBControlCenter.ViewModels;

/// <summary>
/// Root app-shell viewmodel.
/// Owns global services, navigation, connection state, and app-wide mode toggles.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private bool _firstRunPromptShown;
    private readonly Stack<NavItem> _backStack = new();
    private NavItem? _lastNavItem;
    private bool _isNavigatingBack;
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
    private readonly ThemeService _themeService;

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

    [ObservableProperty]
    private bool isAiSidebarEnabled = true;

    [ObservableProperty]
    private string aiSidebarPrompt = string.Empty;

    [ObservableProperty]
    private string aiSidebarStatus = "AI ready.";

    [ObservableProperty]
    private bool aiSidebarBusy;

    [ObservableProperty]
    private bool isSidebarDetailsExpanded;

    [ObservableProperty]
    private string sidebarCalibrationStatus = "—";

    [ObservableProperty]
    private string sidebarSaveState = "—";

    [ObservableProperty]
    private double sidebarClippingPercent = double.NaN;

    [ObservableProperty]
    private bool? sidebarOscillationDetected;

    [ObservableProperty]
    private double sidebarEncoderNoise = double.NaN;

    [ObservableProperty]
    private string sidebarPhoneDashboardStatus = "Stopped";

    public ObservableCollection<OllamaChatEntry> AiSidebarMessages { get; } = new();

    public string GlyphConnection => GlyphHelper.Get("connection");
    public string GlyphFirmware => GlyphHelper.Get("firmware");
    public string GlyphCalibration => GlyphHelper.Get("calibration");
    public string GlyphSave => GlyphHelper.Get("save");
    public string GlyphHealth => GlyphHelper.Get("health");
    public string GlyphPhone => GlyphHelper.Get("phone");
    public string GlyphQr => GlyphHelper.Get("qr");
    public string SidebarDetailsChevronGlyph => IsSidebarDetailsExpanded ? GlyphHelper.Get("chevronUp") : GlyphHelper.Get("chevronDown");

    public bool CanGoBack => _backStack.Count > 0;

    public MainViewModel()
    {
        // Service bootstrap kept explicit for readability.
        // This app is intentionally single-process/single-device oriented.
        _logger = new LoggerService();
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _themeService = new ThemeService();
        _themeService.ApplyTheme(_settings.ThemeMode);
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

        // Build all pages once and keep them alive in navigation list.
        BuildNavigation();
        ScanPorts();

        _serial.Disconnected += OnSerialDisconnected;
        _telemetry.SamplesUpdated += OnSidebarSamplesUpdated;
        _telemetry.StatsUpdated += OnSidebarStatsUpdated;
        _calibration.StatusChanged += OnSidebarCalibrationChanged;
        _deviceSettings.PersistenceChanged += OnSidebarPersistenceChanged;
        _dashboardHost.StateChanged += OnSidebarDashboardChanged;

        OnSidebarCalibrationChanged(_calibration.CurrentAssessment);
        OnSidebarPersistenceChanged(_deviceSettings.PersistenceState);
        OnSidebarDashboardChanged(_dashboardHost.State);

        if (!string.IsNullOrWhiteSpace(_settings.LastPort))
        {
            SelectedPort = _settings.LastPort;
        }

        if (!string.IsNullOrWhiteSpace(_settings.LastFlashStatus))
        {
            LastFlashStatus = _settings.LastFlashStatus!;
        }

        // Restore persisted UI mode flags.
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
        IsAiSidebarEnabled = _settings.AiChatEnabled;

        // Restore last tuning state so UI opens with previous values.
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

        // Optional local phone dashboard autostart.
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
        if (_lastNavItem != null && value != null && !ReferenceEquals(_lastNavItem, value) && !_isNavigatingBack)
        {
            _backStack.Push(_lastNavItem);
        }

        _lastNavItem = value;
        OnPropertyChanged(nameof(CanGoBack));

        if (value?.TargetViewModel != null)
        {
            CurrentViewModel = value.TargetViewModel;
            _settings.LastNavKey = value.Key;
            _settingsService.Save(_settings);
        }
    }

    private void BuildNavigation()
    {
        // One viewmodel per module keeps each area isolated and testable.
        var home = new HomeViewModel(_logger, _hid, _telemetry, _deviceState, _calibration, _deviceSettings, _dashboardHost, _snapshots);
        var wizard = new SetupWizardViewModel(_logger, _deviceManager, _flasher, _firmwareLibrary, _profiles, _deviceState, _tuningState, _calibration, _deviceSettings, _wizardState, _customBuilder, _settingsService, _settings, _snapshots);
        var calibration = new CalibrationViewModel(_logger, _calibration, _deviceSettings, _deviceState, _capabilities, _tuningState, _snapshots);
        var firmware = new FirmwareViewModel(_logger, _flasher, _firmwareLibrary, _settingsService, _settings, _deviceManager, _protocol, _snapshots, _customBuilder, _wizardState);
        var ffb = new FfbTuningViewModel(_logger, _deviceSettings, _deviceState, _uiMode, _tuningState, _capabilities, _snapshots);
        var steering = new SteeringViewModel(_logger, _deviceSettings, _deviceState, _tuningState);
        var buttons = new ButtonsViewModel(_logger, _hid, _deviceState);
        var pedals = new PedalsViewModel(_pedals, _hid, _wizardState, _settings);
        var profiles = new ProfilesViewModel(_logger, _profiles, _deviceSettings, _deviceState, _tuningState, _settingsService, _settings, _wheelProfiles, _snapshots, _dashboardLayout, _flasher);
        var telemetry = new TelemetryViewModel(_logger, _telemetry, _deviceState, _hid, _pedals, _tuningState);
        var snapshots = new SnapshotsViewModel(_logger, _snapshots, _flasher, _deviceSettings, _deviceState, _tuningState, _telemetry);
        var selfTest = new SelfTestViewModel(_logger, _selfTest, _snapshots, _telemetry, _deviceState);
        var phoneDashboard = new PhoneDashboardViewModel(_logger, _dashboardHost, _settingsService, _settings);
        var lab = new LabToolsViewModel(_logger, _hid, _deviceState, _calibration, _settingsService, _settings);
        var diagnostics = new DiagnosticsViewModel(_logger, _diagnostics, _settingsService, _settings, _deviceState, _telemetry, _tuningState, _wizardState);
        var settingsVm = new SettingsViewModel(_settingsService, _settings, _themeService);

        NavigationItems.Add(new NavItem { Key = "home", IconGlyph = GlyphHelper.Get("home"), Title = "Home Dashboard", TargetViewModel = home, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[0]) });
        NavigationItems.Add(new NavItem { Key = "wizard", IconGlyph = GlyphHelper.Get("setup"), Title = "Setup Wizard", TargetViewModel = wizard, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[1]) });
        NavigationItems.Add(new NavItem { Key = "calibration", IconGlyph = GlyphHelper.Get("calibration"), Title = "Calibration", TargetViewModel = calibration, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[2]) });
        NavigationItems.Add(new NavItem { Key = "firmware", IconGlyph = GlyphHelper.Get("firmware"), Title = "Firmware", TargetViewModel = firmware, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[3]) });
        NavigationItems.Add(new NavItem { Key = "ffb", IconGlyph = GlyphHelper.Get("ffb"), Title = "FFB Settings", TargetViewModel = ffb, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[4]) });
        NavigationItems.Add(new NavItem { Key = "steering", IconGlyph = GlyphHelper.Get("steering"), Title = "Steering", TargetViewModel = steering, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[5]) });
        NavigationItems.Add(new NavItem { Key = "pedals", IconGlyph = GlyphHelper.Get("pedals"), Title = "Pedals", TargetViewModel = pedals, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[6]) });
        NavigationItems.Add(new NavItem { Key = "buttons", IconGlyph = GlyphHelper.Get("buttons"), Title = "Buttons", TargetViewModel = buttons, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[7]) });
        NavigationItems.Add(new NavItem { Key = "profiles", IconGlyph = GlyphHelper.Get("profiles"), Title = "Profiles", TargetViewModel = profiles, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[8]) });
        NavigationItems.Add(new NavItem { Key = "telemetry", IconGlyph = GlyphHelper.Get("telemetry"), Title = "Telemetry", TargetViewModel = telemetry, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[9]) });
        NavigationItems.Add(new NavItem { Key = "snapshots", IconGlyph = GlyphHelper.Get("timeline"), Title = "Snapshots", TargetViewModel = snapshots, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[10]) });
        NavigationItems.Add(new NavItem { Key = "selftest", IconGlyph = GlyphHelper.Get("selftest"), Title = "Self-Test", TargetViewModel = selfTest, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[11]) });
        NavigationItems.Add(new NavItem { Key = "phone", IconGlyph = GlyphHelper.Get("phone"), Title = "Phone Dashboard", TargetViewModel = phoneDashboard, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[12]) });
        NavigationItems.Add(new NavItem { Key = "lab", IconGlyph = GlyphHelper.Get("tools"), Title = "Lab Tools", TargetViewModel = lab, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[13]) });
        NavigationItems.Add(new NavItem { Key = "diagnostics", IconGlyph = GlyphHelper.Get("diagnostics"), Title = "Diagnostics", TargetViewModel = diagnostics, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[14]) });
        NavigationItems.Add(new NavItem { Key = "settings", IconGlyph = GlyphHelper.Get("settings"), Title = "Settings", TargetViewModel = settingsVm, SelectCommand = new RelayCommand(() => SelectedNavItem = NavigationItems[15]) });

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
    private void ToggleSidebarDetails()
    {
        IsSidebarDetailsExpanded = !IsSidebarDetailsExpanded;
    }

    [RelayCommand]
    private void NavigatePhoneDashboard()
    {
        NavigateTo("phone");
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
        // Demo mode intentionally blocks real serial connect.
        if (IsDemoMode)
        {
            _logger.Warn("Demo mode is active. Disable demo mode to connect.");
            return;
        }

        if (_serial.IsConnected)
        {
            // Graceful disconnect path.
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
            // Connect -> sync settings -> attach HID -> start telemetry.
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
                // Conservative default: trust wheel values unless user chooses otherwise.
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

    [RelayCommand]
    private void GoBack()
    {
        if (_backStack.Count == 0)
        {
            return;
        }

        _isNavigatingBack = true;
        try
        {
            SelectedNavItem = _backStack.Pop();
        }
        finally
        {
            _isNavigatingBack = false;
            OnPropertyChanged(nameof(CanGoBack));
        }
    }

    [RelayCommand]
    private void ClearAiSidebar()
    {
        AiSidebarMessages.Clear();
        AiSidebarStatus = "AI chat cleared.";
    }

    [RelayCommand]
    private async Task AskAiSidebarAsync()
    {
        var prompt = AiSidebarPrompt?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            AiSidebarStatus = "Type a question first.";
            return;
        }

        if (!IsAiSidebarEnabled)
        {
            AiSidebarStatus = "AI sidebar is disabled. Enable it in Settings.";
            return;
        }

        AiSidebarBusy = true;
        AiSidebarMessages.Add(new OllamaChatEntry
        {
            Role = "user",
            Content = prompt,
            TimestampLocal = DateTime.Now
        });
        AiSidebarPrompt = string.Empty;

        // Local command actions first (fast and deterministic).
        var local = TryApplyLocalAiAction(prompt);
        if (local.Applied)
        {
            AiSidebarMessages.Add(new OllamaChatEntry
            {
                Role = "assistant",
                Content = local.Message,
                TimestampLocal = DateTime.Now
            });
            AiSidebarStatus = "Applied local action.";
            AiSidebarBusy = false;
            return;
        }

        var history = AiSidebarMessages.ToList();
        var contextualPrompt = BuildAiContextPrompt(prompt);

        (bool Success, string Content, string Error) result;
        var provider = _settings.AiProvider ?? "Ollama";
        if (string.Equals(provider, "ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(_settings.AiApiKey))
            {
                AiSidebarStatus = "API provider selected but API key is empty (Settings -> AI Provider).";
                AiSidebarBusy = false;
                return;
            }

            var model = string.IsNullOrWhiteSpace(_settings.AiModel) ? "gpt-4o-mini" : _settings.AiModel!;
            result = await _ollama.AskOpenAiCompatAsync(
                _settings.AiEndpoint ?? "https://api.openai.com/v1",
                _settings.AiApiKey!,
                model,
                history,
                contextualPrompt,
                _settings.AiUserName,
                _settings.AiUserEmail,
                CancellationToken.None);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_settings.AiModel))
            {
                AiSidebarStatus = "Select/pull an Ollama model first.";
                AiSidebarBusy = false;
                return;
            }

            result = await _ollama.AskAsync(
                _settings.AiEndpoint ?? _settings.OllamaEndpoint ?? "http://localhost:11434",
                _settings.AiModel!,
                history,
                contextualPrompt,
                null,
                CancellationToken.None);
        }

        AiSidebarMessages.Add(new OllamaChatEntry
        {
            Role = "assistant",
            Content = result.Success ? result.Content : $"[Error] {result.Error}",
            TimestampLocal = DateTime.Now
        });
        AiSidebarStatus = result.Success ? "AI response received." : "AI request failed.";
        AiSidebarBusy = false;
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
        // Demo mode injects a fake device and synthetic telemetry for UI exploration.
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
            DeviceFirmwareVersion = "—";
            DevicePort = "—";
            DeviceVidPid = "—";
            DeviceProduct = "—";
            DeviceCapabilitiesText = "—";
            FirmwareUpdateStatus = "—";
            SidebarClippingPercent = double.NaN;
            SidebarOscillationDetected = null;
            SidebarEncoderNoise = double.NaN;
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
        FirmwareUpdateStatus = match != null ? "Up-to-date" : "Update available";
    }

    private void OnSettingsSaved(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.LastFlashStatus))
        {
            LastFlashStatus = settings.LastFlashStatus!;
        }

        _themeService.ApplyTheme(settings.ThemeMode);

        if (IsBeginnerMode != settings.BeginnerMode)
        {
            IsBeginnerMode = settings.BeginnerMode;
        }

        if (IsDemoMode != settings.DemoMode)
        {
            IsDemoMode = settings.DemoMode;
        }

        if (IsAiSidebarEnabled != settings.AiChatEnabled)
        {
            IsAiSidebarEnabled = settings.AiChatEnabled;
        }
    }

    partial void OnIsAiSidebarEnabledChanged(bool value)
    {
        _settings.AiChatEnabled = value;
        _settingsService.Save(_settings);
    }

    partial void OnIsSidebarDetailsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarDetailsChevronGlyph));
    }

    private void OnSidebarCalibrationChanged(CalibrationAssessment assessment)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SidebarCalibrationStatus = !assessment.IsSupported
                ? "—"
                : assessment.NeedsCalibration ? "Not calibrated ⚠" : "Calibrated ✅";
        });
    }

    private void OnSidebarPersistenceChanged(SettingsPersistenceState state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SidebarSaveState = state switch
            {
                SettingsPersistenceState.SavedToWheel => "Saved to Wheel",
                SettingsPersistenceState.SavedToPc => "Saved to PC",
                SettingsPersistenceState.UnsavedChanges => "Unsaved changes",
                _ => "—"
            };
        });
    }

    private void OnSidebarDashboardChanged(DashboardHostState state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SidebarPhoneDashboardStatus = state.IsRunning ? "Running" : "Stopped";
        });
    }

    private void OnSidebarStatsUpdated(TelemetryStats stats)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SidebarClippingPercent = stats.ClippingPercent;
        });
    }

    private void OnSidebarSamplesUpdated()
    {
        var samples = _telemetry.GetSamplesSnapshot();
        if (samples.Count < 3)
        {
            return;
        }

        var window = samples.TakeLast(200).ToList();
        var zeroCrossings = 0;
        var lastSign = Math.Sign(window[0].Velocity);
        for (var i = 1; i < window.Count; i++)
        {
            var sign = Math.Sign(window[i].Velocity);
            if (sign != 0 && lastSign != 0 && sign != lastSign)
            {
                zeroCrossings++;
            }

            if (sign != 0)
            {
                lastSign = sign;
            }
        }

        var jitter = 0d;
        for (var i = 1; i < window.Count; i++)
        {
            jitter += Math.Abs(window[i].Angle - window[i - 1].Angle);
        }

        jitter /= Math.Max(1, window.Count - 1);

        Application.Current.Dispatcher.Invoke(() =>
        {
            SidebarOscillationDetected = zeroCrossings > 18;
            SidebarEncoderNoise = Math.Round(Math.Min(100, jitter * 10), 1);
        });
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
        static bool IsValidLength(double? value, double min, double max) =>
            value.HasValue && double.IsFinite(value.Value) && value.Value >= min && value.Value <= max;

        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;
        var screenWidth = Math.Max(800d, SystemParameters.VirtualScreenWidth);
        var screenHeight = Math.Max(600d, SystemParameters.VirtualScreenHeight);
        var maxWidth = Math.Max(900d, screenWidth);
        var maxHeight = Math.Max(700d, screenHeight);

        if (IsValidLength(_settings.WindowWidth, 900, maxWidth))
        {
            window.Width = _settings.WindowWidth!.Value;
        }

        if (IsValidLength(_settings.WindowHeight, 620, maxHeight))
        {
            window.Height = _settings.WindowHeight!.Value;
        }

        var hasLeft = IsValidLength(_settings.WindowLeft, screenLeft - screenWidth, screenLeft + (screenWidth * 2));
        var hasTop = IsValidLength(_settings.WindowTop, screenTop - screenHeight, screenTop + (screenHeight * 2));
        if (hasLeft && hasTop)
        {
            var clampedLeft = Math.Clamp(_settings.WindowLeft!.Value, screenLeft, screenLeft + screenWidth - 120);
            var clampedTop = Math.Clamp(_settings.WindowTop!.Value, screenTop, screenTop + screenHeight - 120);
            window.Left = clampedLeft;
            window.Top = clampedTop;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public void CaptureWindowState(Window window)
    {
        if (double.IsFinite(window.Width) && window.Width > 0)
        {
            _settings.WindowWidth = window.Width;
        }

        if (double.IsFinite(window.Height) && window.Height > 0)
        {
            _settings.WindowHeight = window.Height;
        }

        if (double.IsFinite(window.Left))
        {
            _settings.WindowLeft = window.Left;
        }

        if (double.IsFinite(window.Top))
        {
            _settings.WindowTop = window.Top;
        }

        _settingsService.Save(_settings);
    }

    private (bool Applied, string Message) TryApplyLocalAiAction(string prompt)
    {
        // Simple global navigation intents.
        var navTarget = Regex.Match(prompt, @"\b(go to|open|switch to)\s+(home|setup|wizard|firmware|tuning|profiles|timeline|snapshots|diagnostics|telemetry|phone|settings)\b", RegexOptions.IgnoreCase);
        if (navTarget.Success)
        {
            var key = navTarget.Groups[2].Value.ToLowerInvariant();
            key = key switch
            {
                "wizard" => "wizard",
                "setup" => "wizard",
                "timeline" => "snapshots",
                _ => key
            };
            NavigateTo(key);
            return (true, $"Navigated to {key}.");
        }

        if (CurrentViewModel is SetupWizardViewModel wizard)
        {
            var result = wizard.ApplyNaturalLanguageConfig(prompt);
            if (result.Applied)
            {
                return (true, result.Summary);
            }
        }

        if (CurrentViewModel is FfbTuningViewModel tuning)
        {
            var result = tuning.ApplyNaturalLanguageTuning(prompt);
            if (result.Applied)
            {
                return (true, result.Summary);
            }
        }

        return (false, string.Empty);
    }

    private string BuildAiContextPrompt(string prompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are assisting inside a wheel control center desktop app.");
        sb.AppendLine($"Current page: {SelectedNavItem?.Title ?? "Unknown"}");

        if (CurrentViewModel is SetupWizardViewModel wizard)
        {
            sb.AppendLine("Page context:");
            sb.AppendLine($"- Step: {wizard.StepIndex + 1}");
            sb.AppendLine($"- RPWM: {wizard.RpwmPin}, LPWM: {wizard.LpwmPin}");
            sb.AppendLine($"- Encoder A/B: {wizard.EncoderAPin}/{wizard.EncoderBPin}");
            sb.AppendLine($"- PWM mode: {wizard.PwmMode}");
        }
        else if (CurrentViewModel is FfbTuningViewModel tuning)
        {
            sb.AppendLine("Page context:");
            sb.AppendLine($"- Strength: {tuning.GeneralGain}");
            sb.AppendLine($"- Damping/Friction/Inertia: {tuning.DamperGain}/{tuning.FrictionGain}/{tuning.InertiaGain}");
            sb.AppendLine($"- Spring: {tuning.SpringGain}");
        }

        sb.AppendLine("User request:");
        sb.Append(prompt);
        return sb.ToString();
    }

    public void EnsureFirstRunProfile(Window owner)
    {
        if (_firstRunPromptShown)
        {
            return;
        }

        _firstRunPromptShown = true;
        if (!string.IsNullOrWhiteSpace(_settings.AiUserName) &&
            !string.IsNullOrWhiteSpace(_settings.AiUserEmail) &&
            !string.IsNullOrWhiteSpace(_settings.AiProvider))
        {
            return;
        }

        var dialog = new FirstRunProfileWindow(_settings.AiUserName, _settings.AiUserEmail)
        {
            Owner = owner
        };

        var accepted = dialog.ShowDialog() == true;
        if (!accepted)
        {
            return;
        }

        _settings.AiUserName = dialog.UserName;
        _settings.AiUserEmail = dialog.UserEmail;
        _settings.AiProvider = dialog.Provider;
        _settings.AiEndpoint = dialog.Endpoint;
        _settings.AiApiKey = dialog.ApiKey;
        _settings.AiChatEnabled = true;
        _settingsService.Save(_settings);
    }
}
