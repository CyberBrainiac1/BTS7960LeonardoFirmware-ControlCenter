using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ArduinoFFBControlCenter.Helpers;

public class BoolToBrushConverter : IValueConverter
{
    public Brush TrueBrush { get; set; } = Brushes.LimeGreen;
    public Brush FalseBrush { get; set; } = Brushes.DimGray;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? TrueBrush : FalseBrush;
        }
        return FalseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
