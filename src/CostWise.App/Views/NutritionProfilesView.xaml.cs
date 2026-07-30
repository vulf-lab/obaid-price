using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CostWise.App.Services;
using CostWise.App.ViewModels;

namespace CostWise.App.Views;

public partial class NutritionProfilesView : UserControl
{
    private const string LayoutKey = "NutritionProfiles.Matrix";

    private NutritionProfilesViewModel? _vm;
    private readonly ObservableCollection<ColumnToggleItem> _columnToggles = new();
    private bool _suppressToggleSync;

    public NutritionProfilesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ColumnTogglesList.ItemsSource = _columnToggles;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = e.NewValue as NutritionProfilesViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            RebuildMatrixColumns();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NutritionProfilesViewModel.SpecParameters))
            RebuildMatrixColumns();
    }

    private void MatrixGrid_OnLoaded(object sender, RoutedEventArgs e) => RebuildMatrixColumns();

    private void MatrixGrid_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (MatrixGrid.Columns.Count > 0)
            DataGridColumnLayoutStore.Save(MatrixGrid, LayoutKey);
    }

    private void RebuildMatrixColumns()
    {
        if (_vm is null || MatrixGrid is null) return;
        _vm.BuildMatrixColumns(MatrixGrid);
        DataGridColumnLayoutStore.Restore(MatrixGrid, LayoutKey);
        // Keep locked left-to-right sequence (ignore any previously saved DisplayIndex).
        for (var i = 0; i < MatrixGrid.Columns.Count; i++)
            MatrixGrid.Columns[i].DisplayIndex = i;
        RebuildColumnToggles(MatrixGrid);
    }

    private void ColumnsButton_OnClick(object sender, RoutedEventArgs e)
    {
        RebuildColumnToggles(MatrixGrid);
        ColumnsPopup.IsOpen = !ColumnsPopup.IsOpen;
    }

    private void ColumnToggle_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleSync) return;
        if (sender is not CheckBox { DataContext: ColumnToggleItem item }) return;

        item.Column.Visibility = item.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        DataGridColumnLayoutStore.Save(MatrixGrid, LayoutKey);
    }

    private void RebuildColumnToggles(DataGrid grid)
    {
        _suppressToggleSync = true;
        _columnToggles.Clear();
        foreach (var column in grid.Columns.OrderBy(c => c.DisplayIndex))
        {
            var key = DataGridColumnLayoutStore.ColumnKey(column);
            if (key is null) continue;

            var display = DataGridColumnLayoutStore.ColumnTitle(column) ?? key;
            var locked = string.Equals(key, "Code", StringComparison.OrdinalIgnoreCase);
            _columnToggles.Add(new ColumnToggleItem(column, display, locked)
            {
                IsVisible = column.Visibility == Visibility.Visible
            });
        }
        _suppressToggleSync = false;
    }

    private async void MatrixGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;
        if (MatrixGrid.SelectedItem is not NutritionMatrixRow row) return;

        var col = MatrixGrid.CurrentColumn;
        var key = col is null ? null : DataGridColumnLayoutStore.ColumnKey(col);
        // Profile dialog: double-click Name only (Code is a link to Formulations).
        if (key is not null &&
            !string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase))
            return;

        await _vm.OpenProfileAsync(row);
    }
}
