using Controlability.Core.Settings;
using Wpf.Ui.Appearance;

namespace Controlability.App.Services;

/// <summary>Switches the theme while the app runs.</summary>
public interface IAppearance
{
    void Apply(AppTheme theme);
}

public sealed class WpfAppearance(IServiceProvider services) : IAppearance
{
    /// <summary>Follows the system theme unless the user chose one; dark when the system theme cannot be read.</summary>
    public static void ApplyTheme(AppTheme theme)
    {
        var target = theme switch
        {
            AppTheme.Light => ApplicationTheme.Light,
            AppTheme.Dark => ApplicationTheme.Dark,
            _ => ApplicationThemeManager.GetSystemTheme() == SystemTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark,
        };
        DesignTheme.Initialize();
        ApplicationThemeManager.Apply(target);
    }

    public void Apply(AppTheme theme)
    {
        ApplyTheme(theme);

        // The main window only follows the system while the choice is "System".
        if (services.GetService(typeof(MainWindow)) is MainWindow window)
        {
            if (theme == AppTheme.System)
            {
                SystemThemeWatcher.Watch(window);
            }
            else
            {
                SystemThemeWatcher.UnWatch(window);
            }
        }
    }
}
