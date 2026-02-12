using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class LabToolsViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly HidWheelService _hid;
    private readonly DeviceStateService _deviceState;
    private readonly FfbTestService _ffb;
    private readonly CalibrationService _calibration;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _appSettings;
    private readonly DispatcherTimer _armTimer;
    private DateTime _armStart;
    private bool _arming;
    private DateTime _lastStateUpdate = DateTime.UtcNow;
    private int _lastAngle;

    public ObservableCollection<Models.ButtonState> ButtonStates { get; } = new();

    [ObservableProperty] private int wheelAngle;
    [ObservableProperty] private string testStatus = "Test tools idle";
    [ObservableProperty] private int testMagnitude = 2000;
    [ObservableProperty] private int testFrequencyMs = 800;
    [ObservableProperty] private int springStrength = 4000;
    [ObservableProperty] private int damperStrength = 4000;
    [ObservableProperty] private bool isArmed;
    [ObservableProperty] private double armProgress;
    [ObservableProperty] private double inputLatencyMs;
    [ObservableProperty] private string noiseStatus = "Noise: unknown";
    [ObservableProperty] private bool isFfbAttached;
    [ObservableProperty] private bool canRunTests;
    [ObservableProperty] private bool calibrationRequired;
    [ObservableProperty] private bool bypassCalibrationWarning;
    [ObservableProperty] private string calibrationNotice = string.Empty;

    public LabToolsViewModel(LoggerService logger, HidWheelService hid, DeviceStateService deviceState, CalibrationService calibration, SettingsService settingsService, AppSettings appSettings)
    {
        _logger = logger;
        _hid = hid;
        _deviceState = deviceState;
        _calibration = calibration;
        _settingsService = settingsService;
        _appSettings = appSettings;
        _ffb = new FfbTestService(_logger);

        for (int i = 0; i < 32; i++)
        {
            ButtonStates.Add(new Models.ButtonState(i + 1));
        }

        _hid.StateUpdated += OnStateUpdated;
        _armTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _armTimer.Tick += (_, __) => TickArm();

        BypassCalibrationWarning = _appSettings.BypassCalibrationWarning;
        _calibration.StatusChanged += OnCalibrationChanged;
    }

    private void OnStateUpdated()
    {
        var now = DateTime.UtcNow;
        var dt = (now - _lastStateUpdate).TotalMilliseconds;
        _lastStateUpdate = now;

        var angle = (int)_hid.WheelAngle;
        var delta = Math.Abs(angle - _lastAngle);
        _lastAngle = angle;
        var noise = delta > 200 ? "Noise: high" : "Noise: ok";

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            InputLatencyMs = dt;
            WheelAngle = angle;
            NoiseStatus = noise;
            for (int i = 0; i < ButtonStates.Count && i < _hid.Buttons.Length; i++)
            {
                ButtonStates[i].IsPressed = _hid.Buttons[i];
            }
        });
    }

    public void BeginArm()
    {
        if (IsArmed)
        {
            return;
        }
        _arming = true;
        _armStart = DateTime.UtcNow;
        ArmProgress = 0;
        _armTimer.Start();
    }

    public void CancelArm()
    {
        _arming = false;
        _armTimer.Stop();
        ArmProgress = 0;
    }

    private void TickArm()
    {
        if (!_arming)
        {
            return;
        }
        var elapsed = (DateTime.UtcNow - _armStart).TotalMilliseconds;
        ArmProgress = Math.Clamp(elapsed / 2000.0, 0, 1);
        if (elapsed >= 2000)
        {
            IsArmed = true;
            _armTimer.Stop();
            _arming = false;
            TestStatus = "Armed. Start with low strength.";
            UpdateCanRun();
        }
    }

    private bool EnsureArmed()
    {
        if (!IsArmed)
        {
            TestStatus = "Hold ARM TEST to enable.";
            return false;
        }
        return true;
    }

    [RelayCommand]
    private void AttachFfbDevice()
    {
        var devices = _hid.EnumerateDevices();
        var target = devices.FirstOrDefault(d => d.ProductName.Contains("Arduino", StringComparison.OrdinalIgnoreCase)) ?? devices.FirstOrDefault();
        if (target == null)
        {
            TestStatus = "No HID device detected.";
            return;
        }
        if (_ffb.Attach(target.InstanceGuid))
        {
            TestStatus = $"FFB tester attached to {target.ProductName}";
            IsFfbAttached = true;
            UpdateCanRun();
        }
    }

    [RelayCommand]
    private void TestConstant()
    {
        if (!EnsureArmed())
        {
            return;
        }
        _ffb.PlayConstant(Math.Clamp(TestMagnitude, -10000, 10000));
        TestStatus = "Constant force running.";
    }

    [RelayCommand]
    private void TestSine()
    {
        if (!EnsureArmed())
        {
            return;
        }
        _ffb.PlaySine(Math.Clamp(TestMagnitude, 0, 10000), Math.Clamp(TestFrequencyMs, 200, 3000));
        TestStatus = "Sine wave running.";
    }

    [RelayCommand]
    private void TestSpring()
    {
        if (!EnsureArmed())
        {
            return;
        }
        _ffb.PlaySpring(Math.Clamp(SpringStrength, 0, 10000));
        TestStatus = "Spring test running.";
    }

    [RelayCommand]
    private void TestDamper()
    {
        if (!EnsureArmed())
        {
            return;
        }
        _ffb.PlayDamper(Math.Clamp(DamperStrength, 0, 10000));
        TestStatus = "Damper test running.";
    }

    [RelayCommand]
    private void StopAll()
    {
        _ffb.StopAll();
        TestStatus = "Tests stopped.";
    }

    partial void OnIsArmedChanged(bool value)
    {
        UpdateCanRun();
    }

    private void UpdateCanRun()
    {
        CanRunTests = IsArmed && IsFfbAttached && (!CalibrationRequired || BypassCalibrationWarning);
    }

    private void OnCalibrationChanged(Models.CalibrationAssessment assessment)
    {
        CalibrationRequired = !assessment.IsSupported || assessment.NeedsCalibration;
        if (!assessment.IsSupported)
        {
            CalibrationNotice = "Calibration status unknown. Connect the wheel or bypass at your own risk.";
        }
        else
        {
            CalibrationNotice = assessment.NeedsCalibration
                ? "Calibration required before tests. You can bypass at your own risk."
                : string.Empty;
        }
        UpdateCanRun();
    }

    partial void OnBypassCalibrationWarningChanged(bool value)
    {
        _appSettings.BypassCalibrationWarning = value;
        _settingsService.Save(_appSettings);
        UpdateCanRun();
    }
}
