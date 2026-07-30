using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CostWise.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>MultiBinding: [0]=item Id, [1]=renaming Id (int?). Match → Visible (or Invert).</summary>
public sealed class IdMatchToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2)
            return Visibility.Collapsed;

        var itemId = values[0] is int i ? i : (int?)null;
        var renamingId = values[1] is int r ? r : (int?)null;
        var match = itemId is int a && renamingId is int b && a == b;
        if (parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            match = !match;
        return match ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
