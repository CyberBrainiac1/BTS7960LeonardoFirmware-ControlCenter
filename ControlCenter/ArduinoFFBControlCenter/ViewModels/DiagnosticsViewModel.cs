using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Services;
using ArduinoFFBControlCenter.Helpers;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class DiagnosticsViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly DiagnosticsService _diagnostics;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly DeviceStateService _deviceState;
    private readonly TelemetryService _telemetry;
    private readonly TuningStateService _tuningState;
    private readonly WizardStateService _wizardState;
    public System.Collections.ObjectModel.ObservableCollection<Models.TroubleshootItem> Troubleshooting { get; } = new();

    [ObservableProperty] private string wiringSummary = "No wiring data.";

    public DiagnosticsViewModel(LoggerService logger, DiagnosticsService diagnostics, SettingsService settingsService, AppSettings settings, DeviceStateService deviceState, TelemetryService telemetry, TuningStateService tuningState, WizardStateService wizardState)
    {
        _logger = logger;
        _diagnostics = diagnostics;
        _settingsService = settingsService;
        _settings = settings;
        _deviceState = deviceState;
        _telemetry = telemetry;
        _tuningState = tuningState;
        _wizardState = wizardState;

        Troubleshooting.Add(new Models.TroubleshootItem
        {
            Title = "Port Busy",
            Steps = "Close Arduino IDE or legacy GUI. Disconnect in the app, then retry flash."
        });
        Troubleshooting.Add(new Models.TroubleshootItem
        {
            Title = "Bootloader Not Detected",
            Steps = "Double-press reset on the Leonardo, then use Manual Recovery in Firmware page."
        });
        Troubleshooting.Add(new Models.TroubleshootItem
        {
            Title = "Wrong HEX / Mismatch",
            Steps = "Verify board is Leonardo (ATmega32U4). Select HEX with correct option letters."
        });
        Troubleshooting.Add(new Models.TroubleshootItem
        {
            Title = "Reversed Motor Direction",
            Steps = "Swap motor leads or use PWM balance (B command) to compensate."
        });
        Troubleshooting.Add(new Models.TroubleshootItem
        {
            Title = "Encoder Reversed",
            Steps = "Swap encoder A/B channels or change firmware option for direction."
        });
        Troubleshooting.Add(new Models.TroubleshootItem
        {
            Title = "No FFB in Games",
            Steps = "Check Windows Game Controllers and ensure the device shows Force Feedback."
        });

        LoadWiring();
    }

    public System.Collections.ObjectModel.ObservableCollection<Models.LogEntry> Logs => _logger.Entries;

    [RelayCommand]
    private void ExportSupportBundle()
    {
        var path = Path.Combine(AppPaths.AppDataRoot, $"support-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        var samples = _telemetry.GetSamplesSnapshot().TakeLast(12000).ToList();
        _diagnostics.CreateSupportBundle(path, _deviceState.CurrentDevice, _tuningState.CurrentConfig, samples);
        _logger.Info($"Support bundle saved to {path}");
    }

    private void LoadWiring()
    {
        var state = _wizardState.Load();
        if (state?.Wiring != null)
        {
            WiringSummary = $"RPWM {state.Wiring.RpwmPin}, LPWM {state.Wiring.LpwmPin}, EncA {state.Wiring.EncoderAPin}, EncB {state.Wiring.EncoderBPin}, Pedals {state.Wiring.ThrottlePin}/{state.Wiring.BrakePin}/{state.Wiring.ClutchPin}";
        }
    }
}
