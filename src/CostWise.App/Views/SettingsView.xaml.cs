using System.Windows;
using System.Windows.Controls;
using CostWise.App.Services;

namespace CostWise.App.Views;

public partial class SettingsView
{
    public SettingsView() => InitializeComponent();

    private void FeedTypesGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Settings.FeedTypes");

    private void FeedTypesGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Settings.FeedTypes");

    private void SpeciesGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Settings.Species");

    private void SpeciesGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Settings.Species");

    private void CategoriesGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Settings.Categories");

    private void CategoriesGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Settings.Categories");

    private void SubCategoriesGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Settings.SubCategories");

    private void SubCategoriesGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Settings.SubCategories");

    private void SizesGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Settings.Sizes");

    private void SizesGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Settings.Sizes");

    private void PackingOptionsGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Settings.PackingOptions");

    private void PackingOptionsGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Settings.PackingOptions");

    private void DocumentOptionsGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Settings.DocumentOptions");

    private void DocumentOptionsGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Settings.DocumentOptions");

    private void AdditiveOptionsGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Settings.AdditiveOptions");

    private void AdditiveOptionsGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Settings.AdditiveOptions");

    private void CurrenciesGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Settings.Currencies");

    private void CurrenciesGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Settings.Currencies");

    private static void Restore(object sender, string key)
    {
        if (sender is DataGrid grid)
            DataGridColumnWidthStore.Restore(grid, key);
    }

    private static void Persist(object sender, string key)
    {
        if (sender is DataGrid grid)
            DataGridColumnWidthStore.Save(grid, key);
    }
}
