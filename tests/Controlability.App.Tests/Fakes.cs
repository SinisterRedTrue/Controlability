using Controlability.App.Services;
using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Core.Settings;
using Controlability.Engine;

namespace Controlability.App.Tests;

public sealed class FakeClock : IClock
{
    public long Ticks { get; private set; }

    public double ToMs(long ticks) => ticks / 1000.0;

    public void Advance(TimeSpan time) => Ticks += (long)(time.TotalMilliseconds * 1000);
}

/// <summary>Queues posted work; a test runs it with <see cref="RunDue"/> or <see cref="RunAll"/>.</summary>
public sealed class FakeUiThread : IUiThread
{
    public List<(Action Action, TimeSpan Delay)> Posted { get; } = [];

    public List<(TimeSpan Interval, Action Action)> Timers { get; } = [];

    public void Post(Action action) => Posted.Add((action, TimeSpan.Zero));

    public void Post(Action action, TimeSpan delay) => Posted.Add((action, delay));

    public IDisposable Every(TimeSpan interval, Action action)
    {
        Timers.Add((interval, action));
        return new Handle(() => Timers.RemoveAll(t => t.Action == action));
    }

    public void RunAll()
    {
        var work = Posted.ToList();
        Posted.Clear();
        foreach (var (action, _) in work)
        {
            action();
        }
    }

    private sealed class Handle(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}

public sealed class FakeUrlLauncher : IUrlLauncher
{
    public List<string> Opened { get; } = [];

    public void Open(string url) => Opened.Add(url);
}

public sealed class FakeProfileCatalog(params ProfileChoice[] profiles) : IProfileCatalog
{
    public IReadOnlyList<ProfileChoice> GetProfiles() => profiles;
}

public sealed class FakeNotifier : INotifier
{
    public List<(string Title, string Message)> Notifications { get; } = [];

    public void Notify(string title, string message) => Notifications.Add((title, message));
}

public sealed class FakeShell : IShell
{
    public int ShowCount { get; private set; }

    public int ExitCount { get; private set; }

    public void ShowMainWindow() => ShowCount++;

    public void Exit() => ExitCount++;
}

/// <summary>An <see cref="IEngine"/> that records commands and lets a test change what it reports.</summary>
public sealed class FakeEngine : IEngine
{
    public bool IsRunning { get; set; } = true;

    public bool IsPaused { get; set; }

    public KeyboardMode Mode { get; set; } = KeyboardMode.KeyboardFirst;

    public KeyboardInputStyle KeyboardStyle { get; set; } = KeyboardInputStyle.XboxController;

    public IReadOnlyList<DeviceInfo> Devices { get; set; } = [];

    public IReadOnlyList<SlotStatus> Slots { get; set; } =
    [
        .. Enumerable.Range(0, 4).Select(i => new SlotStatus(i, null, null, null, SlotState.Empty, null)),
    ];

    public DriverStatus DriverStatus { get; set; } = new(true, true);

    public string? ActiveGame { get; set; }

    public long TickErrorCount => 0;

    public Dictionary<DeviceId, InputState> Live { get; } = [];

    public List<string> Calls { get; } = [];

    public int ChangedSubscribers => Changed?.GetInvocationList().Length ?? 0;

    public List<DeviceId> Hidden { get; } = [];

    public IReadOnlyCollection<DeviceId> HiddenDevices => Hidden;

    public event Action? Changed;

    public static DeviceInfo MakeDevice(string name, ControllerType type = ControllerType.PS4, ConnectionType connection = ConnectionType.Usb)
        => new(new DeviceId(name), name, 0x054C, 0x09CC, name, null, connection, type, DeviceCapabilities.Rumble | DeviceCapabilities.Led);

    public static SlotStatus Active(int index, DeviceInfo device, string profile = "default-ps4", int? xinput = null)
        => new(index, device.Id, device.Name, profile, SlotState.Active, xinput ?? index);

    public void RaiseChanged() => Changed?.Invoke();

    public void Start() => Calls.Add("Start");

    public void Stop() => Calls.Add("Stop");

    public void Pause(bool paused) => Calls.Add($"Pause:{paused}");

    public void Assign(DeviceId device, int slot) => Calls.Add($"Assign:{device}:{slot}");

    public void Unassign(int slot) => Calls.Add($"Unassign:{slot}");

    public void SetProfile(int slot, string profileId) => Calls.Add($"SetProfile:{slot}:{profileId}");

    public void SetKeyboardStyle(KeyboardInputStyle style) => Calls.Add($"SetKeyboardStyle:{style}");

    public void SetKeyboardTargets(IReadOnlyList<string> processNames) => Calls.Add($"SetKeyboardTargets:{string.Join(",", processNames)}");

    public void Identify(int slot) => Calls.Add($"Identify:{slot}");

    public void SetMode(KeyboardMode mode) => Calls.Add($"SetMode:{mode}");

    public void ReloadProfile(string profileId) => Calls.Add($"ReloadProfile:{profileId}");

    public void SetDeviceHidden(DeviceId device, bool hidden)
    {
        Calls.Add($"SetDeviceHidden:{device}:{hidden}");
        if (hidden)
        {
            Hidden.Add(device);
        }
        else
        {
            Hidden.Remove(device);
        }
    }

    public void TestRumble(DeviceId device) => Calls.Add($"TestRumble:{device}");

    public void SetLedColor(DeviceId device, byte r, byte g, byte b) => Calls.Add($"SetLedColor:{device}:{r},{g},{b}");

    public void ApplySettings() => Calls.Add("ApplySettings");

    public void UnhideAll() => Calls.Add("UnhideAll");

    public void RestartInput() => Calls.Add("RestartInput");

    public bool TryGetLiveState(DeviceId device, out InputState raw, out XboxState mapped)
    {
        mapped = default;
        return Live.TryGetValue(device, out raw);
    }

    public void Dispose()
    {
    }
}
