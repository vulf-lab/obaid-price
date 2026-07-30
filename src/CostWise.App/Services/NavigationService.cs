using Microsoft.Extensions.DependencyInjection;

namespace CostWise.App.Services;

public interface INavigationService
{
    event Action<object?>? CurrentViewModelChanged;
    object? CurrentViewModel { get; }
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo<TViewModel>(Action<TViewModel>? configure) where TViewModel : class;
}

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public object? CurrentViewModel { get; private set; }

    public event Action<object?>? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : class =>
        NavigateTo<TViewModel>(configure: null);

    public void NavigateTo<TViewModel>(Action<TViewModel>? configure) where TViewModel : class
    {
        var vm = _services.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);
        CurrentViewModel = vm;
        CurrentViewModelChanged?.Invoke(CurrentViewModel);
    }
}
