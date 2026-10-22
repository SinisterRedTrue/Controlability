using System.IO;
using System.Windows;
using Controlability.App.Services;
using Controlability.App.Tray;
using Controlability.App.ViewModels;
using Controlability.App.Views;
using Controlability.Core.Abstractions;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;
using Controlability.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Appearance;

namespace Controlability.App;

public partial class App : Application
{
    private IHost? _host;
    private ILogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        ConfigureServices(builder.Services);
        _host = builder.Build();
        _logger = _host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Controlability");

        var settings = _host.Services.GetRequiredService<Settings>();
        ApplyTheme(settings.Theme);

        // Created before the engine starts, so the first connect toasts are not missed.
        _host.Services.GetRequiredService<TrayIconHost>();
        _host.Services.GetRequiredService<ConnectionNotifier>();
        _host.Services.GetRequiredService<BatteryNotifier>();
        var window = _host.Services.GetRequiredService<MainWindow>();
        _host.Start();

        if (!settings.StartMinimized)
        {
            window.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            // Stops the engine first (pads removed, controllers unhidden), then disposes everything.
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Services.GetRequiredService<TrayIconHost>().Dispose();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddControlabilityEngine(options => options.Log = message => _logger?.LogInformation("{Message}", message));

        services.AddSingleton<IUiThread>(_ => new WpfUiThread(Dispatcher));
        services.AddSingleton<IUrlLauncher>(_ => new ShellUrlLauncher(message => _logger?.LogWarning("{Message}", message)));
        services.AddSingleton<ProfileStoreCatalog>(provider => new ProfileStoreCatalog(
            provider.GetRequiredService<ProfileStore>(), Path.Combine(AppContext.BaseDirectory, "profiles")));
        services.AddSingleton<IProfileCatalog>(provider => provider.GetRequiredService<ProfileStoreCatalog>());
        services.AddSingleton<IProfileEditor>(provider => provider.GetRequiredService<ProfileStoreCatalog>());
        services.AddSingleton<IRunningApps, RunningApps>();
        services.AddSingleton<IDialogs, WpfDialogs>();
        services.AddSingleton<ShellController>();
        services.AddSingleton<IShell>(provider => provider.GetRequiredService<ShellController>());
        services.AddSingleton<DiPageProvider>();

        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<TrayViewModel>();
        services.AddSingleton(provider => new SettingsViewModel(
            provider.GetRequiredService<IEngine>(),
            provider.GetRequiredService<Settings>(),
            provider.GetRequiredService<SettingsStore>().Save,
            provider.GetRequiredService<IProfileEditor>(),
            provider.GetRequiredService<IRunningApps>(),
            provider.GetRequiredService<IUiThread>(),
            provider.GetRequiredService<IClock>()));
        services.AddSingleton<DevicesViewModel>();
        services.AddSingleton<ProfilesViewModel>();
        services.AddSingleton<ConnectionNotifier>();
        services.AddSingleton<BatteryNotifier>();

        services.AddSingleton<TrayIconHost>();
        services.AddSingleton<INotifier>(provider => provider.GetRequiredService<TrayIconHost>());

        services.AddSingleton<MainWindow>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<DevicesPage>();
        services.AddTransient<ProfilesPage>();
        services.AddTransient<SettingsPage>();

        services.AddHostedService<EngineHostedService>();
    }

    /// <summary>Follows the system theme unless the user chose one; dark when the system theme cannot be read.</summary>
    private static void ApplyTheme(AppTheme theme)
    {
        var target = theme switch
        {
            AppTheme.Light => ApplicationTheme.Light,
            AppTheme.Dark => ApplicationTheme.Dark,
            _ => ApplicationThemeManager.GetSystemTheme() == SystemTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark,
        };
        ApplicationThemeManager.Apply(target);
    }
}
