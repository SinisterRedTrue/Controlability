using System.Windows.Controls;
using Controlability.App.Resources;
using Wpf.Ui.Controls;

namespace Controlability.App.Views;

/// <summary>Placeholder pages for the parts that come in later phases.</summary>
public abstract class ComingSoonPage : Page
{
    protected ComingSoonPage(SymbolRegular symbol, string title, string message)
    {
        Title = title;
        Content = new EmptyStateView { Symbol = symbol, Title = Strings.ComingSoonTitle, Message = message };
    }
}

public sealed class DevicesPage() : ComingSoonPage(SymbolRegular.Games24, Strings.NavDevices, Strings.ComingSoonDevices);

public sealed class ProfilesPage() : ComingSoonPage(SymbolRegular.Document24, Strings.NavProfiles, Strings.ComingSoonProfiles);
