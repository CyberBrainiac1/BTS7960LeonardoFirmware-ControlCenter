namespace ArduinoFFBControlCenter.Models;

public class DashboardLayout
{
    public int Columns { get; set; } = 12;
    public List<DashboardPage> Pages { get; set; } = new();
    public string Theme { get; set; } = "default";
    public DashboardPreferences Preferences { get; set; } = new();
}

public class DashboardPage
{
    public string Id { get; set; } = "drive";
    public string Name { get; set; } = "Drive";
    public List<DashboardWidget> Widgets { get; set; } = new();
}

public class DashboardWidget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "numeric"; // numeric, bar, graph, status
    public string Field { get; set; } = "speed"; // speed, gear, rpm, torque, angle, clipping, etc
    public string Label { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; } = 4;
    public int H { get; set; } = 2;
}

public class DashboardPreferences
{
    public string SpeedUnit { get; set; } = "kph";
    public string AngleUnit { get; set; } = "deg";
}
