namespace Controlability.App.Services;

/// <summary>What the tray needs from the application shell.</summary>
public interface IShell
{
    void ShowMainWindow();

    void Exit();
}

/// <summary>Shows a notification (a tray balloon).</summary>
public interface INotifier
{
    void Notify(string title, string message);
}
