using System.Globalization;
using System.Windows.Data;

namespace ArduinoFFBControlCenter.Helpers;

public class BoolToStatusTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "Warn";
    public string FalseText { get; set; } = "OK";
    public string NullText { get; set; } = "N/A";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolean)
        {
            if (parameter is string text && text.Contains('|'))
            {
                var split = text.Split('|');
                if (split.Length >= 2)
                {
                    return boolean ? split[0] : split[1];
                }
            }

            return boolean ? TrueText : FalseText;
        }

        return NullText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
