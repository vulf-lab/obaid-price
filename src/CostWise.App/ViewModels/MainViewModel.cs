using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Services;

namespace CostWise.App.ViewModels;

public partial class NavItem : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = string.Empty;
    public Action Navigate { get; init; } = () => { };

    [ObservableProperty] private bool _isSelected;
}

public partial class MainViewModel : ObservableObject
{
    private readonly Services.INavigationService _navigation;
    private readonly AppPreferences _preferences;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentPageTitle = "Formulations";

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public MainViewModel(Services.INavigationService navigation, AppPreferences preferences)
    {
        _navigation = navigation;
        _preferences = preferences;
        _navigation.CurrentViewModelChanged += vm =>
        {
            CurrentView = Services.ViewModelToViewConverter.Locator?.Resolve(vm!) ?? vm;
            SyncNavSelection(vm);
        };
        BuildNavItems();
    }

    private void SyncNavSelection(object? vm)
    {
        var key = vm switch
        {
            FormulationsViewModel => "formulations",
            NutritionProfilesViewModel => "nutrition",
            ComparisonViewModel => "compare",
            ProductionMatrixViewModel => "production",
            RawIngredientsViewModel => "raw",
            PricingViewModel => "pricing",
            PriceListsViewModel => "pricelists",
            SettingsViewModel => "settings",
            ProfileViewModel => "profile",
            _ => null
        };
        if (key is null) return;

        foreach (var n in NavItems)
            n.IsSelected = string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase);

        var selected = NavItems.FirstOrDefault(n => n.IsSelected);
        if (selected is not null)
            CurrentPageTitle = selected.Title;
    }

    private void BuildNavItems()
    {
        var catalog = new Dictionary<string, NavItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["formulations"] = new NavItem
            {
                Key = "formulations",
                Title = "Formulations",
                IconGlyph = "\uE8F1", // Dictionary
                Navigate = NavigateFormulations
            },
            ["nutrition"] = new NavItem
            {
                Key = "nutrition",
                Title = "Nutrition Profiles",
                IconGlyph = "\uE9D9", // Chart / health
                Navigate = NavigateNutrition
            },
            ["compare"] = new NavItem
            {
                Key = "compare",
                Title = "Comparison",
                IconGlyph = "\uE9D2", // Compare
                Navigate = NavigateComparison
            },
            ["production"] = new NavItem
            {
                Key = "production",
                Title = "Production",
                IconGlyph = "\uE774", // Factory / Building
                Navigate = NavigateProduction
            },
            ["raw"] = new NavItem
            {
                Key = "raw",
                Title = "Raw Ingredients",
                IconGlyph = "\uE8B7", // Leaf / Grocery
                Navigate = NavigateRawIngredients
            },
            ["pricing"] = new NavItem
            {
                Key = "pricing",
                Title = "Pricing",
                IconGlyph = "\uE8A1", // Tag / Price
                Navigate = NavigatePricing
            },
            ["pricelists"] = new NavItem
            {
                Key = "pricelists",
                Title = "Price Lists",
                IconGlyph = "\uE8A5", // Page / Document
                Navigate = NavigatePriceLists
            },
            ["profile"] = new NavItem
            {
                Key = "profile",
                Title = "Profile",
                IconGlyph = "\uE77B", // Contact
                Navigate = NavigateProfile
            },
            ["settings"] = new NavItem
            {
                Key = "settings",
                Title = "Settings",
                IconGlyph = "\uE713", // Settings
                Navigate = NavigateSettings
            }
        };

        var order = _preferences.NavOrder;
        NavItems.Clear();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in order)
        {
            if (!catalog.TryGetValue(key, out var item)) continue;
            NavItems.Add(item);
            used.Add(key);
        }

        foreach (var key in new[] { "formulations", "nutrition", "compare", "production", "raw", "pricing", "pricelists", "profile", "settings" })
        {
            if (used.Contains(key)) continue;
            NavItems.Add(catalog[key]);
        }
    }

    [RelayCommand]
    private void NavigateToItem(NavItem? item)
    {
        if (item is null) return;
        foreach (var n in NavItems)
            n.IsSelected = ReferenceEquals(n, item);
        item.Navigate();
    }

    private void NavigateFormulations()
    {
        CurrentPageTitle = "Formulations";
        _navigation.NavigateTo<FormulationsViewModel>();
    }

    private void NavigateNutrition()
    {
        CurrentPageTitle = "Nutrition Profiles";
        _navigation.NavigateTo<NutritionProfilesViewModel>();
    }

    private void NavigateComparison()
    {
        CurrentPageTitle = "Comparison";
        _navigation.NavigateTo<ComparisonViewModel>();
    }

    private void NavigateProduction()
    {
        CurrentPageTitle = "Active formulations (production)";
        _navigation.NavigateTo<ProductionMatrixViewModel>();
    }

    private void NavigateRawIngredients()
    {
        CurrentPageTitle = "Raw Ingredients";
        _navigation.NavigateTo<RawIngredientsViewModel>();
    }

    private void NavigatePricing()
    {
        CurrentPageTitle = "Pricing";
        _navigation.NavigateTo<PricingViewModel>();
    }

    private void NavigatePriceLists()
    {
        CurrentPageTitle = "Price Lists";
        _navigation.NavigateTo<PriceListsViewModel>();
    }

    private void NavigateProfile()
    {
        CurrentPageTitle = "Profile";
        _navigation.NavigateTo<ProfileViewModel>();
    }

    private void NavigateSettings()
    {
        CurrentPageTitle = "Settings";
        _navigation.NavigateTo<SettingsViewModel>();
    }

    public void PersistNavOrder()
    {
        _preferences.NavOrder = NavItems.Select(n => n.Key).ToArray();
        _preferences.Save();
    }

    public void Initialize()
    {
        var first = NavItems.FirstOrDefault();
        if (first is not null)
            NavigateToItem(first);
        else
            NavigateFormulations();
    }
}
