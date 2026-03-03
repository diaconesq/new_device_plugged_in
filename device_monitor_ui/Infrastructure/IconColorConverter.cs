using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DeviceMonitorUi.Infrastructure;

public class IconColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Green" => Brushes.LimeGreen,
            "Red" => Brushes.Red,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
