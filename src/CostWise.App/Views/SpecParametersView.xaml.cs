using System.Windows;
using System.Windows.Controls;
using CostWise.App.Services;

namespace CostWise.App.Views;

public partial class SpecParametersView
{
    private const string GridKey = "SpecParameters";

    public SpecParametersView() => InitializeComponent();

    private void SpecsGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            DataGridColumnWidthStore.Restore(grid, GridKey);
    }

    private void SpecsGrid_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            DataGridColumnWidthStore.Save(grid, GridKey);
    }
}
