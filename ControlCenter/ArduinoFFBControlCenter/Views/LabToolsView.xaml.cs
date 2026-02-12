using System.Windows.Controls;

namespace ArduinoFFBControlCenter.Views;

public partial class LabToolsView : UserControl
{
    public LabToolsView()
    {
        InitializeComponent();
        ArmButton.PreviewMouseLeftButtonDown += (_, __) =>
        {
            if (DataContext is ViewModels.LabToolsViewModel vm)
            {
                vm.BeginArm();
            }
        };
        ArmButton.PreviewMouseLeftButtonUp += (_, __) =>
        {
            if (DataContext is ViewModels.LabToolsViewModel vm)
            {
                vm.CancelArm();
            }
        };
        ArmButton.MouseLeave += (_, __) =>
        {
            if (DataContext is ViewModels.LabToolsViewModel vm)
            {
                vm.CancelArm();
            }
        };
    }
}
