using Controlability.Core.Keyboard;
using Controlability.Core.Models;

namespace Controlability.Core.Abstractions;

/// <summary>The single keyboard + mouse pad of Mode B.</summary>
public static class KeyboardPad
{
    public static readonly DeviceId Id = new("keyboard-mouse");

    public static readonly DeviceInfo Info = new(
        Id, "Keyboard + Mouse", 0, 0, null, null, ConnectionType.Unknown, ControllerType.Keyboard, DeviceCapabilities.None);
}

/// <summary>
/// An <see cref="IInputSource"/> that exists only while Mode B is on. Nothing runs (no thread, no hook, no Raw Input
/// registration) until <see cref="Start"/>. <see cref="IInputSource.DeviceAdded"/> announces <see cref="KeyboardPad.Info"/>
/// from <see cref="Start"/>; <see cref="IInputSource.DeviceRemoved"/> follows <see cref="Stop"/>. Both are raised on the
/// caller's thread. <see cref="IInputSource.Poll"/> does nothing while stopped.
/// </summary>
public interface IKeyboardSource : IInputSource
{
    bool IsActive { get; }

    /// <summary>Raised on a worker thread when Ctrl+Alt+Pause was pressed. Blocking is already off by then.</summary>
    event Action? PanicRequested;

    /// <summary>Non-fatal problems (hook reinstalled, hook failed to install). Raised on a worker thread.</summary>
    event Action<string>? Diagnostic;

    /// <summary>Physical keyboards seen so far, for the settings picker and the CLI. Empty while stopped.</summary>
    IReadOnlyList<DeviceInfo> Keyboards { get; }

    /// <summary>Swallow the bound keys now. Off after <see cref="Start"/>, and stays off after a panic until the next start.</summary>
    bool Blocking { get; set; }

    /// <param name="keyboardId">Id of the one keyboard to listen to, or null for any keyboard.</param>
    /// <param name="targetProcesses">See <see cref="ForegroundPolicy"/>: where capture is allowed.</param>
    void Start(KeyBindings bindings, string? keyboardId, IReadOnlyList<string> targetProcesses);

    void SetBindings(KeyBindings bindings);

    /// <summary>Removes the hooks, closes Raw Input and releases every key.</summary>
    void Stop();
}
