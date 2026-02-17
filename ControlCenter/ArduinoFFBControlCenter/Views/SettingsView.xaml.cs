using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace ArduinoFFBControlCenter.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch
        {
            // No-op: keep UI responsive even if shell launch is blocked.
        }
    }
}
