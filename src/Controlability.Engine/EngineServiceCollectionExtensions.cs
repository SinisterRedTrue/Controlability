using Controlability.Core.Abstractions;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;
using Controlability.Input.Keyboard;
using Controlability.Input.Sdl;
using Controlability.Output.ViGEm;
using Controlability.Platform;
using Controlability.Platform.HidHide;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Controlability.Engine;

public sealed class EngineOptions
{
    /// <summary>Where the shipped profiles are copied from on the first run. Default: <c>profiles</c> next to the exe.</summary>
    public string? DefaultProfilesFolder { get; set; }

    /// <summary>Changes the loaded settings in memory (for example the CLI forcing the Manual policy).</summary>
    public Action<Settings>? ConfigureSettings { get; set; }

    /// <summary>False keeps the remembered slots in memory only.</summary>
    public bool PersistSettings { get; set; } = true;

    /// <summary>Receives engine and SDL diagnostics.</summary>
    public Action<string>? Log { get; set; }
}

public static class EngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers the whole runtime: clock, paths, settings, profiles, SDL input, ViGEm output and <see cref="IEngine"/>.
    /// Every service uses <c>TryAdd</c>, so a caller can register its own (fakes, extra sources) first.
    /// </summary>
    public static IServiceCollection AddControlabilityEngine(this IServiceCollection services, Action<EngineOptions>? configure = null)
    {
        var options = new EngineOptions();
        configure?.Invoke(options);

        services.TryAddSingleton<IClock, StopwatchClock>();
        services.TryAddSingleton(AppPaths.ForCurrentUser());
        services.TryAddSingleton(provider =>
        {
            var paths = provider.GetRequiredService<AppPaths>();
            paths.EnsureCreated(options.DefaultProfilesFolder ?? Path.Combine(AppContext.BaseDirectory, "profiles"));
            return new SettingsStore(paths.SettingsFile, options.Log);
        });
        services.TryAddSingleton(provider => provider.GetRequiredService<SettingsStore>().Load());
        services.TryAddSingleton(provider =>
        {
            provider.GetRequiredService<AppPaths>().EnsureCreated(options.DefaultProfilesFolder ?? Path.Combine(AppContext.BaseDirectory, "profiles"));
            return new ProfileStore(provider.GetRequiredService<AppPaths>().ProfilesFolder, log: options.Log);
        });
        services.TryAddSingleton<IOutputSink>(_ => new ViGEmSink());
        services.TryAddSingleton<IDeviceHider>(provider =>
        {
            var hider = HidHideService.CreateDefault(provider.GetRequiredService<AppPaths>().HiddenJournalFile, options.Log);
            hider.RecoverFromJournal();
            return hider;
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IInputSource, SdlControllerSource>(provider =>
        {
            var source = new SdlControllerSource(provider.GetRequiredService<IClock>());
            source.Diagnostic += message => options.Log?.Invoke($"[sdl] {message}");
            return source;
        }));
        services.TryAddSingleton<IKeyboardSource>(provider => new RawInputKeyboardSource(provider.GetRequiredService<IClock>()));
        services.TryAddSingleton<IEngine>(provider =>
        {
            var settings = provider.GetRequiredService<Settings>();
            options.ConfigureSettings?.Invoke(settings);
            var store = provider.GetRequiredService<SettingsStore>();
            return new Engine(
                [.. provider.GetServices<IInputSource>()],
                provider.GetRequiredService<IOutputSink>(),
                provider.GetRequiredService<ProfileStore>(),
                provider.GetRequiredService<IClock>(),
                settings,
                provider.GetRequiredService<IDeviceHider>(),
                options.PersistSettings ? store.Save : null,
                options.Log,
                provider.GetRequiredService<IKeyboardSource>());
        });
        return services;
    }
}
