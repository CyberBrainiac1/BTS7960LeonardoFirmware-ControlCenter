using System.Windows;
using System.Windows.Media;

namespace ArduinoFFBControlCenter.Services;

public enum ThemeMode
{
    Light,
    SoftDark
}

public class ThemeService
{
    private static readonly Dictionary<ThemeMode, Dictionary<string, string>> Palettes = new()
    {
        [ThemeMode.Light] = new Dictionary<string, string>
        {
            ["Background0"] = "#F7FAFF",
            ["Background1"] = "#EEF4FF",
            ["Surface"] = "#FFFFFF",
            ["Surface2"] = "#F2F7FF",
            ["SurfaceHover"] = "#E6F0FF",
            ["SurfacePressed"] = "#DCEAFF",
            ["Border"] = "#C3D5F0",
            ["TextPrimary"] = "#17253A",
            ["TextSecondary"] = "#506783",
            ["Accent"] = "#248BFF",
            ["AccentStrong"] = "#0E75E2",
            ["Accent2Color"] = "#00BFA5",
            ["Success"] = "#179D67",
            ["Warning"] = "#CA7D13",
            ["Error"] = "#D84F5A",
            ["Disabled"] = "#91A7C3"
        },
        [ThemeMode.SoftDark] = new Dictionary<string, string>
        {
            ["Background0"] = "#0D131E",
            ["Background1"] = "#111A27",
            ["Surface"] = "#172335",
            ["Surface2"] = "#1C2A3F",
            ["SurfaceHover"] = "#233652",
            ["SurfacePressed"] = "#2A3E5E",
            ["Border"] = "#2D4262",
            ["TextPrimary"] = "#F6FAFF",
            ["TextSecondary"] = "#AAB8CD",
            ["Accent"] = "#3F9DFF",
            ["AccentStrong"] = "#1C84F3",
            ["Accent2Color"] = "#19C795",
            ["Success"] = "#2FB871",
            ["Warning"] = "#E8B045",
            ["Error"] = "#E8686C",
            ["Disabled"] = "#6D7E99"
        }
    };

    private static readonly string[] ColorKeys =
    {
        "Background0", "Background1", "Surface", "Surface2", "SurfaceHover", "SurfacePressed",
        "Border", "TextPrimary", "TextSecondary", "Accent", "AccentStrong", "Accent2Color",
        "Success", "Warning", "Error", "Disabled"
    };

    public void ApplyTheme(string? mode)
    {
        var selected = ParseMode(mode);
        if (!Palettes.TryGetValue(selected, out var palette))
        {
            return;
        }

        var resources = Application.Current.Resources;
        foreach (var key in ColorKeys)
        {
            if (!palette.TryGetValue(key, out var hex))
            {
                continue;
            }

            var color = (Color)ColorConverter.ConvertFromString(hex)!;
            resources[key] = color;

            var brushKey = key == "Accent2Color" ? "Accent2Brush" : $"{key}Brush";
            resources[brushKey] = new SolidColorBrush(color);
        }

        // Keep legacy aliases consistent for existing bindings.
        resources["AppBackground"] = resources["Background0Brush"];
        resources["SideNavBackground"] = resources["Background1Brush"];
        resources["TopBarBackground"] = resources["Background1Brush"];
        resources["CardBackground"] = resources["SurfaceBrush"];
        resources["CardBorder"] = resources["BorderBrush"];
        resources["MutedText"] = resources["TextSecondaryBrush"];
        resources["WarnBrush"] = resources["WarningBrush"];
        resources["DangerBrush"] = resources["ErrorBrush"];
        resources["Accent2"] = resources["Accent2Brush"];

        // Derived tinted brushes used by cards/meters/chips.
        resources["AccentMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected == ThemeMode.Light ? "#26248BFF" : "#333F9DFF"));
        resources["AccentMutedStrongBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected == ThemeMode.Light ? "#44248BFF" : "#4A3F9DFF"));
        resources["SuccessMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected == ThemeMode.Light ? "#2C179D67" : "#2C2FB871"));
        resources["WarningMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected == ThemeMode.Light ? "#33CA7D13" : "#33E8B045"));
        resources["ErrorMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected == ThemeMode.Light ? "#33D84F5A" : "#33E8686C"));
    }

    public static ThemeMode ParseMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return ThemeMode.Light;
        }

        return Enum.TryParse<ThemeMode>(mode, true, out var parsed)
            ? parsed
            : ThemeMode.Light;
    }
}
