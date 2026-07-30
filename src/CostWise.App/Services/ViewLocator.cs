using System.Windows;
using System.Windows.Controls;

namespace CostWise.App.Services;

public sealed class ViewLocator
{
    private readonly Dictionary<Type, Type> _map = new();

    public void Register<TViewModel, TView>()
        where TViewModel : class
        where TView : FrameworkElement
    {
        _map[typeof(TViewModel)] = typeof(TView);
    }

    public FrameworkElement? Resolve(object viewModel)
    {
        if (!_map.TryGetValue(viewModel.GetType(), out var viewType))
            return null;

        var view = (FrameworkElement)Activator.CreateInstance(viewType)!;
        view.DataContext = viewModel;
        return view;
    }
}

public sealed class ViewModelToViewConverter : System.Windows.Data.IValueConverter
{
    public static ViewLocator? Locator { get; set; }

    public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is null || Locator is null)
            return null;
        return Locator.Resolve(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
