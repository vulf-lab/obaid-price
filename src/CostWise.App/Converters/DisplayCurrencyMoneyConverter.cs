using System.Globalization;
using System.Windows.Data;
using CostWise.App.Services;

namespace CostWise.App.Converters;

/// <summary>
/// Formats money for Active Pricing: USD uses $ and UsdDecimalPlaces; other currencies use CostDecimalPlaces with no symbol.
/// Values: [0] = amount (decimal), [1] = currency code (string).
/// </summary>
public sealed class DisplayCurrencyMoneyConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 1 || values[0] is null || values[0] == System.Windows.DependencyProperty.UnsetValue)
            return string.Empty;

        decimal amount;
        try
        {
            amount = System.Convert.ToDecimal(values[0], culture);
        }
        catch
        {
            return string.Empty;
        }

        var code = values.Length > 1 ? values[1] as string : null;
        var isUsd = string.Equals(code, "USD", StringComparison.OrdinalIgnoreCase);
        var prefs = AppPreferences.Current;
        var places = isUsd
            ? prefs?.UsdDecimalPlaces ?? 2
            : prefs?.CostDecimalPlaces ?? 2;
        var formatted = amount.ToString($"N{places}", culture);
        return isUsd ? $"${formatted}" : formatted;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
