namespace Controlability.Core.Abstractions;

/// <summary>
/// Implemented by input sources that can drop and re-open all their devices, for example after the PC woke from sleep
/// and the OS handles went stale. Like rumble, it may be called from any thread; the work happens on the next poll.
/// </summary>
public interface ISourceRestart
{
    void Restart();
}
