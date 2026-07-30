using System.Windows;
using System.Windows.Controls;
using CostWise.App.Services;

namespace CostWise.App.Views;

public partial class ActivePricingView
{
    public ActivePricingView() => InitializeComponent();

    private void ActivePricesGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            DataGridColumnWidthStore.Restore(grid, "Pricing.ActiveBooks");
    }

    private void ActivePricesGrid_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            DataGridColumnWidthStore.Save(grid, "Pricing.ActiveBooks");
    }
}
