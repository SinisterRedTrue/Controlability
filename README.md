# Controlability

Turn any game controller into a virtual **Xbox 360 controller** on Windows, and play local co-op with a **keyboard and a controller** on the same PC.

Many PC games (Minecraft Dungeons, most Unity and Unreal games, Game Pass titles) only understand Xbox controllers. PlayStation, Switch and generic pads are ignored or mapped wrongly. Controlability reads your controller, shows it to the game as an Xbox pad, and hides the real one so nothing is doubled.

<!-- Screenshots: replace the placeholders with real images in docs/images/ -->
| Dashboard | Devices and live test | Mapping editor |
|-----------|----------------------|----------------|
| ![Dashboard](docs/images/dashboard.png) | ![Devices](docs/images/devices.png) | ![Mapping editor](docs/images/profiles.png) |

> Screenshots are placeholders until the v1.0 images are added.

## Features

- **Any controller to Xbox 360**: PS4, PS5, Switch Pro and generic USB/Bluetooth pads, through SDL3. Rumble goes back to the real controller.
- **Four player slots** with auto, remember and manual assignment, a Bluetooth grace period (a short drop does not remove the player), and a light bar colour per player.
- **No doubled input**: HidHide hides the real controller from games, and gives it back when you exit, pause, or after a crash.
- **Two keyboard modes**
  - *Mode A, keyboard-first co-op*: the keyboard and mouse stay a keyboard; controllers become pads.
  - *Mode B, keyboard-as-controller*: the keyboard and mouse become pad 1 and controllers start at pad 2. Needed by games that only count controllers as players (Minecraft Dungeons). A panic key (`Ctrl+Alt+Pause`) always turns it off.
- **Profiles**: button remapping, dead zones, response curves, triggers, import/export, and automatic profile switching when a game gets the focus.
- **Extras**: gyro to right stick, touchpad as mouse, turbo, toggle buttons, battery display and low-battery alerts.
- **Tray app**: runs in the background, starts with Windows if you want, with toasts and a quick menu.
- **Fail safe**: if a driver is missing the app still starts and tells you what to install.
- **No telemetry, no accounts, no online features.**

## Download and install

1. Download `Controlability-Setup-<version>.exe` from the [releases page](https://github.com/SinisterRedTrue/Controlability/releases). A `.sha256` file is next to it.
2. Run it. The installer can also install the two drivers it needs:
   - [ViGEmBus](https://github.com/nefarius/ViGEmBus) creates the virtual Xbox controllers (required).
   - [HidHide](https://github.com/nefarius/HidHide) hides the real controller from games (recommended; Windows restarts once).
3. Start Controlability and follow the first-run wizard.

Requires Windows 10 (1809) or Windows 11, 64-bit. .NET is bundled.

Read the [user guide](docs/user-guide.md) next. If something does not work, see [troubleshooting](docs/troubleshooting.md). Tested games are listed in [compatibility](docs/compatibility.md).

## Build from source

Requirements: Windows 10/11 x64 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). ViGEmBus and HidHide are needed to run the app, not to build it.

```powershell
dotnet restore Controlability.sln
dotnet build Controlability.sln --configuration Release --no-restore
dotnet test Controlability.sln --configuration Release --no-build
dotnet run --project src/Controlability.App
```

A command-line debug tool is included (`list`, `watch`, `rumble`, `forward`, `run`, `latency`, `driver`, `hidhide`, `unhide-all`; see [dev notes](docs/dev-notes.md)):

```powershell
dotnet run --project src/Controlability.Cli -- list
```

To build the installer you also need [Inno Setup 6.3 or newer](https://jrsoftware.org/isdl.php):

```powershell
.\build-release.ps1
```

It tests, publishes a self-contained build, runs Inno Setup, and writes `artifacts\Controlability-Setup-<version>.exe` and its `.sha256`.

Paths in this repository can contain `[ ]` and spaces: quote them, and use `-LiteralPath` in PowerShell.

## Structure

| Project | Responsibility |
| --- | --- |
| `Controlability.Core` | Models, mapping, profiles, abstractions (no Windows dependencies) |
| `Controlability.Input` | SDL3 controllers, Raw Input keyboard and mouse |
| `Controlability.Output` | Virtual Xbox 360 pads (ViGEm) |
| `Controlability.Platform` | HidHide, process watcher, autostart, single instance, logging |
| `Controlability.Engine` | Input thread, slot manager, `IEngine` |
| `Controlability.App` | WPF desktop app |
| `Controlability.Cli` | Console debug tool |
| `tests/` | Core, Engine and App tests (xUnit) |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Controlability is released under the [MIT License](LICENSE). Copyright (c) 2026 SinisterRed.

It uses SDL3 (zlib), ViGEm.Client, WPF-UI, H.NotifyIcon and CommunityToolkit.Mvvm (MIT), and installs ViGEmBus (BSD-3-Clause) and HidHide (MIT). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
