using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenAHU.Desktop.Shell;

public sealed class InverseBooleanVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool present && present ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility visibility && visibility != Visibility.Visible;
}
