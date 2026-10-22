# Developer notes

## Build and test
```
dotnet build
dotnet test
```

## CLI debug tool
Run from the repo root. Quote paths: the folders contain `[ ]` and spaces.

```
dotnet run --project src/Controlability.Cli -- <command> [index]
```

| Command | What it does |
|---------|--------------|
| `list` | Prints every connected controller with all `DeviceInfo` fields (id, VID:PID, serial, HID path, connection, type, capabilities), then exits. |
| `watch [index]` | Live single-line view of sticks, triggers, pressed buttons and battery at about 30 Hz. SDL is polled at 1000 Hz. Stops on Ctrl+C. Waits for the controller if it is unplugged and picks it up again when it returns. |
| `rumble [index]` | Rumbles at full strength for 1 s and cycles the LED red, green, blue. Says so when the device lacks rumble or an LED. |

| `forward [index]` | Maps the chosen controller with the PlayStation default layout and sends it to one virtual Xbox 360 pad. Game rumble goes back to the real controller. Prints the XInput user index. Ctrl+C removes the pad. |
| `latency [seconds=10] [index]` | Same as `forward` for N seconds, then prints p50 / p95 / p99 / max of `Poll()` start to `SubmitReport()` return, for submitted (changed) reports only. Move the sticks the whole time. |
| `driver` | Prints whether ViGEmBus is installed and its version (exit code 1 if missing). |

`index` is the zero-based position shown by `list` and defaults to 0.

## ViGEm notes
- Package: `Nefarius.ViGEm.Client` 1.21.256. One `ViGEmClient` per process, created lazily by `ViGEmSink`.
- Driver missing: `IsAvailable` is false, `Connect` returns `SlotHandle.Invalid`, every other call is a no-op.
- `AutoSubmitReport` is off. `Submit` compares with the last report sent and returns early if equal, otherwise writes all fields and calls `SubmitReport()` once.
- Pads are connected in slot order; Windows assigns the XInput user index in connect order. The index is read lazily with `GetXInputUserIndex(slot)` because the driver reports it shortly after connecting.
- Rumble: the game's large/small motor bytes raise `RumbleRequested`, which the Cli maps to `SetRumble(low = large, high = small)` on the real controller.
- Testing without the driver: `ViGEmSink` takes an internal `IXboxTargetFactory`; see `ViGEmSinkTests`.

Example `watch` line:
```
L  0.00,-0.03 | R -0.02,-0.02 | LT 0.00 RT 0.00 | South+DpadUp | Bat Full 100%
```
Sticks are -1..+1 with **up = +1**; triggers are 0..1.

## SDL3 notes
- Package: `ppy.SDL3-CS`. The native `SDL3.dll` is copied next to the executables under `runtimes\win-x64\native\`.
- All SDL calls run on the thread that calls `SdlControllerSource.Poll()`. `SetRumble` / `SetLed` only enqueue requests.
- SDL ignores XInput devices and VID/PID `045E:028E` so our own virtual pads are never read back. A real wired Xbox 360 pad has the same ID and is ignored too (decision D12).
- If a PS4 controller does not show up, check that Steam is not holding it (PlayStation Configuration Support) and that HidHide is not hiding it from this app.
