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
| `hidhide` | Prints the HidHide version, cloaking, inverse state, whitelisted apps, all hidden devices, our `hidden.json` journal and the gaming devices HidHide can hide. Changes nothing. |
| `unhide-all` | Unhides every device in our journal (crash recovery). Devices hidden by something else stay hidden. |

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

## HidHide notes
- `HidHideCLI.exe` is found in `%ProgramFiles%\Nefarius Software Solutions\HidHide\x64`, then through the uninstall registry entry (`InstallLocation`), then on `PATH`. Tested with version 1.5.230.0.
- Real command names (from `--help`): `--cloak-on/off/toggle/state`, `--app-reg/unreg/list/clean "<exe>"`, `--dev-hide/unhide "<device instance path>"`, `--dev-list` (hidden devices), `--dev-gaming` / `--dev-all` (JSON), `--inv-on/off/state`, `--version`. They match the plan.
- The state commands print the command that would set that state: `--cloak-state` prints `--cloak-off`, `--app-list` prints `--app-reg "C:\...\x.exe"` per line, `--dev-list` prints `--dev-hide "<path>"` per line. We read the quoted part.
- `--dev-gaming` prints JSON (list of groups, each with `devices`). Its `symbolicLink` is the HID path SDL reports (case may differ), `deviceInstancePath` is what `--dev-hide` wants. DS4 over USB: `\\?\HID#VID_054C&PID_09CC&MI_03#9&343547ec&0&0000#{4d1e55b2-...}` becomes `HID\VID_054C&PID_09CC&MI_03\9&343547ec&0&0000`.
- `DevicePathConverter.Resolve` tries: symbolic link, converted instance path, then VID/PID of present devices (narrowed by serial). Two identical pads without a serial are not hidden rather than guessing.
- `HidHideService` only unhides what is in `%LOCALAPPDATA%\Controlability\hidden.json`. A device that was already hidden before we started is left alone and not journaled. Cloaking is turned on at the first hide and left on.
- The app registers its own exe (`Environment.ProcessPath`) in HidHide's application list and leaves it there. With `dotnet Controlability.Cli.dll` that exe would be `dotnet.exe`.
- Hiding runs synchronously on the engine thread (a few CLI calls, tens of milliseconds each), so other slots stall briefly when a controller is assigned.
- A killed process runs no handler: the journal is read on the next start (the DI factory recovers it when the hider is created) or by `unhide-all`.
- The CLI may need an elevated shell to change the configuration; the read-only commands (`hidhide`) worked without. If `Hide` fails the engine logs it and still connects the pad (games may see both devices).
