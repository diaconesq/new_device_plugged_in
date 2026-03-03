using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DeviceMonitorUi.Models;

namespace DeviceMonitorUi.Infrastructure;

public class KindToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ChangeKind kind && kind == ChangeKind.Added
            ? Brushes.LimeGreen
            : Brushes.Red;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
