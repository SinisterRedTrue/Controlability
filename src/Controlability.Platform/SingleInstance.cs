namespace Controlability.Platform;

/// <summary>
/// Lets only one copy of the app run per user session. The first copy owns a named mutex and waits on a named event;
/// a second copy sets that event (<see cref="SignalFirst"/>) and exits, and the first copy shows its window.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private readonly bool _owner;
    private RegisteredWaitHandle? _registration;
    private bool _disposed;

    private SingleInstance(Mutex mutex, EventWaitHandle signal, bool owner)
    {
        _mutex = mutex;
        _signal = signal;
        _owner = owner;
    }

    /// <summary>True for the first copy. A second copy gets false and should call <see cref="SignalFirst"/> and exit.</summary>
    public bool IsFirst => _owner;

    /// <param name="name">Unique per app (and per test). The "Local\" prefix keeps it to the current session.</param>
    public static SingleInstance Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var mutex = new Mutex(initiallyOwned: true, $@"Local\{name}.mutex", out var owner);
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.show");
        return new SingleInstance(mutex, signal, owner);
    }

    /// <summary>Asks the first copy to bring its window forward.</summary>
    public void SignalFirst() => _signal.Set();

    /// <summary>Runs <paramref name="onSignal"/> (on a thread-pool thread) each time a later copy signals. First copy only.</summary>
    public void Listen(Action onSignal)
    {
        ArgumentNullException.ThrowIfNull(onSignal);
        if (!_owner || _registration is not null)
        {
            return;
        }

        _registration = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) => onSignal(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registration?.Unregister(null);
        if (_owner)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Released from another thread than the one that created it: the OS frees it at exit anyway.
            }
        }

        _mutex.Dispose();
        _signal.Dispose();
    }
}
