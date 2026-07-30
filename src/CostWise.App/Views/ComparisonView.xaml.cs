using System.Windows;
using System.Windows.Controls;
using CostWise.App.ViewModels;

namespace CostWise.App.Views;

public partial class ComparisonView : UserControl
{
    public ComparisonView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ComparisonViewModel oldVm)
            oldVm.PropertyChanged -= VmOnPropertyChanged;
        if (e.NewValue is ComparisonViewModel newVm)
        {
            newVm.PropertyChanged += VmOnPropertyChanged;
            RebuildGrids(newVm);
        }
    }

    private void VmOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ComparisonViewModel vm) return;
        if (e.PropertyName is nameof(ComparisonViewModel.ColumnHeaders)
            or nameof(ComparisonViewModel.Mode)
            or nameof(ComparisonViewModel.IsProfilesMode)
            or nameof(ComparisonViewModel.NutrientRows)
            or nameof(ComparisonViewModel.IngredientRows))
            RebuildGrids(vm);
    }

    private void RebuildGrids(ComparisonViewModel vm)
    {
        if (ProfileNutrientGrid is not null)
            vm.RebuildNutrientGrid(ProfileNutrientGrid, withFilters: true);
        if (FormulaNutrientGrid is not null)
            vm.RebuildNutrientGrid(FormulaNutrientGrid, withFilters: false);
        if (IngredientGrid is not null)
            vm.RebuildIngredientGrid(IngredientGrid);
    }

    private void ProfileNutrientGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ComparisonViewModel vm)
            vm.RebuildNutrientGrid(ProfileNutrientGrid, withFilters: true);
    }

    private void FormulaNutrientGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ComparisonViewModel vm)
            vm.RebuildNutrientGrid(FormulaNutrientGrid, withFilters: false);
    }

    private void IngredientGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ComparisonViewModel vm)
            vm.RebuildIngredientGrid(IngredientGrid);
    }
}
