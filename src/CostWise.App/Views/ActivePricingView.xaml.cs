using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CostWise.App.Services;
using CostWise.App.ViewModels;
using CostWise.Core.Entities;

namespace CostWise.App.Views;

public partial class ActivePricingView
{
    private const string LayoutKeyPrefix = "Pricing.ActiveBooks";

    private ActivePricingViewModel? _vm;
    private int? _layoutBookId;
    private Point _dragStart;
    private ActivePriceRow? _dragRow;
    private Point _bookDragStart;
    private object? _bookDragItem;
    private readonly ObservableCollection<ColumnToggleItem> _columnToggles = new();
    private bool _suppressToggleSync;
    private readonly List<(DataGridColumn Column, EventHandler Handler)> _displayIndexHandlers = new();

    public ActivePricingView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ColumnTogglesList.ItemsSource = _columnToggles;
        Unloaded += (_, _) => DetachVm();
    }

    private string CurrentLayoutKey =>
        _layoutBookId is int id
            ? $"{LayoutKeyPrefix}.{id}"
            : LayoutKeyPrefix;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachVm();
        _vm = e.NewValue as ActivePricingViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _layoutBookId = _vm.SelectedBook?.Id;
    }

    private void DetachVm()
    {
        if (_vm is null) return;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ActivePricingViewModel.SelectedBook))
            return;
        SwitchBookLayout();
    }

    private void SwitchBookLayout()
    {
        if (!ActivePricesGrid.IsLoaded) return;

        if (_layoutBookId is int previousId)
            DataGridColumnLayoutStore.Save(ActivePricesGrid, $"{LayoutKeyPrefix}.{previousId}");

        _layoutBookId = _vm?.SelectedBook?.Id;
        DataGridColumnLayoutStore.Restore(ActivePricesGrid, CurrentLayoutKey);
        RebuildColumnToggles(ActivePricesGrid);
    }

    private void ActivePricesGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        _layoutBookId = _vm?.SelectedBook?.Id;
        DataGridColumnLayoutStore.Restore(grid, CurrentLayoutKey);
        RebuildColumnToggles(grid);
        AttachDisplayIndexWatchers(grid);
    }

    private void ActivePricesGrid_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        DetachDisplayIndexWatchers();
        DataGridColumnLayoutStore.Save(grid, CurrentLayoutKey);
    }

    private void ColumnsButton_OnClick(object sender, RoutedEventArgs e)
    {
        RebuildColumnToggles(ActivePricesGrid);
        ColumnsPopup.IsOpen = !ColumnsPopup.IsOpen;
    }

    private void ColumnToggle_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleSync) return;
        if (sender is not CheckBox { DataContext: ColumnToggleItem item }) return;

        item.Column.Visibility = item.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        DataGridColumnLayoutStore.Save(ActivePricesGrid, CurrentLayoutKey);
    }

    private void RebuildColumnToggles(DataGrid grid)
    {
        _suppressToggleSync = true;
        _columnToggles.Clear();
        foreach (var column in grid.Columns.OrderBy(c => c.DisplayIndex))
        {
            var header = DataGridColumnLayoutStore.ColumnKey(column);
            if (header is null) continue;

            var locked = string.Equals(header, "Code", StringComparison.OrdinalIgnoreCase);
            _columnToggles.Add(new ColumnToggleItem(column, header, locked)
            {
                IsVisible = column.Visibility == Visibility.Visible
            });
        }
        _suppressToggleSync = false;
    }

    private void AttachDisplayIndexWatchers(DataGrid grid)
    {
        DetachDisplayIndexWatchers();
        foreach (var column in grid.Columns)
        {
            EventHandler handler = (_, _) =>
            {
                if (!grid.IsLoaded) return;
                DataGridColumnLayoutStore.Save(grid, CurrentLayoutKey);
                RebuildColumnToggles(grid);
            };
            var descriptor = DependencyPropertyDescriptor.FromProperty(
                DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn));
            descriptor?.AddValueChanged(column, handler);
            if (descriptor is not null)
                _displayIndexHandlers.Add((column, handler));
        }
    }

    private void DetachDisplayIndexWatchers()
    {
        var descriptor = DependencyPropertyDescriptor.FromProperty(
            DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn));
        foreach (var (column, handler) in _displayIndexHandlers)
            descriptor?.RemoveValueChanged(column, handler);
        _displayIndexHandlers.Clear();
    }

    private void BooksList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _bookDragStart = e.GetPosition(null);
        _bookDragItem = GetListBoxItemData(e.OriginalSource as DependencyObject);
    }

    private void BooksList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _bookDragItem is null)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _bookDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _bookDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        DragDrop.DoDragDrop(BooksList, _bookDragItem, DragDropEffects.Move);
        _bookDragItem = null;
    }

    private void BooksList_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(PriceBook))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void BooksList_OnDrop(object sender, DragEventArgs e)
    {
        if (_vm is null) return;
        if (e.Data.GetData(typeof(PriceBook)) is not PriceBook source)
            return;

        var target = GetListBoxItemData(e.OriginalSource as DependencyObject) as PriceBook;
        var sourceIndex = _vm.Books.IndexOf(source);
        if (sourceIndex < 0) return;

        var targetIndex = target is null
            ? _vm.Books.Count - 1
            : _vm.Books.IndexOf(target);
        if (targetIndex < 0 || sourceIndex == targetIndex) return;

        _vm.Books.Move(sourceIndex, targetIndex);
        var orderedIds = _vm.Books.Select(b => b.Id).ToList();
        if (_vm.ReorderBooksCommand.CanExecute(orderedIds))
            _vm.ReorderBooksCommand.Execute(orderedIds);

        e.Handled = true;
    }

    private static object? GetListBoxItemData(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListBoxItem item)
                return item.DataContext;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void ActivePricesGrid_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm?.FilterHost.HasActiveFilters == true)
        {
            _dragRow = null;
            return;
        }

        if (FindParent<TextBox>(e.OriginalSource as DependencyObject) is not null ||
            FindParent<DataGridColumnHeader>(e.OriginalSource as DependencyObject) is not null)
        {
            _dragRow = null;
            return;
        }

        _dragStart = e.GetPosition(null);
        _dragRow = GetRowData(e.OriginalSource as DependencyObject);
    }

    private void ActivePricesGrid_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _vm?.FilterHost.HasActiveFilters == true || _dragRow is null)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        DragDrop.DoDragDrop(ActivePricesGrid, _dragRow, DragDropEffects.Move);
        _dragRow = null;
    }

    private void ActivePricesGrid_OnDragOver(object sender, DragEventArgs e)
    {
        if (_vm?.FilterHost.HasActiveFilters == true)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = e.Data.GetDataPresent(typeof(ActivePriceRow))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ActivePricesGrid_OnDrop(object sender, DragEventArgs e)
    {
        if (_vm is null || _vm.FilterHost.HasActiveFilters) return;
        if (e.Data.GetData(typeof(ActivePriceRow)) is not ActivePriceRow source)
            return;

        var target = GetRowData(e.OriginalSource as DependencyObject);
        var sourceIndex = _vm.Rows.IndexOf(source);
        if (sourceIndex < 0) return;

        var targetIndex = target is null
            ? _vm.Rows.Count - 1
            : _vm.Rows.IndexOf(target);
        if (targetIndex < 0 || sourceIndex == targetIndex) return;

        _vm.Rows.Move(sourceIndex, targetIndex);
        var orderedIds = _vm.Rows.Select(r => r.FormulationId).ToList();
        if (_vm.ReorderBookFormulasCommand.CanExecute(orderedIds))
            _vm.ReorderBookFormulasCommand.Execute(orderedIds);

        e.Handled = true;
    }

    private void ActivePricesGrid_OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;
        if (e.Row.Item is not ActivePriceRow row)
            return;

        var header = e.Column is not null ? DataGridColumnLayoutStore.ColumnKey(e.Column) : null;
        if (header is not ("Manual MT" or "Manual bag"))
            return;

        var fromMt = header == "Manual MT";
        if (e.EditingElement is TextBox editor)
        {
            if (fromMt)
                row.OverrideSellMtText = editor.Text;
            else
                row.OverrideSellBagText = editor.Text;
        }

        Dispatcher.BeginInvoke(async () =>
        {
            if (_vm is not null)
                await _vm.PersistManualOverrideAsync(row, fromMt);
        });
    }

    private static T? FindParent<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
                return match;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static ActivePriceRow? GetRowData(DependencyObject? source) =>
        FindParent<DataGridRow>(source)?.Item as ActivePriceRow;
}

public sealed class ColumnToggleItem : INotifyPropertyChanged
{
    private bool _isVisible;

    public ColumnToggleItem(DataGridColumn column, string header, bool locked)
    {
        Column = column;
        Header = header;
        CanToggle = !locked;
        _isVisible = column.Visibility == Visibility.Visible;
    }

    public DataGridColumn Column { get; }
    public string Header { get; }
    public bool CanToggle { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
