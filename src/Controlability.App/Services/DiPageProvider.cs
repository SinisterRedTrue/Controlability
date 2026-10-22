using Wpf.Ui.Abstractions;

namespace Controlability.App.Services;

/// <summary>Resolves navigation pages from the DI container, so pages can take view models in their constructor.</summary>
public sealed class DiPageProvider(IServiceProvider services) : INavigationViewPageProvider
{
    public object? GetPage(Type pageType) => services.GetService(pageType);
}
