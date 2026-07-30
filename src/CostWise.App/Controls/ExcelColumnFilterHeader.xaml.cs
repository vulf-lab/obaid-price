using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CostWise.App.Controls;

public partial class ExcelColumnFilterHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ExcelColumnFilterHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ColumnKeyProperty =
        DependencyProperty.Register(nameof(ColumnKey), typeof(string), typeof(ExcelColumnFilterHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HostProperty =
        DependencyProperty.Register(nameof(Host), typeof(IColumnFilterHost), typeof(ExcelColumnFilterHeader),
            new PropertyMetadata(null, OnHostChanged));

    public static readonly DependencyProperty AllowValueFilterProperty =
        DependencyProperty.Register(nameof(AllowValueFilter), typeof(bool), typeof(ExcelColumnFilterHeader),
            new PropertyMetadata(true, OnAllowValueFilterChanged));

    private ObservableCollection<FilterValueOption> _options = new();

    public ExcelColumnFilterHeader()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateValueFilterVisibility();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string ColumnKey
    {
        get => (string)GetValue(ColumnKeyProperty);
        set => SetValue(ColumnKeyProperty, value);
    }

    public IColumnFilterHost? Host
    {
        get => (IColumnFilterHost?)GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public bool AllowValueFilter
    {
        get => (bool)GetValue(AllowValueFilterProperty);
        set => SetValue(AllowValueFilterProperty, value);
    }

    public bool IsFilterActive => Host?.IsFilterActive(ColumnKey) == true;

    private static void OnAllowValueFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ExcelColumnFilterHeader header)
            header.UpdateValueFilterVisibility();
    }

    private void UpdateValueFilterVisibility()
    {
        var vis = AllowValueFilter ? Visibility.Visible : Visibility.Collapsed;
        if (ValueFilterActions is not null) ValueFilterActions.Visibility = vis;
        if (ValueFilterScroll is not null) ValueFilterScroll.Visibility = vis;
    }

    private static void OnHostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ExcelColumnFilterHeader header) return;
        if (e.OldValue is IColumnFilterHost oldHost)
            oldHost.FiltersChanged -= header.OnFiltersChanged;
        if (e.NewValue is IColumnFilterHost newHost)
            newHost.FiltersChanged += header.OnFiltersChanged;
        header.RefreshActiveState();
    }

    private void OnFiltersChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(RefreshActiveState);

    private void RefreshActiveState()
    {
        if (FilterButton is null) return;
        try
        {
            FilterButton.Foreground = IsFilterActive
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("TextMutedBrush");
            FilterButton.FontWeight = IsFilterActive ? FontWeights.Bold : FontWeights.Normal;
        }
        catch
        {
            // Resource lookup can fail before the control is in a namescope.
        }
    }

    private void FilterButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Host is null) return;
        _options = new ObservableCollection<FilterValueOption>(Host.GetOptions(ColumnKey));
        OptionsList.ItemsSource = _options;
        FilterPopup.IsOpen = true;
    }

    private void SortAsc_OnClick(object sender, RoutedEventArgs e)
    {
        Host?.Sort(ColumnKey, ascending: true);
        FilterPopup.IsOpen = false;
    }

    private void SortDesc_OnClick(object sender, RoutedEventArgs e)
    {
        Host?.Sort(ColumnKey, ascending: false);
        FilterPopup.IsOpen = false;
    }

    private void SelectAll_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var o in _options) o.IsSelected = true;
    }

    private void Clear_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var o in _options) o.IsSelected = false;
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e)
    {
        if (Host is null) return;
        var selected = _options.Where(o => o.IsSelected).Select(o => o.Value).ToList();
        Host.ApplyValueFilter(ColumnKey, selected);
        FilterPopup.IsOpen = false;
    }
}
