using System.Windows;
using System.Windows.Threading;
using CostWise.App.Services;
using CostWise.App.ViewModels;
using CostWise.App.Views;
using CostWise.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CostWise.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(FormatException(args.Exception), "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            var locator = new ViewLocator();
            locator.Register<FormulationsViewModel, FormulationsView>();
            locator.Register<ProductionMatrixViewModel, ProductionMatrixView>();
            locator.Register<RawIngredientsViewModel, RawIngredientsView>();
            locator.Register<SpecParametersViewModel, SpecParametersView>();
            locator.Register<PricingViewModel, PricingView>();
            locator.Register<ActivePricingViewModel, ActivePricingView>();
            locator.Register<CostingViewModel, CostingView>();
            locator.Register<SettingsViewModel, SettingsView>();
            ViewModelToViewConverter.Locator = locator;

            var dbPath = DependencyInjection.GetDefaultDatabasePath();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddCostWiseInfrastructure(dbPath);
                    services.AddSingleton(AppPreferences.CreateAndLoad());
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton(locator);
                    services.AddTransient<ProductionExportService>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<FormulationsViewModel>();
                    services.AddTransient<ProductionMatrixViewModel>();
                    services.AddTransient<RawIngredientsViewModel>();
                    services.AddTransient<SpecParametersViewModel>();
                    services.AddTransient<PricingViewModel>();
                    services.AddTransient<ActivePricingViewModel>();
                    services.AddTransient<CostingViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            // QuestPDF license is set lazily on PDF export only — touching Settings at
            // startup crashes if native Skia assets fail to load.
            await _host.Services.InitializeDatabaseAsync();

            var window = _host.Services.GetRequiredService<MainWindow>();
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup failed:\n{FormatException(ex)}", "CostWise", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    private static string FormatException(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
            parts.Add(current.Message);
        return string.Join("\n→ ", parts);
    }
}
