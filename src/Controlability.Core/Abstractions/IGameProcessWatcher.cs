namespace Controlability.Core.Abstractions;

/// <summary>Reports the process that owns the foreground window, and tells when it changes.</summary>
public interface IGameProcessWatcher : IDisposable
{
    /// <summary>Process name without ".exe", or null when unknown. Raised on a worker thread, only when it changed.</summary>
    event Action<string?>? ForegroundProcessChanged;

    void Start();

    void Stop();
}
