using System.Windows;
using System.Windows.Controls;
using CostWise.App.Services;

namespace CostWise.App.Views;

public partial class CostingView
{
    public CostingView() => InitializeComponent();

    private void ScenariosGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            DataGridColumnWidthStore.Restore(grid, "Costing.Scenarios");
    }

    private void ScenariosGrid_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            DataGridColumnWidthStore.Save(grid, "Costing.Scenarios");
    }
}
