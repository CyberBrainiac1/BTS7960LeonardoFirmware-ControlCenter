using System.Globalization;
using System.Windows.Data;

namespace ArduinoFFBControlCenter.Helpers;

public class PercentOrNaConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return "N/A";
        }

        if (value is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                return "N/A";
            }

            return $"{d:0.#}%";
        }

        if (double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            if (double.IsNaN(parsed) || double.IsInfinity(parsed))
            {
                return "N/A";
            }

            return $"{parsed:0.#}%";
        }

        return "N/A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
