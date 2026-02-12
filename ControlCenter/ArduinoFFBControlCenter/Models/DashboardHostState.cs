namespace ArduinoFFBControlCenter.Models;

public class DashboardHostState
{
    public bool IsRunning { get; set; }
    public int Port { get; set; }
    public string? PrimaryAddress { get; set; }
    public List<string> Urls { get; set; } = new();
    public bool RequirePin { get; set; }
    public bool AdvancedRemote { get; set; }
}
