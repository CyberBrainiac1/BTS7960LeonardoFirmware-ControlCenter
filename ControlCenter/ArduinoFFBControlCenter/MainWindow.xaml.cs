using System.Windows;

namespace ArduinoFFBControlCenter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, __) =>
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.ApplyWindowState(this);
            }
        };
        Closing += (_, __) =>
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.CaptureWindowState(this);
            }
        };
    }
}
