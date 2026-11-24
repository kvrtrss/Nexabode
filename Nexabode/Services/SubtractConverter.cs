using System.Globalization;
using System.Windows.Data;

namespace Nexabode.Services;

// Takes a width and subtracts a fixed amount (the ConverterParameter). Used so the text that opens under a selected
// table row is exactly as wide as the table, wraps there, and never makes the table scroll sideways.
public class SubtractConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var width = value is double d ? d : 0;
        var less = parameter is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
        return Math.Max(0, width - less);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
