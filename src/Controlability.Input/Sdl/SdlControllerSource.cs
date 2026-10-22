using System.Collections.Concurrent;
using System.Numerics;
using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using SDL;
using static SDL.SDL3;

namespace Controlability.Input.Sdl;

/// <summary>
/// Reads physical gamepads through SDL3.
/// <para>
/// <b>Threading:</b> every SDL call (init, event pump, state reads, rumble, LED, open/close) happens on the thread that
/// calls <see cref="Poll"/>. <see cref="SetRumble"/> and <see cref="SetLed"/> may be called from any thread: they only
/// enqueue a request that the next <see cref="Poll"/> drains. <see cref="TryGetState"/> returns the snapshot taken by the
/// last <see cref="Poll"/> and is safe from any thread. <see cref="DeviceAdded"/> / <see cref="DeviceRemoved"/> are
/// raised on the polling thread. <see cref="Dispose"/> also calls SDL, so call it on the polling thread (or after it stopped).
/// </para>
/// </summary>
public sealed unsafe class SdlControllerSource : IInputSource
{
    private static readonly TimeSpan BatteryRefresh = TimeSpan.FromSeconds(5);

    private readonly IClock _clock;
    private readonly bool _enableGyro;
    private readonly object _gate = new();
    private readonly Dictionary<uint, Device> _byInstance = [];
    private readonly Dictionary<DeviceId, Device> _byId = [];
    private readonly ConcurrentQueue<Command> _commands = new();
    private bool _started;
    private bool _disposed;

    public SdlControllerSource(IClock clock, bool enableGyro = false)
    {
        _clock = clock;
        _enableGyro = enableGyro;
    }

    public event Action<DeviceInfo>? DeviceAdded;

    public event Action<DeviceId>? DeviceRemoved;

    /// <summary>Non-fatal problems (a pad that failed to open, ...). Raised on the polling thread.</summary>
    public event Action<string>? Diagnostic;

    /// <summary>Currently connected devices (snapshot).</summary>
    public IReadOnlyList<DeviceInfo> Devices
    {
        get
        {
            lock (_gate)
            {
                return _byInstance.Values.Select(d => d.Info).ToList();
            }
        }
    }

