using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class ButtonsViewModel : ViewModelBase
{
    private readonly HidWheelService _hid;
    private readonly ButtonLabelService _labels;
    private readonly DeviceStateService _deviceState;

    public ObservableCollection<ButtonState> Buttons { get; } = new();

    [ObservableProperty] private double wheelAngle;
    [ObservableProperty] private bool canShowButtons;
    [ObservableProperty] private string notice = "Connect a device to view buttons.";

    public ButtonsViewModel(LoggerService logger, HidWheelService hid, DeviceStateService deviceState)
    {
        _hid = hid;
        _labels = new ButtonLabelService();
        _deviceState = deviceState;
        for (int i = 0; i < 24; i++)
        {
            Buttons.Add(new ButtonState { Index = i + 1, Label = $"Button {i + 1}", IsPressed = false });
        }

        var saved = _labels.Load();
        foreach (var b in Buttons)
        {
            if (saved.TryGetValue(b.Index, out var label))
            {
                b.Label = label;
            }
        }

        _hid.StateUpdated += OnHidUpdate;
        _deviceState.DeviceChanged += OnDeviceChanged;
    }

    private void OnHidUpdate()
    {
        var states = _hid.Buttons;
        Application.Current.Dispatcher.Invoke(() =>
        {
            WheelAngle = _hid.WheelAngle;
            for (int i = 0; i < Buttons.Count && i < states.Length; i++)
            {
                Buttons[i].IsPressed = states[i];
            }
        });
    }

    private void OnDeviceChanged(DeviceInfo? info)
    {
        if (info == null)
        {
            CanShowButtons = false;
            Notice = "Connect a device to view buttons.";
            return;
        }

        CanShowButtons = true;
        Notice = string.Empty;
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SaveLabels()
    {
        _labels.Save(Buttons);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ResetLabels()
    {
        for (int i = 0; i < Buttons.Count; i++)
        {
            Buttons[i].Label = $"Button {Buttons[i].Index}";
        }
        _labels.Save(Buttons);
    }
}
