using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.Core.Entities;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.App.ViewModels;

public partial class RawIngredientRow : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _pricePerMt;
    [ObservableProperty] private bool _isAvailable = true;
}

public partial class RawIngredientsViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;

    public ObservableCollection<RawIngredientRow> Items { get; } = new();

    [ObservableProperty] private RawIngredientRow? _selectedItem;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private decimal _newPrice;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public RawIngredientsViewModel(IDbContextFactory<CostWiseDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        Items.Clear();
        foreach (var item in await db.RawIngredients.OrderBy(x => x.Name).ToListAsync())
        {
            Items.Add(new RawIngredientRow
            {
                Id = item.Id,
                Name = item.Name,
                PricePerMt = item.PricePerMt,
                IsAvailable = item.IsAvailable
            });
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.RawIngredients.Add(new RawIngredient
        {
            Name = NewName.Trim(),
            PricePerMt = NewPrice,
            IsAvailable = true
        });
        await db.SaveChangesAsync();
        NewName = string.Empty;
        NewPrice = 0;
        StatusMessage = "Raw ingredient added.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Items.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.RawIngredients.ToDictionaryAsync(x => x.Id);
        var madeUnavailable = 0;

        foreach (var row in Items)
        {
            if (!byId.TryGetValue(row.Id, out var entity))
                continue;

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = "Every raw ingredient needs a name.";
                return;
            }

            var wasAvailable = entity.IsAvailable;
            entity.Name = row.Name.Trim();
            entity.PricePerMt = row.PricePerMt;
            entity.IsAvailable = row.IsAvailable;
            if (wasAvailable && !row.IsAvailable)
                madeUnavailable++;
        }

        try
        {
            await db.SaveChangesAsync();
            StatusMessage = madeUnavailable > 0
                ? $"Saved {Items.Count} raw ingredients. {madeUnavailable} marked unavailable — affected formulations are unfit."
                : $"Saved {Items.Count} raw ingredients.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique.";
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (SelectedItem is null) return;
        if (MessageBox.Show($"Remove '{SelectedItem.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.RawIngredients.FindAsync(SelectedItem.Id);
        if (entity is null) return;
        try
        {
            db.RawIngredients.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = "Cannot remove: ingredient is used in formulations.";
        }
    }
}

public partial class SpecParameterRow : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _unit = string.Empty;
}

public partial class SpecParametersViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;

    public ObservableCollection<SpecParameterRow> Items { get; } = new();

    [ObservableProperty] private SpecParameterRow? _selectedItem;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newUnit = "%";
    [ObservableProperty] private string _statusMessage = string.Empty;

    public SpecParametersViewModel(IDbContextFactory<CostWiseDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        Items.Clear();
        foreach (var item in await db.SpecParameters.OrderBy(x => x.Name).ToListAsync())
            Items.Add(new SpecParameterRow { Id = item.Id, Name = item.Name, Unit = item.Unit });
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.SpecParameters.Add(new SpecParameter { Name = NewName.Trim(), Unit = string.IsNullOrWhiteSpace(NewUnit) ? "%" : NewUnit.Trim() });
        await db.SaveChangesAsync();
        NewName = string.Empty;
        StatusMessage = "Specification added.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Items.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.SpecParameters.ToDictionaryAsync(x => x.Id);

        foreach (var row in Items)
        {
            if (!byId.TryGetValue(row.Id, out var entity))
                continue;

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = "Every specification needs a name.";
                return;
            }

            entity.Name = row.Name.Trim();
            entity.Unit = string.IsNullOrWhiteSpace(row.Unit) ? "%" : row.Unit.Trim();
        }

        try
        {
            await db.SaveChangesAsync();
            StatusMessage = $"Saved {Items.Count} specifications.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique.";
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (SelectedItem is null) return;
        if (MessageBox.Show($"Remove '{SelectedItem.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.SpecParameters.FindAsync(SelectedItem.Id);
        if (entity is null) return;
        try
        {
            db.SpecParameters.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = "Cannot remove: specification is used in formulations.";
        }
    }
}