    public void Poll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started)
        {
            SdlRuntime.Acquire();
            _started = true;
            OpenAlreadyConnected();
        }

        DrainCommands();
        SDL_PumpEvents();

        SDL_Event e;
        while (SDL_PollEvent(&e))
        {
            HandleEvent(in e);
        }

        var now = _clock.Ticks;
        foreach (var device in SnapshotDevices())
        {
            ReadState(device, now);
        }
    }

    public bool TryGetState(DeviceId id, out InputState state)
    {
        lock (_gate)
        {
            if (_byId.TryGetValue(id, out var device))
            {
                state = device.State;
                return true;
            }
        }

        state = default;
        return false;
    }

    public void SetRumble(DeviceId id, byte low, byte high, int durationMs)
        => _commands.Enqueue(new Command(CommandKind.Rumble, id, low, high, durationMs));

    public void SetLed(DeviceId id, byte r, byte g, byte b)
        => _commands.Enqueue(new Command(CommandKind.Led, id, r, g, b));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var device in SnapshotDevices())
        {
            SDL_CloseGamepad(device.Pad);
        }

        lock (_gate)
        {
            _byInstance.Clear();
            _byId.Clear();
        }

        if (_started)
        {
            SdlRuntime.Release();
        }
    }

    private void OpenAlreadyConnected()
    {
        using var ids = SDL_GetGamepads();
        if (ids is null)
        {
            return;
        }

        foreach (var id in ids)
        {
            Open(id);
        }
    }

    private void HandleEvent(in SDL_Event e)
    {
        switch ((SDL_EventType)e.type)
        {
            case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
                Open(e.gdevice.which);
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
                Close(e.gdevice.which);
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_TOUCHPAD_DOWN:
            case SDL_EventType.SDL_EVENT_GAMEPAD_TOUCHPAD_MOTION:
                UpdateTouch(e.gtouchpad, down: true);
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_TOUCHPAD_UP:
                UpdateTouch(e.gtouchpad, down: false);
                break;
            case SDL_EventType.SDL_EVENT_JOYSTICK_BATTERY_UPDATED:
                if (TryGetDevice(e.jbattery.which, out var device))
                {
                    RefreshBattery(device);
                }

                break;
        }
    }

    private void Open(SDL_JoystickID instanceId)
    {
        if (TryGetDevice(instanceId, out _))
        {
            return;
        }

        var pad = SDL_OpenGamepad(instanceId);
        if (pad == null)
        {
            Diagnostic?.Invoke($"SDL_OpenGamepad({(uint)instanceId}) failed: {SDL_GetError()}");
            return;
        }

        var vid = SDL_GetGamepadVendor(pad);
        var pid = SDL_GetGamepadProduct(pad);
        if (SdlMapping.IsOwnVirtualPad(vid, pid))
        {
            SDL_CloseGamepad(pad);
            return;
        }

        var serial = SDL_GetGamepadSerial(pad);
        var path = SDL_GetGamepadPath(pad);
        var id = SdlMapping.MakeDeviceId(vid, pid, serial, path);
        var type = SdlMapping.TranslateType(SDL_GetGamepadType(pad));
        var hasGyro = SDL_GamepadHasSensor(pad, SDL_SensorType.SDL_SENSOR_GYRO);
        var gyroEnabled = _enableGyro && hasGyro && SDL_SetGamepadSensorEnabled(pad, SDL_SensorType.SDL_SENSOR_GYRO, true);

        var props = SDL_GetGamepadProperties(pad);
        var capabilities = DeviceCapabilities.None;
        if (SDL_GetBooleanProperty(props, SDL_PROP_GAMEPAD_CAP_RUMBLE_BOOLEAN, false))
        {
            capabilities |= DeviceCapabilities.Rumble;
        }

        if (SDL_GetBooleanProperty(props, SDL_PROP_GAMEPAD_CAP_RGB_LED_BOOLEAN, false))
        {
            capabilities |= DeviceCapabilities.Led;
        }

        if (hasGyro)
        {
            capabilities |= DeviceCapabilities.Gyro;
        }

        if (SDL_GetNumGamepadTouchpads(pad) > 0)
        {
            capabilities |= DeviceCapabilities.Touchpad;
        }

        var battery = ReadBattery(pad);
        if (battery.State is BatteryState.Discharging or BatteryState.Charging or BatteryState.Full
            || type is ControllerType.PS4 or ControllerType.PS5 or ControllerType.SwitchPro)
        {
            capabilities |= DeviceCapabilities.Battery;
        }

        var info = new DeviceInfo(
            id,
            SDL_GetGamepadName(pad) ?? "Unknown controller",
            vid,
            pid,
            string.IsNullOrWhiteSpace(serial) ? null : serial,
            path,
            SdlMapping.TranslateConnection(SDL_GetGamepadConnectionState(pad)),
            type,
            capabilities);

        var device = new Device(pad, (uint)instanceId, info, gyroEnabled) { Battery = battery };
        lock (_gate)
        {
            if (_byId.ContainsKey(id))
            {
                SDL_CloseGamepad(pad);
                Diagnostic?.Invoke($"Ignoring duplicate device id {id}");
                return;
            }

            _byInstance[device.InstanceId] = device;
            _byId[id] = device;
        }

        ReadState(device, _clock.Ticks);
        DeviceAdded?.Invoke(info);
    }

    private void Close(SDL_JoystickID instanceId)
    {
        Device? device;
        lock (_gate)
        {
            if (!_byInstance.Remove((uint)instanceId, out device))
            {
                return;
            }

            _byId.Remove(device.Info.Id);
        }

        SDL_CloseGamepad(device.Pad);
        DeviceRemoved?.Invoke(device.Info.Id);
    }

    private void UpdateTouch(SDL_GamepadTouchpadEvent e, bool down)
    {
        if (e.touchpad != 0 || e.finger is < 0 or > 1 || !TryGetDevice(e.which, out var device))
        {
            return;
        }

        var point = new TouchPoint(Math.Clamp(e.x, 0f, 1f), Math.Clamp(e.y, 0f, 1f), down);
        if (e.finger == 0)
        {
            device.Touch0 = point;
        }
        else
        {
            device.Touch1 = point;
        }
    }

    private static BatteryInfo ReadBattery(SDL_Gamepad* pad)
    {
        int percent;
        var state = SDL_GetGamepadPowerInfo(pad, &percent);
        return SdlMapping.TranslateBattery(state, percent);
    }

    private static void RefreshBattery(Device device)
    {
        device.Battery = ReadBattery(device.Pad);
        device.NextBatteryRefresh = Environment.TickCount64 + (long)BatteryRefresh.TotalMilliseconds;
    }

    private void ReadState(Device device, long now)
    {
        if (Environment.TickCount64 >= device.NextBatteryRefresh)
        {
            RefreshBattery(device);
        }

        var pad = device.Pad;
        var state = new InputState
        {
            LeftX = SdlMapping.NormalizeAxis(SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX)),
            LeftY = SdlMapping.NormalizeStickY(SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY)),
            RightX = SdlMapping.NormalizeAxis(SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX)),
            RightY = SdlMapping.NormalizeStickY(SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY)),
            LeftTrigger = SdlMapping.NormalizeTrigger(SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER)),
            RightTrigger = SdlMapping.NormalizeTrigger(SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER)),
            Touch0 = device.Touch0,
            Touch1 = device.Touch1,
            Battery = device.Battery,
            TimestampTicks = now,
        };

        var buttons = ButtonId.None;
        foreach (var button in SdlMapping.ReadableButtons)
        {
            if (SDL_GetGamepadButton(pad, button))
            {
                buttons |= SdlMapping.TranslateButton(button);
            }
        }

        state.Buttons = buttons;

        if (device.GyroEnabled)
        {
            var data = stackalloc float[3];
            if (SDL_GetGamepadSensorData(pad, SDL_SensorType.SDL_SENSOR_GYRO, data, 3))
            {
                state.Gyro = new Vector3(data[0], data[1], data[2]);
                state.HasGyro = true;
            }
        }

        lock (_gate)
        {
            device.State = state;
        }
    }

    private void DrainCommands()
    {
        while (_commands.TryDequeue(out var command))
        {
            Device? device;
            lock (_gate)
            {
                _byId.TryGetValue(command.Id, out device);
            }

            if (device is null)
            {
                continue;
            }

            switch (command.Kind)
            {
                case CommandKind.Rumble when device.Info.Capabilities.HasFlag(DeviceCapabilities.Rumble):
                    SDL_RumbleGamepad(
                        device.Pad,
                        SdlMapping.ScaleRumble(command.A),
                        SdlMapping.ScaleRumble(command.B),
                        (uint)Math.Max(0, command.C));
                    break;
                case CommandKind.Led when device.Info.Capabilities.HasFlag(DeviceCapabilities.Led):
                    SDL_SetGamepadLED(device.Pad, command.A, command.B, (byte)command.C);
                    break;
            }
        }
    }

    private bool TryGetDevice(SDL_JoystickID instanceId, out Device device)
    {
        lock (_gate)
        {
            return _byInstance.TryGetValue((uint)instanceId, out device!);
        }
    }

    private List<Device> SnapshotDevices()
    {
        lock (_gate)
        {
            return [.. _byInstance.Values];
        }
    }

    private enum CommandKind
    {
        Rumble,
        Led,
    }

    /// <summary>Rumble: A = low, B = high, C = duration ms. LED: A = red, B = green, C = blue.</summary>
    private readonly record struct Command(CommandKind Kind, DeviceId Id, byte A, byte B, int C);

    private sealed class Device
    {
        public Device(SDL_Gamepad* pad, uint instanceId, DeviceInfo info, bool gyroEnabled)
        {
            Pad = pad;
            InstanceId = instanceId;
            Info = info;
            GyroEnabled = gyroEnabled;
        }

        public SDL_Gamepad* Pad { get; }

        public uint InstanceId { get; }

        public DeviceInfo Info { get; }

        public bool GyroEnabled { get; }

        public InputState State { get; set; }

        public TouchPoint Touch0 { get; set; }

        public TouchPoint Touch1 { get; set; }

        public BatteryInfo Battery { get; set; }

        public long NextBatteryRefresh { get; set; }
    }
}
