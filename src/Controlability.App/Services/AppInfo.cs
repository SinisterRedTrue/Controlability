using System.Reflection;

namespace Controlability.App.Services;

/// <summary>Version shown in About and in the diagnostics. It comes from <c>Directory.Build.props</c>.</summary>
public static class AppInfo
{
    public static string Version
    {
        get
        {
            var assembly = typeof(AppInfo).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var version = informational ?? assembly.GetName().Version?.ToString() ?? "unknown";

            // The SDK appends "+<commit hash>" to the informational version.
            var plus = version.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? version : version[..plus];
        }
    }
}

/// <summary>One third-party component and its license, for the About section.</summary>
public sealed record ThirdPartyNotice(string Name, string License, string Url);

public static class ThirdPartyNotices
{
    public static IReadOnlyList<ThirdPartyNotice> All { get; } =
    [
        new("SDL3 (via ppy.SDL3-CS)", "zlib / MIT", "https://github.com/ppy/SDL3-CS"),
        new("Nefarius.ViGEm.Client", "MIT", "https://github.com/ViGEm/ViGEm.NET"),
        new("WPF-UI", "MIT", "https://github.com/lepoco/wpfui"),
        new("H.NotifyIcon", "MIT", "https://github.com/HavenDV/H.NotifyIcon"),
        new("CommunityToolkit.Mvvm", "MIT", "https://github.com/CommunityToolkit/dotnet"),
        new("Microsoft.Extensions (Hosting, DI, Logging)", "MIT", "https://github.com/dotnet/runtime"),
        new("ViGEmBus (installed separately)", "see project", "https://github.com/nefarius/ViGEmBus"),
        new("HidHide (installed separately)", "see project", "https://github.com/nefarius/HidHide"),
    ];
}
