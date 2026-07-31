using System.Windows;
using System.Windows.Threading;
using CostWise.App.Services;
using CostWise.App.Services.Update;
using CostWise.App.ViewModels;
using CostWise.App.Views;
using CostWise.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CostWise.App;

public partial class App : Application
{
    private IHost? _host;
    private AuthSession? _session;
    private bool _handlingSignOut;
    private bool _exitRequested;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent shutdown when the login dialog closes (it was the only window).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI exception", args.Exception);
            MessageBox.Show(
                AppLog.UserFacing(args.Exception),
                "Unexpected error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                AppLog.Error("Unhandled domain exception", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        try
        {
            var locator = new ViewLocator();
            locator.Register<FormulationsViewModel, FormulationsView>();
            locator.Register<NutritionProfilesViewModel, NutritionProfilesView>();
            locator.Register<ComparisonViewModel, ComparisonView>();
            locator.Register<ProductionMatrixViewModel, ProductionMatrixView>();
            locator.Register<RawIngredientsViewModel, RawIngredientsView>();
            locator.Register<PricingViewModel, PricingView>();
            locator.Register<ActivePricingViewModel, ActivePricingView>();
            locator.Register<CostingViewModel, CostingView>();
            locator.Register<PriceListsViewModel, PriceListsView>();
            locator.Register<SettingsViewModel, SettingsView>();
            locator.Register<ProfileViewModel, ProfileView>();
            ViewModelToViewConverter.Locator = locator;

            var dbPath = DependencyInjection.GetDefaultDatabasePath();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddCostWiseInfrastructure(dbPath);
                    services.AddSingleton(AppPreferences.CreateAndLoad());
                    services.AddSingleton(AuthAccountStore.CreateAndLoad());
                    services.AddSingleton<AuthSession>();
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<CompareSelectionService>();
                    services.AddSingleton<UpdateService>();
                    services.AddSingleton(locator);
                    services.AddTransient<ProductionExportService>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<FormulationsViewModel>();
                    services.AddTransient<NutritionProfilesViewModel>();
                    services.AddTransient<ComparisonViewModel>();
                    services.AddTransient<ProductionMatrixViewModel>();
                    services.AddTransient<RawIngredientsViewModel>();
                    services.AddTransient<PricingViewModel>();
                    services.AddTransient<ActivePricingViewModel>();
                    services.AddTransient<CostingViewModel>();
                    services.AddTransient<PriceListsViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<ProfileViewModel>();
                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<MainWindow>();
                })
                .Build();

            // QuestPDF license is set lazily on PDF export only — touching Settings at
            // startup crashes if native Skia assets fail to load.
            if (!await TryInitializeDatabaseAsync())
            {
                Shutdown(1);
                return;
            }

            _session = _host.Services.GetRequiredService<AuthSession>();
            _session.SignedOut += OnSignedOut;

            if (!AuthenticateInteractive())
            {
                Shutdown(0);
                return;
            }

            ShowMainWindow();
            ScheduleUpdateCheck();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Startup failed:\n{AppLog.UserFacing(ex)}",
                "OBAID Pricing",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            AppLog.Error("Startup failed", ex);
            Shutdown(1);
        }
    }

    private async Task<bool> TryInitializeDatabaseAsync()
    {
        try
        {
            await _host!.Services.InitializeDatabaseAsync();
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Database migration/startup failed", ex);
            var backup = DatabaseBackupService.FindLatestBackup();
            if (backup is not null)
            {
                var restore = MessageBox.Show(
                    "The database could not be upgraded after an application update.\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    "Restore the latest pre-update database backup and exit?\n" +
                    "(Then reinstall the previous OBAID Pricing release if needed.)\n\n" +
                    $"Backup:\n{backup}",
                    "Database upgrade failed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (restore == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (DatabaseBackupService.TryRestoreLatestBackup())
                        {
                            AppLog.Info($"Restored database from backup {backup}");
                            MessageBox.Show(
                                "Database restored from backup. The application will exit.\n" +
                                "Start OBAID Pricing again, or install the previous release if problems continue.",
                                "OBAID Pricing",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            return false;
                        }
                    }
                    catch (Exception restoreEx)
                    {
                        AppLog.Error("Database restore failed", restoreEx);
                        MessageBox.Show(
                            $"Restore failed:\n{AppLog.UserFacing(restoreEx)}",
                            "OBAID Pricing",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show(
                    $"Database startup failed:\n{AppLog.UserFacing(ex)}",
                    "OBAID Pricing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return false;
        }
    }

    private void ScheduleUpdateCheck()
    {
        var updates = _host!.Services.GetRequiredService<UpdateService>();
        var prefs = _host.Services.GetRequiredService<AppPreferences>();

        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                var owner = new WpfWindowOwner(MainWindow);
                if (prefs.UpdatePolicy == UpdatePolicy.SilentDownloadApplyOnRestart)
                    await updates.TryApplyPendingSilentAsync(owner);
                await updates.RunStartupCheckAsync(owner);
            }
            catch (Exception ex)
            {
                AppLog.Error("Background update check failed", ex);
            }
        }, DispatcherPriority.Background);
    }

    private bool AuthenticateInteractive()
    {
        // Always require a secret: password, PIN (when remembered + PIN set), or first-run setup.
        // Never auto-enter without credentials.
        var login = _host!.Services.GetRequiredService<LoginWindow>();
        return login.ShowDialog() == true && _session!.IsAuthenticated;
    }

    private void ShowMainWindow()
    {
        var window = _host!.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Closed += MainWindow_OnClosed;
        window.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        if (sender is Window w)
            w.Closed -= MainWindow_OnClosed;

        if (_handlingSignOut || _exitRequested)
            return;

        _exitRequested = true;
        Shutdown(0);
    }

    private void OnSignedOut()
    {
        if (_handlingSignOut) return;
        _handlingSignOut = true;

        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                foreach (Window window in Windows.Cast<Window>().ToList())
                {
                    if (window is MainWindow)
                    {
                        window.Closed -= MainWindow_OnClosed;
                        window.Close();
                    }
                }

                if (!AuthenticateInteractive())
                {
                    _exitRequested = true;
                    Shutdown(0);
                    return;
                }

                ShowMainWindow();
                ScheduleUpdateCheck();
            }
            finally
            {
                _handlingSignOut = false;
            }
        }, DispatcherPriority.Normal);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_session is not null)
            _session.SignedOut -= OnSignedOut;
        _host?.Dispose();
        base.OnExit(e);
    }
}
