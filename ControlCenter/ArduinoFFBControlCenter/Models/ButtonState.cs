using CommunityToolkit.Mvvm.ComponentModel;

namespace ArduinoFFBControlCenter.Models;

public partial class ButtonState : ObservableObject
{
    public int Index { get; set; }

    [ObservableProperty]
    private string label = string.Empty;

    [ObservableProperty]
    private bool isPressed;

    public ButtonState()
    {
    }

    public ButtonState(int index)
    {
        Index = index;
        Label = $"Button {index}";
    }
}
