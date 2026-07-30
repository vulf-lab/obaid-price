using System.Globalization;
using CostWise.Core.Services;

namespace CostWise.App.Services;

public static class PriceListNumberFormat
{
    /// <summary>USD → "$"; other codes (e.g. KES) unchanged.</summary>
    public static string CurrencyLabel(string? currencyCode) =>
        string.Equals(currencyCode, "USD", StringComparison.OrdinalIgnoreCase)
            ? "$"
            : string.IsNullOrWhiteSpace(currencyCode) ? "KES" : currencyCode.Trim();

    /// <summary>USD → 1 decimal with thousands; otherwise (KES etc.) → 0 decimals with thousands.</summary>
    public static string FormatMoney(decimal? value, string? currencyCode)
    {
        if (value is null) return "—";
        return FormatMoney(value.Value, currencyCode);
    }

    public static string FormatMoney(decimal value, string? currencyCode)
    {
        var isUsd = string.Equals(currencyCode, "USD", StringComparison.OrdinalIgnoreCase);
        var pattern = isUsd ? "#,##0.0" : "#,##0";
        return value.ToString(pattern, CultureInfo.InvariantCulture);
    }

    public static string FormatNutrient(decimal? value)
    {
        if (value is null) return "—";
        return value.Value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static decimal? ApplyRound(decimal? value, decimal increment)
    {
        if (value is null) return null;
        return increment <= 0m ? value : CostingCalculator.RoundToIncrement(value.Value, increment);
    }

    public static decimal ApplyRound(decimal value, decimal increment) =>
        increment <= 0m ? value : CostingCalculator.RoundToIncrement(value, increment);
}
