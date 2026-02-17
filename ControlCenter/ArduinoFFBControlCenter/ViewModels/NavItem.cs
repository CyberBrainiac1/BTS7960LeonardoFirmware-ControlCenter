using System.Windows.Input;

namespace ArduinoFFBControlCenter.ViewModels;

public class NavItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uE8A5";
    public ICommand? SelectCommand { get; set; }
    public ViewModelBase? TargetViewModel { get; set; }
}
