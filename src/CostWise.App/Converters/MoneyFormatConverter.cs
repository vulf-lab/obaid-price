using System.Globalization;
using System.Windows.Data;
using CostWise.App.Services;

namespace CostWise.App.Converters;

/// <summary>Formats decimal money values using AppPreferences.CostDecimalPlaces.</summary>
public sealed class MoneyFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var places = AppPreferences.Current?.CostDecimalPlaces ?? 2;
        var format = $"N{places}";

        return value switch
        {
            decimal d => d.ToString(format, culture),
            double dbl => System.Convert.ToDecimal(dbl).ToString(format, culture),
            float f => System.Convert.ToDecimal(f).ToString(format, culture),
            int i => System.Convert.ToDecimal(i).ToString(format, culture),
            null => string.Empty,
            _ => value
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
