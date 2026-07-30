using System.Globalization;
using System.Windows.Data;
using CostWise.App.Services;

namespace CostWise.App.Converters;

/// <summary>Formats decimal with AppPreferences Kes or Usd decimal places. ConverterParameter: "kes" or "usd".</summary>
public sealed class CurrencyDecimalFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not decimal d) return value ?? string.Empty;
        var kind = parameter?.ToString()?.ToLowerInvariant();
        var prefs = AppPreferences.Current;
        return kind switch
        {
            "usd" => prefs?.FormatUsd(d) ?? d.ToString("N2", culture),
            _ => prefs?.FormatKes(d) ?? d.ToString("N2", culture)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
            return 0m;
        return decimal.TryParse(value.ToString(), NumberStyles.Number, culture, out var d) ? d : 0m;
    }
}
