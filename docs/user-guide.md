# Controlability user guide

Controlability turns almost any game controller (PS4, PS5, Switch Pro, generic USB or Bluetooth pads) into a virtual **Xbox 360 controller**, so games that only understand Xbox controllers work with it. It also lets a **keyboard and mouse share local co-op** with controllers.

## Contents

1. [Install](#install)
2. [First run](#first-run)
3. [The two modes, explained simply](#the-two-modes-explained-simply)
4. [Players and slots](#players-and-slots)
5. [Profiles](#profiles)
6. [The tray icon](#the-tray-icon)
7. [Settings](#settings)
8. [Uninstall](#uninstall)
9. [FAQ](#faq)

## Install

Needs Windows 10 (1809) or Windows 11, 64-bit. You do not need to install .NET.

1. Download `Controlability-Setup-<version>.exe` (and the `.sha256` file next to it, if you want to check it) from the [releases page](https://github.com/SinisterRedTrue/Controlability/releases).
2. Optional check, in PowerShell: `Get-FileHash .\Controlability-Setup-<version>.exe -Algorithm SHA256` should print the hash in the `.sha256` file.
3. Run the installer. It asks for administrator rights because it can install two drivers:
   - **ViGEmBus** creates the virtual Xbox controllers. *Required.*
   - **HidHide** hides your real controller from games so they do not see it twice. *Optional but recommended.*

   Drivers that are already on your PC are not offered again. If the installer is not bundled with the drivers it downloads them from their official GitHub pages; if you are offline you can continue and install them later.
4. Choose whether to add a desktop icon and whether to **start with Windows**.
5. Restart Windows if the installer asks (HidHide usually needs it).

## First run

The first time Controlability starts it shows a short wizard:

1. **Welcome**
2. **Drivers**: shows whether ViGEmBus and HidHide are installed. *Install* opens the official download page. After installing, this page checks again by itself every 2 seconds.
3. **Connect a controller**: plug in or pair one. It appears as soon as Windows sees it. You can skip this.
4. **Choose a mode**: A or B (see below). You can change it any time.
5. **Done**: the app lives in the tray. Closing the window does not stop it.

You can skip the wizard. Without ViGEmBus the app still starts but shows an **Install driver** banner and cannot create virtual controllers. Without HidHide everything works, but games may see the real controller as well as the virtual one (doubled input).

## The two modes, explained simply

The mode decides what happens to your **keyboard and mouse**. Your controllers always become virtual Xbox pads.

### Mode A — Keyboard-first co-op (default)

> The keyboard and mouse go to the game untouched. Each controller becomes a virtual Xbox pad.

Use it for games that accept a real keyboard for one player next to controllers for the others. Controllers fill Player 1 to Player 4.

### Mode B — Keyboard-as-controller

> Your keyboard and mouse pretend to be an Xbox controller in **Player 1**. Controllers start at **Player 2**.

Use it for games that only count **controllers** as local players, for example **Minecraft Dungeons** ("Connect two or more controllers to play local multiplayer"). The game then sees two pads: you on keyboard and mouse, a friend on a controller.

Things to know about Mode B:

- **Pick the game.** Mode B only listens, and only keeps keys away from the game, while the game has the focus. Open **Settings → Game for keyboard mode (Mode B)** and add the game from the *Running programs* list. With no game set, no key is ever swallowed, so your PC stays usable (but the game will see your keys twice).
- **Default keys:** `W A S D` = left stick, mouse = right stick, `Space` = A, `Shift` = B, `E` = X, `Q` = Y, left mouse button = right trigger, right mouse button = left trigger, `Tab` = Back, `Esc` = Start, arrow keys = D-pad, `Ctrl` = left stick click. Change them in **Settings → Keyboard controls** (click *Change*, then press a key or mouse button) or in the *Keyboard* tab of the keyboard profile.
- **Panic key: `Ctrl+Alt+Pause`** (or `Ctrl+Break`). It turns Mode B off immediately and releases every key. It always works.
- **Keyboard input style** (dashboard, Mode B only): *Xbox controller* uses the keys above. *Normal keyboard* keeps the keyboard's player slot (so the game still counts a connected controller) but leaves the pad idle and lets your real keyboard and mouse go to the game with its own default keys.
- Switching mode disconnects and reconnects the pads in slot order. Games read the player order when a pad connects, so do it before a game starts, or restart the game's co-op menu afterwards.

## Players and slots

The dashboard shows four **player cards** (Player 1 to 4). Each shows the device, the connection (USB or Bluetooth), the battery, the profile, the XInput number and a status dot (green = active, yellow = waiting for reconnect, grey = empty).

- **Assign:** drag a device from *Detected devices* onto a card, or right-click the device → *Assign to Player N*.
- **Right-click a card:** *Unassign*, *Change profile*, *Identify* (the controller rumbles and the light bar blinks, so you know which one it is).
- **Slot policy** (Settings → Input): *Auto* gives each new controller the next free slot; *Remember* gives a controller the slot it had last time; *Manual* leaves slots empty until you assign them.
- **Bluetooth grace period** (default 10 s): when a controller drops out, its virtual pad stays connected for that time, so a short Bluetooth gap does not remove the player from the game. If the controller returns in time it goes back to the same slot.
- **XInput number:** Windows numbers virtual pads in the order they connect. If Player 1 is empty and Player 2 connects first, the game sees it as controller 1. The card shows the real number.
- **Pause emulation** (dashboard or tray) holds every pad at neutral and shows your real controllers to Windows again until you un-pause.
- Light bar colours (PS4/PS5): Player 1 blue, Player 2 red, Player 3 green, Player 4 pink.

## Profiles

A profile is a saved set of mappings and settings. Open the **Profiles** page.

- **Shipped profiles:** PlayStation, Nintendo, Generic and Keyboard. They cannot be deleted, only reset to default.
- **Edit tabs:**
  - *Buttons*: click **Bind**, then press the controller button that should send that Xbox button. If that Xbox button was already sent by another button, the two swap.
  - *Sticks*: inner and outer dead zone and a response curve, with a live preview.
  - *Triggers*: dead zone, or digital (all-or-nothing) with a press point.
  - *Extras*: **gyro to right stick** (PS4, PS5, Switch Pro), **touchpad as mouse** (PS4/PS5), **turbo** (rapid-fire a button while held) and **toggle** (hold a button after one press).
  - *Keyboard*: keys and mouse sensitivity (keyboard profiles only).
  - *Applies to*: controller types and **games**. List a game's program and the profile switches in automatically while that game has the focus; the previous profile returns when you leave it.
- **Save** applies the profile to running players at once. Nothing changes in a running game until you save.
- **Import / Export / Duplicate / Reset** are at the top of the list. Profiles are plain JSON files in `%APPDATA%\Controlability\profiles`.
- The **Devices** page has a *Live test*: the left panel shows the raw controller input, the right panel what the game receives after mapping. It also has a rumble test, a light bar colour picker and a *Hide from games* switch per device.

## The tray icon

The icon sits next to the clock. Its colour shows the state: blue = running, grey = paused, red = ViGEmBus missing.

- **Left-click** opens the window. **Right-click** opens the menu: *Open*, the four players (with device names), *Pause emulation*, *Keyboard mode* (A or B), *Exit*.
- Toasts tell you when a controller connects or disconnects and when a battery is below 20 % and 10 %.
- Closing the window **hides it to the tray**; Controlability keeps running. Choose *Exit* in the tray menu to stop it. (You can turn this off in Settings → General.)
- *Exit* removes all virtual pads and gives every hidden controller back to Windows.

## Settings

| Section | What it has |
|---------|-------------|
| General | Start with Windows (adds a per-user startup entry, no admin rights), start minimized, close to tray |
| Input | Keyboard mode, slot policy, polling rate (1000, 500 or 250 Hz), Bluetooth grace period |
| Hiding | Hide real controllers (HidHide) on/off, show them again while paused, **Unhide all now** |
| Appearance | Theme (Windows / light / dark), language |
| Advanced | Log level, *Open logs folder*, **Copy diagnostics** |
| About | Version and the licences of the components Controlability uses |
| Game for keyboard mode (Mode B) | The programs whose focus makes Mode B listen and swallow keys |
| Keyboard controls (Mode B) | The key for each Xbox control |
| Controller buttons | A read-only list of what each button of the connected controller sends |

Settings are stored in `%APPDATA%\Controlability\settings.json`. Logs and the list of hidden devices are in `%LOCALAPPDATA%\Controlability`.

## Uninstall

*Settings → Apps → Installed apps → Controlability → Uninstall*, or the **Uninstall...** button in Controlability's own Settings → About (only on a copy installed with the installer). The uninstaller closes the app, gives every hidden controller back, removes the startup entry and asks whether to keep your settings and profiles. ViGEmBus and HidHide are separate programs and stay installed; remove them from the same Apps list if you no longer need them.

## FAQ

**Does it work with an Xbox controller?**
A real Xbox controller already speaks XInput, so Controlability ignores it and the game sees it directly. A wired Xbox 360 controller shares its USB ID with the virtual pad that Controlability creates, so it is ignored as well. It still works in games, just not through Controlability.

**Why do I need ViGEmBus?**
It is the driver that creates a virtual Xbox 360 controller. Without it Windows has nothing to show the game. ViGEmBus is no longer updated by its author but still works on Windows 10 and 11.

**Why do I need HidHide?**
The game would otherwise see both your real PS4 controller *and* the virtual Xbox pad, and often reacts to both (doubled input). HidHide hides the real one while Controlability uses it.

**My controller disappeared from other programs.**
That is the hiding. It is undone when you exit, pause, unassign, or use **Settings → Hiding → Unhide all now** (also in the tray). See [troubleshooting](troubleshooting.md).

**Can two people use two controllers with no keyboard?**
Yes, in either mode: each controller takes the next free slot.

**Will it work in online games with anti-cheat?**
Some anti-cheat systems dislike virtual controllers or HidHide. If a game complains, pause emulation and use *Unhide all now*, or do not use Controlability for that game. See [troubleshooting](troubleshooting.md#anti-cheat-games).

**Does it send any data anywhere?**
No. There are no accounts, telemetry or online features. The only network access is the installer downloading the drivers you tick, and your browser when you open a download link.

**Does it work with gyro or touchpad games?**
It can turn the gyro into right-stick movement and the touchpad into a mouse (profile *Extras* tab). It does not pass the raw gyro or touchpad to a game.

**Does it support a PS4 output (a virtual DualShock)?**
No. v1 outputs Xbox 360 only, which is what most games need.

**A game is not in the compatibility list.**
Try the generic test in [game-test-plan.md](game-test-plan.md) and see [compatibility.md](compatibility.md).
