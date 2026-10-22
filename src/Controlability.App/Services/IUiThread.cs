namespace Controlability.App.Services;

/// <summary>The UI thread as ViewModels see it, so they need no WPF types and can be tested.</summary>
public interface IUiThread
{
    /// <summary>Runs <paramref name="action"/> on the UI thread. Safe to call from any thread.</summary>
    void Post(Action action);

    /// <summary>Runs <paramref name="action"/> on the UI thread after <paramref name="delay"/>.</summary>
    void Post(Action action, TimeSpan delay);

    /// <summary>Runs <paramref name="action"/> on the UI thread every <paramref name="interval"/> until disposed.</summary>
    IDisposable Every(TimeSpan interval, Action action);
}
