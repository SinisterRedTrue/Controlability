using System.Globalization;
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
using Controlability.Platform;
using Controlability.Platform.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Controlability.App;

public partial class App : Application
{
    private const string InstanceName = "Controlability-6D1E0C52";
    private const string MinimizedArgument = RegistryAutostart.MinimizedArgument;

    private readonly AppPaths _paths = AppPaths.ForCurrentUser();
    private RollingFileLog? _log;
    private SingleInstance? _instance;
    private IHost? _host;
    private ILogger? _logger;
    private CrashGuard? _crashGuard;
    private PowerResumeHandler? _power;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instance = SingleInstance.Create(InstanceName);
        if (!_instance.IsFirst)
        {
            // A copy is already running: ask it to show its window and leave.
            _instance.SignalFirst();
            Shutdown();
            return;
        }

        // Decided before anything can write settings.json.
        var firstRun = !File.Exists(_paths.SettingsFile);
        var startMinimized = e.Args.Contains(MinimizedArgument, StringComparer.OrdinalIgnoreCase);

        _log = new RollingFileLog(_paths.LogsFolder);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddDebug();
        builder.Logging.AddProvider(new FileLoggerProvider(_log));
        ConfigureServices(builder.Services, _log);
        _host = builder.Build();
        _logger = _host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Controlability");

        _crashGuard = new CrashGuard(_logger, ReleaseEverything, ShowCrashDialog);
        _crashGuard.Install(this);

        var settings = _host.Services.GetRequiredService<Settings>();
        _log.Level = settings.LogLevel;
        _logger.LogInformation("Controlability {Version} starting{Minimized}.", AppInfo.Version, startMinimized ? " minimized" : "");
        ApplyLanguage(settings.Language);
        WpfAppearance.ApplyTheme(settings.Theme);

        // Created before the engine starts, so the first connect toasts are not missed.
        _host.Services.GetRequiredService<TrayIconHost>();
        _host.Services.GetRequiredService<ConnectionNotifier>();
        _host.Services.GetRequiredService<BatteryNotifier>();
        var window = _host.Services.GetRequiredService<MainWindow>();
        _host.Start();

        var engine = _host.Services.GetRequiredService<IEngine>();
        _power = new PowerResumeHandler(_logger, engine.RestartInput);
        var shell = _host.Services.GetRequiredService<ShellController>();
        _instance.Listen(() => Dispatcher.BeginInvoke(shell.ShowMainWindow));

        if (firstRun)
        {
            _host.Services.GetRequiredService<WizardWindow>().ShowDialog();
        }

        if (!settings.StartMinimized && !startMinimized)
        {
            window.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _power?.Dispose();
        if (_host is not null)
        {
            // Stops the engine first (pads removed, controllers unhidden), then disposes everything.
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Services.GetRequiredService<TrayIconHost>().Dispose();
            _host.Dispose();
        }

        _instance?.Dispose();
        base.OnExit(e);
    }

    private void ConfigureServices(IServiceCollection services, RollingFileLog log)
    {
        services.AddSingleton(_paths);
        services.AddSingleton(log);
        services.AddControlabilityEngine(options => options.Log = message => _logger?.LogInformation("{Message}", message));

        services.AddSingleton<IUiThread>(_ => new WpfUiThread(Dispatcher));
        services.AddSingleton<IUrlLauncher>(_ => new ShellUrlLauncher(message => _logger?.LogWarning("{Message}", message)));
        services.AddSingleton<IAutostart, RegistryAutostart>();
        services.AddSingleton<IClipboard, WpfClipboard>();
        services.AddSingleton<IAppearance, WpfAppearance>();
        services.AddSingleton<DiagnosticsService>();
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
        services.AddSingleton(provider => new GeneralSettingsViewModel(
            provider.GetRequiredService<IEngine>(),
            provider.GetRequiredService<Settings>(),
            provider.GetRequiredService<SettingsStore>().Save,
            provider.GetRequiredService<IAutostart>(),
            provider.GetRequiredService<IAppearance>(),
            provider.GetRequiredService<RollingFileLog>(),
            provider.GetRequiredService<DiagnosticsService>(),
            provider.GetRequiredService<IDialogs>(),
            provider.GetRequiredService<IUrlLauncher>()));
        services.AddSingleton(provider => new SteamTipViewModel(
            provider.GetRequiredService<IRunningApps>(),
            provider.GetRequiredService<Settings>(),
            provider.GetRequiredService<SettingsStore>().Save,
            provider.GetRequiredService<IUiThread>()));
        services.AddTransient(provider => new WizardViewModel(
            provider.GetRequiredService<IEngine>(),
            provider.GetRequiredService<IDriverProbe>(),
            provider.GetRequiredService<Settings>(),
            provider.GetRequiredService<SettingsStore>().Save,
            provider.GetRequiredService<IAutostart>(),
            provider.GetRequiredService<IUrlLauncher>(),
            provider.GetRequiredService<IUiThread>(),
            provider.GetRequiredService<IClock>()));
        services.AddSingleton<DevicesViewModel>();
        services.AddSingleton<ProfilesViewModel>();
        services.AddSingleton<ConnectionNotifier>();
        services.AddSingleton<BatteryNotifier>();

        services.AddSingleton<TrayIconHost>();
        services.AddSingleton<INotifier>(provider => provider.GetRequiredService<TrayIconHost>());

        services.AddSingleton(provider => new MainWindow(
            provider.GetRequiredService<Settings>(),
            provider.GetRequiredService<SettingsStore>().Save,
            provider.GetRequiredService<ShellController>(),
            provider.GetRequiredService<DiPageProvider>()));
        services.AddTransient<WizardWindow>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<DevicesPage>();
        services.AddTransient<ProfilesPage>();
        services.AddTransient<SettingsPage>();

        services.AddHostedService<EngineHostedService>();
    }

    /// <summary>"system" follows Windows; a tag that .NET does not know is ignored.</summary>
    private static void ApplyLanguage(string language)
    {
        if (language == "system")
        {
            return;
        }

        try
        {
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = new CultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
        }
    }

    /// <summary>The crash path: give the controllers back and remove the virtual pads, without throwing.</summary>
    private void ReleaseEverything()
    {
        if (_host?.Services.GetService<IEngine>() is { } engine)
        {
            engine.Stop();
            engine.UnhideAll();
        }
    }

    private void ShowCrashDialog(Exception exception)
    {
        var diagnostics = _host?.Services.GetService<DiagnosticsService>();
        bool Copy() => diagnostics?.CopyToClipboard() ?? new WpfClipboard().SetText(exception.ToString());
        void Show() => new CrashWindow(exception, Copy).ShowDialog();

        if (Dispatcher.CheckAccess())
        {
            Show();
        }
        else
        {
            Dispatcher.Invoke(Show);
        }
    }
}
