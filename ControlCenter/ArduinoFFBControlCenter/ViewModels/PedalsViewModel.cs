using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class PedalsViewModel : ViewModelBase
{
    private readonly PedalTelemetryService _pedals;
    private readonly HidWheelService _hid;
    private readonly WizardStateService _wizardState;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;

    public ObservableCollection<PedalAxisSource> AxisOptions { get; } = new();

    [ObservableProperty] private bool hasPedals;
    [ObservableProperty] private string wiringSummary = "Pedal wiring not configured.";
    [ObservableProperty] private string status = "Connect the wheel to view pedal input.";

    [ObservableProperty] private PedalAxisSource throttleAxis;
    [ObservableProperty] private PedalAxisSource brakeAxis;
    [ObservableProperty] private PedalAxisSource clutchAxis;

    [ObservableProperty] private double throttleValue;
    [ObservableProperty] private double brakeValue;
    [ObservableProperty] private double clutchValue;
    [ObservableProperty] private int throttleRaw;
    [ObservableProperty] private int brakeRaw;
    [ObservableProperty] private int clutchRaw;

    [ObservableProperty] private int throttleMin;
    [ObservableProperty] private int throttleMax;
    [ObservableProperty] private bool throttleInverted;

    [ObservableProperty] private int brakeMin;
    [ObservableProperty] private int brakeMax;
    [ObservableProperty] private bool brakeInverted;

    [ObservableProperty] private int clutchMin;
    [ObservableProperty] private int clutchMax;
    [ObservableProperty] private bool clutchInverted;

    public PedalsViewModel(PedalTelemetryService pedals, HidWheelService hid, WizardStateService wizardState, AppSettings settings)
    {
        _pedals = pedals;
        _hid = hid;
        _wizardState = wizardState;
        _settings = settings;

        AxisOptions.Add(PedalAxisSource.None);
        AxisOptions.Add(PedalAxisSource.Y);
        AxisOptions.Add(PedalAxisSource.Z);
        AxisOptions.Add(PedalAxisSource.Rx);
        AxisOptions.Add(PedalAxisSource.Ry);
        AxisOptions.Add(PedalAxisSource.Rz);
        AxisOptions.Add(PedalAxisSource.Slider0);
        AxisOptions.Add(PedalAxisSource.Slider1);

        LoadWiring();
        LoadMapping();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _timer.Tick += (_, __) => UpdateLive();
        _timer.Start();
    }

    private void LoadWiring()
    {
        var state = _wizardState.Load();
        if (state?.Wiring != null)
        {
            HasPedals = state.Wiring.HasPedals;
            WiringSummary = HasPedals
                ? $"Throttle {state.Wiring.ThrottlePin}, Brake {state.Wiring.BrakePin}, Clutch {state.Wiring.ClutchPin}"
                : "No pedals configured in the setup wizard.";
            if (!HasPedals)
            {
                Status = "Pedals not configured.";
            }
        }
    }

    private void LoadMapping()
    {
        var mapping = _settings.PedalMapping ?? new PedalAxisMapping();
        var cal = _settings.PedalCalibration ?? new PedalCalibration();
        ThrottleAxis = mapping.ThrottleSource;
        BrakeAxis = mapping.BrakeSource;
        ClutchAxis = mapping.ClutchSource;

        ThrottleMin = cal.Throttle.Min;
        ThrottleMax = cal.Throttle.Max;
        ThrottleInverted = cal.Throttle.Inverted;

        BrakeMin = cal.Brake.Min;
        BrakeMax = cal.Brake.Max;
        BrakeInverted = cal.Brake.Inverted;

        ClutchMin = cal.Clutch.Min;
        ClutchMax = cal.Clutch.Max;
        ClutchInverted = cal.Clutch.Inverted;
    }

    private void UpdateLive()
    {
        if (!_hid.IsAttached)
        {
            Status = "HID not attached. Connect the wheel to view pedal input.";
            return;
        }

        var sample = _pedals.GetSample();
        ThrottleRaw = sample.RawThrottle;
        BrakeRaw = sample.RawBrake;
        ClutchRaw = sample.RawClutch;
        ThrottleValue = sample.Throttle ?? 0;
        BrakeValue = sample.Brake ?? 0;
        ClutchValue = sample.Clutch ?? 0;
        if (Status != "Pedal input live.")
        {
            Status = "Pedal input live.";
        }
    }

    private void SaveMapping()
    {
        var mapping = new PedalAxisMapping
        {
            ThrottleSource = ThrottleAxis,
            BrakeSource = BrakeAxis,
            ClutchSource = ClutchAxis
        };
        _pedals.UpdateMapping(mapping);
    }

    private void SaveCalibration()
    {
        var cal = new PedalCalibration
        {
            Throttle = new PedalAxisCalibration { Min = ThrottleMin, Max = ThrottleMax, Inverted = ThrottleInverted },
            Brake = new PedalAxisCalibration { Min = BrakeMin, Max = BrakeMax, Inverted = BrakeInverted },
            Clutch = new PedalAxisCalibration { Min = ClutchMin, Max = ClutchMax, Inverted = ClutchInverted }
        };
        _pedals.UpdateCalibration(cal);
    }

    [RelayCommand]
    private async Task DetectThrottleAsync()
    {
        if (!_hid.IsAttached)
        {
            Status = "Connect the wheel to detect axes.";
            return;
        }
        Status = "Move the throttle pedal now...";
        var axis = await _pedals.DetectAxisAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
        if (axis == null)
        {
            Status = "No axis movement detected.";
            return;
        }
        ThrottleAxis = axis.Value;
        Status = $"Throttle assigned to {axis.Value}.";
    }

    [RelayCommand]
    private async Task DetectBrakeAsync()
    {
        if (!_hid.IsAttached)
        {
            Status = "Connect the wheel to detect axes.";
            return;
        }
        Status = "Move the brake pedal now...";
        var axis = await _pedals.DetectAxisAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
        if (axis == null)
        {
            Status = "No axis movement detected.";
            return;
        }
        BrakeAxis = axis.Value;
        Status = $"Brake assigned to {axis.Value}.";
    }

    [RelayCommand]
    private async Task DetectClutchAsync()
    {
        if (!_hid.IsAttached)
        {
            Status = "Connect the wheel to detect axes.";
            return;
        }
        Status = "Move the clutch pedal now...";
        var axis = await _pedals.DetectAxisAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
        if (axis == null)
        {
            Status = "No axis movement detected.";
            return;
        }
        ClutchAxis = axis.Value;
        Status = $"Clutch assigned to {axis.Value}.";
    }

    [RelayCommand]
    private void SetThrottleMin() { ThrottleMin = _pedals.GetRaw(ThrottleAxis); }
    [RelayCommand]
    private void SetThrottleMax() { ThrottleMax = _pedals.GetRaw(ThrottleAxis); }
    [RelayCommand]
    private void SetBrakeMin() { BrakeMin = _pedals.GetRaw(BrakeAxis); }
    [RelayCommand]
    private void SetBrakeMax() { BrakeMax = _pedals.GetRaw(BrakeAxis); }
    [RelayCommand]
    private void SetClutchMin() { ClutchMin = _pedals.GetRaw(ClutchAxis); }
    [RelayCommand]
    private void SetClutchMax() { ClutchMax = _pedals.GetRaw(ClutchAxis); }

    [RelayCommand]
    private void ResetCalibration()
    {
        ThrottleMin = 0;
        ThrottleMax = 65535;
        BrakeMin = 0;
        BrakeMax = 65535;
        ClutchMin = 0;
        ClutchMax = 65535;
        ThrottleInverted = false;
        BrakeInverted = false;
        ClutchInverted = false;
        SaveCalibration();
        Status = "Calibration reset.";
    }

    partial void OnThrottleAxisChanged(PedalAxisSource value) => SaveMapping();
    partial void OnBrakeAxisChanged(PedalAxisSource value) => SaveMapping();
    partial void OnClutchAxisChanged(PedalAxisSource value) => SaveMapping();
    partial void OnThrottleMinChanged(int value) => SaveCalibration();
    partial void OnThrottleMaxChanged(int value) => SaveCalibration();
    partial void OnBrakeMinChanged(int value) => SaveCalibration();
    partial void OnBrakeMaxChanged(int value) => SaveCalibration();
    partial void OnClutchMinChanged(int value) => SaveCalibration();
    partial void OnClutchMaxChanged(int value) => SaveCalibration();
    partial void OnThrottleInvertedChanged(bool value) => SaveCalibration();
    partial void OnBrakeInvertedChanged(bool value) => SaveCalibration();
    partial void OnClutchInvertedChanged(bool value) => SaveCalibration();
}
