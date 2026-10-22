using Controlability.Core.Abstractions;
using Controlability.Core.Keyboard;
using Controlability.Core.Models;

namespace Controlability.Engine.Tests.Runtime;

/// <summary>Stands in for the Raw Input keyboard source: counts what the engine asks of it and never touches Windows.</summary>
public sealed class FakeKeyboardSource : IKeyboardSource
{
    private readonly object _gate = new();
    private InputState _state = InputState.Neutral;

    public event Action<DeviceInfo>? DeviceAdded;

    public event Action<DeviceId>? DeviceRemoved;

    public event Action? PanicRequested;

    public event Action<string>? Diagnostic;

    public bool IsActive { get; private set; }

    public IReadOnlyList<DeviceInfo> Keyboards => [];

    public bool Blocking { get; set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public int PollCount { get; private set; }

    public string? StartedWithKeyboardId { get; private set; }

    public KeyBindings? Bindings { get; private set; }

    public bool Disposed { get; private set; }

    public IReadOnlyList<string> TargetProcesses { get; private set; } = [];

    public void Start(KeyBindings bindings, string? keyboardId, IReadOnlyList<string> targetProcesses)
    {
        TargetProcesses = targetProcesses;
        StartCount++;
        StartedWithKeyboardId = keyboardId;
        Bindings = bindings;
        IsActive = true;
        DeviceAdded?.Invoke(KeyboardPad.Info);
    }

    public void SetBindings(KeyBindings bindings) => Bindings = bindings;

    public void Stop()
    {
        StopCount++;
        IsActive = false;
        Blocking = false;
        DeviceRemoved?.Invoke(KeyboardPad.Id);
    }

    public void SetState(InputState state)
    {
        lock (_gate)
        {
            _state = state;
        }
    }

    /// <summary>What the hook does itself: blocking off first, then the engine is told.</summary>
    public void Panic()
    {
        Blocking = false;
        PanicRequested?.Invoke();
    }

    public void RaiseDiagnostic(string message) => Diagnostic?.Invoke(message);

    public void Poll() => PollCount++;

    public bool TryGetState(DeviceId id, out InputState state)
    {
        lock (_gate)
        {
            state = _state;
            return IsActive && id == KeyboardPad.Id;
        }
    }

    public void SetRumble(DeviceId id, byte low, byte high, int durationMs)
    {
    }

    public void SetLed(DeviceId id, byte r, byte g, byte b)
    {
    }

    public void Dispose() => Disposed = true;
}
