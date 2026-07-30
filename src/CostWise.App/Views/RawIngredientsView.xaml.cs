using System.Windows;
using System.Windows.Controls;
using CostWise.App.Services;

namespace CostWise.App.Views;

public partial class RawIngredientsView
{
    private const string GridKey = "RawIngredients";

    public RawIngredientsView() => InitializeComponent();

    private void IngredientsGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            DataGridColumnWidthStore.Restore(grid, GridKey);
    }

    private void IngredientsGrid_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            DataGridColumnWidthStore.Save(grid, GridKey);
    }
}
