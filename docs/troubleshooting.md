# Troubleshooting

First step for almost everything: **Settings → Advanced → Copy diagnostics**. It copies the versions, driver state, devices, settings and the last 200 log lines. Paste it when you report a problem. Logs are in `%LOCALAPPDATA%\Controlability\logs\` (**Open logs folder** in the same place).

Tools that help:

- `joy.cpl` (Win+R) shows the game controllers Windows sees. With Controlability running you should see "Xbox 360 Controller for Windows" for each active player, and not your real PS4 controller.
- The command-line tool in the source tree: `dotnet run --project src/Controlability.Cli -- list` (what Controlability sees), `watch` (live values), `hidhide` (what is hidden), `unhide-all`.

## Contents

- [The controller is not detected](#the-controller-is-not-detected)
- [Doubled input](#doubled-input)
- [The controller disappeared (Unhide all)](#the-controller-disappeared)
- [Steam Input](#steam-input)
- [Anti-cheat games](#anti-cheat-games)
- [Bluetooth drops](#bluetooth-drops)
- [Driver install errors](#driver-install-errors)
- [Mode B problems](#mode-b-problems)
- [Minecraft Dungeons says "connect two or more controllers"](#minecraft-dungeons-says-connect-two-or-more-controllers)
- [Other](#other)

## The controller is not detected

1. Is it on the **Dashboard → Detected devices** list? If yes, it is detected: drag it to a player card (or check the slot policy in Settings → Input; *Manual* does not assign by itself).
2. If not: try another USB port and a data cable (some cables only charge). Check that Windows sees it in `joy.cpl` or *Settings → Bluetooth & devices*.
3. **Steam can hold the controller first.** Quit Steam, or turn off *PlayStation Configuration Support* (Steam → Settings → Controller), then replug the controller. See [Steam Input](#steam-input).
4. **Another program holds it**: DS4Windows, reWASD, a manufacturer tool or a game launcher. Close them. Two programs reading the same controller at once cause missing or doubled input.
5. **It is a real Xbox 360 wired controller.** Controlability ignores devices with the USB ID `045E:028E` (that is also the ID of its own virtual pads, which must never be read back). The game can use such a controller directly.
6. **It was hidden by HidHide earlier** and Controlability cannot see it: use **Settings → Hiding → Unhide all now**, then replug.
7. Bluetooth: remove the controller in Windows' Bluetooth list, then pair again (hold **PS + Share** on a DS4 until the light bar flashes quickly).
8. Still nothing: run the `list` command of the CLI and send the output with the diagnostics.

## Doubled input

Symptom: the character moves twice as much, a menu skips, or the game shows two controllers.

The game sees the **real controller and the virtual one**. Check, in this order:

1. **HidHide is not installed.** The dashboard shows a banner, and diagnostics say `HidHide: not installed`. Install it (the banner has a button) and restart Windows.
2. **Hiding is off.** Settings → Hiding → *Hide physical controllers from games* must be on.
3. **Emulation is paused.** While paused, the real controllers are visible on purpose (Settings → Hiding → *Show the real controllers again while paused*).
4. **The game process is on HidHide's allow-list.** In the HidHide Configuration Client, the *Applications* list should contain only Controlability. Remove the game from it.
5. **Steam is passing the controller through its own layer.** See [Steam Input](#steam-input).
6. **Mode B and the game list.** In Mode B, the keys are only kept from the game while it has the focus *and* it is in the game list. Add the game (Settings → Game for keyboard mode) or the game reads both your keys and the virtual keyboard pad. If the game does not take the keyboard pad as keys at all, try *Keyboard input → Normal keyboard*.
7. Open `joy.cpl`: you should see one Xbox 360 Controller per active player and no PS4 controller. If the PS4 controller is still there, hiding is not working: see "driver" below, and send the log.

## The controller disappeared

Your PS4 controller is missing in a game, in `joy.cpl`, or in another program, even though Controlability is closed.

This is HidHide still hiding it. Normally Controlability undoes it on exit, on a crash, and at the next start. If it did not:

1. **Settings → Hiding → Unhide all now** (also in the tray menu). Confirm. Replug the controller.
2. If Controlability will not start, run the command line: `Controlability.exe --cleanup` (in the install folder), or from the source tree `dotnet run --project src/Controlability.Cli -- unhide-all`.
3. Still missing: open the **HidHide Configuration Client** (Start menu) → *Devices* tab and untick your controller. Also check that *Enable device hiding* is not hiding something you want.
4. Reboot as the last resort.

Only devices that Controlability hid itself are ever unhidden by it. A device you hid manually in HidHide stays hidden.

## Steam Input

Steam can take over PlayStation controllers and present its own virtual controller. That gives a second, unmapped pad or doubled input.

- **Turn it off for everything:** Steam → Settings → Controller → turn off *PlayStation Configuration Support* (and *Generic Gamepad Configuration Support*). Then unplug and replug the controller.
- **Or only for one game:** right-click the game in Steam → Properties → Controller → *Disable Steam Input*.
- Controlability shows a tip on the dashboard while Steam is running. Dismiss it with *Got it*.

## Anti-cheat games

Some online games with kernel-level anti-cheat (for example EasyAntiCheat or BattlEye titles) dislike virtual controller drivers or HidHide, and may refuse to start or kick you. Controlability cannot change that.

- Before starting such a game: **Pause emulation** (tray), then **Settings → Hiding → Unhide all now**, then exit Controlability.
- Check the game's FAQ for controller or "virtual device" rules.
- Single-player and local co-op games are normally fine.

## Bluetooth drops

- Controlability keeps a player's virtual pad for **10 seconds** (Settings → Input → *Bluetooth grace period*) when the controller drops, so a short gap does not remove the player. Raise it if your connection is shaky; set it to *None* to remove the pad at once.
- Keep the controller within a couple of metres, away from USB 3 hubs and Wi-Fi routers (they share the 2.4 GHz band). A short USB extension cable for the Bluetooth dongle helps.
- Windows' Bluetooth driver power saving can switch the adapter off: Device Manager → Bluetooth adapter → Properties → Power Management → untick *Allow the computer to turn off this device*.
- A cheap Bluetooth adapter is a common cause. A wired connection is the most reliable.
- After sleep, controllers reconnect on their own within a few seconds; press the PS button if one does not.
- If a DS4 reconnects but the game lost the player, go back to the game's menu and re-join.

## Driver install errors

**ViGEmBus**

- Needs administrator rights and a restart is sometimes required. Run the installer again as administrator.
- ViGEmBus is retired (no new releases). It works on current Windows 10 and 11 builds, but a future Windows update could break it.
- After installing ViGEmBus, restart Controlability (the banner says so). If the banner stays, restart Windows once and check again.

**HidHide**

- Needs a restart of Windows after install. The wizard/dashboard turn green once the driver is loaded.
- Latest official release: <https://github.com/nefarius/HidHide/releases>.
- If the CLI of HidHide is slow (several seconds per call), Controlability still works: hiding happens in the background and does not freeze the window.

**The installer could not download a driver**: you are offline, or a proxy blocks GitHub. Continue anyway, then use the *Install driver* button in the app later, or download the installers by hand from the links above.

**Check what is installed**: *Settings → Advanced → Copy diagnostics* lists both versions; the dashboard chips show green when each is ready.

## Mode B problems

- **Nothing happens in the game.** The game must be in the list (Settings → Game for keyboard mode) and have the window focus. Use the exact program name from Task Manager → Details.
- **Keys also reach the game twice.** The game is not in the list, or its name does not match. Try *Normal keyboard* style.
- **The keyboard stopped working everywhere.** Press `Ctrl+Alt+Pause` (or `Ctrl+Break`). Mode B turns off at once. If the keyboard hook ever stops getting events, Controlability re-installs it by itself.
- **The wrong keyboard.** With several keyboards, Mode B listens to all of them by default. The CLI command `keys` lists them with ids; the chosen one is `KeyboardDevice` in `settings.json`.
- **The DS4 touchpad moves the mouse.** Windows treats the touchpad as a mouse. Controlability ignores Sony and Nintendo "mice" for the keyboard pad.

## Minecraft Dungeons says "connect two or more controllers"

That is expected in **Mode A** with one controller: the game counts only controllers as local players, not the keyboard. Use **Mode B** (dashboard → Mode B), add the game under *Settings → Game for keyboard mode*, and the keyboard becomes Player 1. See [compatibility.md](compatibility.md).

## Other

- **Two copies.** Starting Controlability a second time brings the first window forward. If nothing appears, look in the tray (hidden icons arrow).
- **A profile "disappeared".** A corrupt profile file is moved to `%APPDATA%\Controlability\profiles\broken\` and the log says why. Fix or delete it; you can *Reset to default* for shipped profiles.
- **Crash dialog.** Controlability unhides the controllers and removes the pads before closing. Use *Copy diagnostics* in the dialog and report it.
- **Reset everything**: exit the app, rename `%APPDATA%\Controlability` to `Controlability.old`, start again (the wizard runs again).
