using System.Text.Json;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class DashboardLayoutService
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public DashboardLayout Load()
    {
        if (!File.Exists(AppPaths.DashboardLayoutFile))
        {
            var layout = CreateDefaultLayout();
            Save(layout);
            return layout;
        }

        try
        {
            var json = File.ReadAllText(AppPaths.DashboardLayoutFile);
            var layout = JsonSerializer.Deserialize<DashboardLayout>(json, _options);
            return layout ?? CreateDefaultLayout();
        }
        catch
        {
            return CreateDefaultLayout();
        }
    }

    public void Save(DashboardLayout layout)
    {
        var json = JsonSerializer.Serialize(layout, _options);
        File.WriteAllText(AppPaths.DashboardLayoutFile, json);
    }

    private DashboardLayout CreateDefaultLayout()
    {
        return new DashboardLayout
        {
            Columns = 12,
            Preferences = new DashboardPreferences
            {
                SpeedUnit = "kph",
                AngleUnit = "deg"
            },
            Pages = new List<DashboardPage>
            {
                new DashboardPage
                {
                    Id = "drive",
                    Name = "Drive",
                    Widgets = new List<DashboardWidget>
                    {
                        new DashboardWidget { Type = "numeric", Field = "speed", Label = "Speed", X = 0, Y = 0, W = 6, H = 2 },
                        new DashboardWidget { Type = "numeric", Field = "gear", Label = "Gear", X = 6, Y = 0, W = 3, H = 2 },
                        new DashboardWidget { Type = "numeric", Field = "rpm", Label = "RPM", X = 9, Y = 0, W = 3, H = 2 },
                        new DashboardWidget { Type = "bar", Field = "throttle", Label = "Throttle", X = 0, Y = 2, W = 6, H = 1 },
                        new DashboardWidget { Type = "bar", Field = "brake", Label = "Brake", X = 6, Y = 2, W = 6, H = 1 },
                        new DashboardWidget { Type = "status", Field = "status", Label = "Status", X = 0, Y = 3, W = 12, H = 2 },
                        new DashboardWidget { Type = "graph", Field = "torque", Label = "Torque", X = 0, Y = 5, W = 12, H = 3 }
                    }
                },
                new DashboardPage
                {
                    Id = "ffb",
                    Name = "FFB",
                    Widgets = new List<DashboardWidget>
                    {
                        new DashboardWidget { Type = "numeric", Field = "angle", Label = "Angle", X = 0, Y = 0, W = 6, H = 2 },
                        new DashboardWidget { Type = "numeric", Field = "torque", Label = "Torque", X = 6, Y = 0, W = 6, H = 2 },
                        new DashboardWidget { Type = "bar", Field = "clipping", Label = "Clipping", X = 0, Y = 2, W = 12, H = 2 },
                        new DashboardWidget { Type = "graph", Field = "angle", Label = "Angle", X = 0, Y = 4, W = 12, H = 3 }
                    }
                },
                new DashboardPage
                {
                    Id = "diag",
                    Name = "Diagnostics",
                    Widgets = new List<DashboardWidget>
                    {
                        new DashboardWidget { Type = "numeric", Field = "rate", Label = "Telemetry Hz", X = 0, Y = 0, W = 4, H = 2 },
                        new DashboardWidget { Type = "numeric", Field = "loopdt", Label = "Loop dt", X = 4, Y = 0, W = 4, H = 2 },
                        new DashboardWidget { Type = "bar", Field = "clipping", Label = "Clipping", X = 8, Y = 0, W = 4, H = 2 },
                        new DashboardWidget { Type = "status", Field = "status", Label = "Status", X = 0, Y = 2, W = 12, H = 2 }
                    }
                }
            }
        };
    }
}
