using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CostWise.App.Services;
using CostWise.App.ViewModels;

namespace CostWise.App.Views;

public partial class FormulationsView
{
    public FormulationsView() => InitializeComponent();

    private void FormulationsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FormulationsViewModel vm && vm.SelectedItem is not null && vm.OpenCommand.CanExecute(vm.SelectedItem))
            vm.OpenCommand.Execute(vm.SelectedItem);
    }

    private void FormulationsListGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Formulations.List");

    private void FormulationsListGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Formulations.List");

    private void IngredientsGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Formulations.Ingredients");

    private void IngredientsGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Formulations.Ingredients");

    private void SpecsGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Formulations.Specs");

    private void SpecsGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Formulations.Specs");

    private void ChangeLogsGrid_OnLoaded(object sender, RoutedEventArgs e) =>
        Restore(sender, "Formulations.ChangeLogs");

    private void ChangeLogsGrid_OnUnloaded(object sender, RoutedEventArgs e) =>
        Persist(sender, "Formulations.ChangeLogs");

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
