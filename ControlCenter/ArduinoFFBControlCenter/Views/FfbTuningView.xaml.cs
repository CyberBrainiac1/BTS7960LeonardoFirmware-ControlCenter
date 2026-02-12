using System.Windows.Controls;

namespace ArduinoFFBControlCenter.Views;

public partial class FfbTuningView : UserControl
{
    public FfbTuningView()
    {
        InitializeComponent();
    }

    private void UnlockStrength_OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.FfbTuningViewModel vm)
        {
            vm.BeginStrengthUnlock();
        }
    }

    private void UnlockStrength_OnMouseUp(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (DataContext is ViewModels.FfbTuningViewModel vm)
        {
            vm.CancelStrengthUnlock();
        }
    }
}
