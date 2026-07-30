using Microsoft.Extensions.DependencyInjection;

namespace CostWise.App.Services;

public interface INavigationService
{
    event Action<object?>? CurrentViewModelChanged;
    object? CurrentViewModel { get; }
    void NavigateTo<TViewModel>() where TViewModel : class;
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

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        CurrentViewModel = _services.GetRequiredService<TViewModel>();
        CurrentViewModelChanged?.Invoke(CurrentViewModel);
    }
}
