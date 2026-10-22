# Game test plan

Hands-on tests for v1.0. Fill the **Result** column with ✅ / ❌ and a note, then send the failures (with "Copy diagnostics" from Settings → Advanced) so they can be fixed.

## Before you start

- Controlability is installed and starts without a driver banner (ViGEmBus and HidHide both installed and Windows restarted once).
- A DS4 on USB **and** Bluetooth if possible. A second pad (PS5, Switch, generic) is a bonus.
- Close Steam's controller support for the game: Steam → Settings → Controller → turn **off** *PlayStation Configuration Support* (and *Generic Gamepad Configuration Support*), or turn it off per game in the game's controller options.
- Know where the logs are: `%LOCALAPPDATA%\Controlability\logs\`.
- `joy.cpl` (Win+R) shows what Windows sees. `Ctrl+Alt+Pause` (or `Ctrl+Break`) is the panic key: it turns Mode B off at once.

### Find the game's program name (needed for Mode B)

Mode B only listens and swallows keys while the game has the focus. The name must match the running program exactly (case does not matter, `.exe` is optional).

1. Start the game and reach its main menu.
2. Alt+Tab to Controlability → **Settings → Game for keyboard mode (Mode B)**.
3. Press **Refresh list**, pick the game under **Running programs**, press **Add selected**.
4. If nothing in the list looks right: Task Manager → *Details* tab → find the game's `.exe` (Unreal games often have a launcher exe and a second `…-Win64-Shipping.exe`; the one that owns the game window is the one that counts). Type that name into the box and press **Add**.

Write down the name that worked: **______________**

---

## Part 1 — Minecraft Dungeons

Minecraft Dungeons only counts **controllers** as local players. Its menu says "Connect two or more controllers to play local multiplayer", so a real keyboard next to one pad is not enough. That is why Mode B exists.

Run Parts 1A to 1E on the version you have. Part 1F covers the Steam and Microsoft Store/Xbox app differences; repeat 1B and 1D on the second version if you own both.

### 1A. Mode A, one DS4 (expected: no local co-op)

Purpose: confirm and document that Mode A cannot start co-op in this game.

| # | What to do | What should happen | Result |
|---|-----------|--------------------|--------|
| A1 | In Controlability, set the mode to **A – Keyboard-first co-op**. Plug in one DS4 (USB). Check the dashboard: Player 1 is the DS4, with an XInput index. | `joy.cpl` lists only "Xbox 360 Controller for Windows" (the DS4 itself is hidden). | |
| A2 | Start Minecraft Dungeons, open the local multiplayer menu. | The game shows **"connect two or more controllers"** (or equivalent). The DS4 is recognised as a controller (button prompts are Xbox style; Cross = A). | |
| A3 | Press the real keyboard keys in the menu. | The keyboard works as a normal keyboard (it is not swallowed), but it is not counted as a player. | |
| A4 | Only if you have **two** controllers: plug in the second one too. | Both appear as Player 1 and Player 2 on the dashboard and the game offers 2-player local co-op. Both can move and act independently. | |

Result for the table: Mode A with one DS4 → no (A2). Mode A with two pads → A4.

### 1B. Mode B, keyboard pad + DS4 (the main test)

| # | What to do | What should happen | Result |
|---|-----------|--------------------|--------|
| B1 | Quit the game. In Controlability set the mode to **B – Keyboard-as-controller**. Under *Keyboard input* choose **Xbox controller**. Make sure the game from the step above is in the Mode B game list. | Dashboard: Player 1 = **Keyboard**, Player 2 = DS4 (plug it in now if it is not). No keys are swallowed yet (no game has the focus). | |
| B2 | Open `joy.cpl` → Properties of the first device. | Two "Xbox 360 Controller for Windows" entries. Pad 1 reacts to W/A/S/D, the mouse and Space; pad 2 reacts to the DS4. | |
| B3 | Start the game, open the local multiplayer menu. | **No** "connect two or more controllers" message. The game offers 2 players. | |
| B4 | Join with both players. Move player 1 with W/A/S/D, aim with the mouse, attack with the left mouse button, roll with Space. | Player 1 moves, aims and acts. Nothing happens on player 2 at the same time. | |
| B5 | Move player 2 with the DS4 left stick, attack with Square/R2 as the game binds it. | Player 2 acts independently of player 1. Cross = A, Circle = B, Square = X, Triangle = Y as in the PlayStation layout. | |
| B6 | Both players move and attack at the same moment for 1 minute. | No stuck keys, no input going to the wrong player, no drops. | |
| B7 | Press `Esc` and `Tab` in the game. | `Esc` = Start (pause menu), `Tab` = Back (map / inventory) as in the default keyboard profile. | |
| B8 | While the game has the focus, check that typed keys do not leak: Alt+Tab to Notepad and type. | In Notepad every key types normally (the focus left the game, so nothing is swallowed). | |
| B9 | In the game, press `Ctrl+Alt+Pause`. | Mode B turns off at once; the keyboard and mouse work normally everywhere. Turn Mode B back on from the dashboard when you are done. | |
| B10 | Play a whole level (about 10 minutes) and watch Task Manager. | Controlability stays under about 1.5 % CPU and 80 MB RAM; the game has no input hitches. | |

If player 1 also reacts to the *same* keys through the game's own keyboard controls (double movement), note it: switch *Keyboard input* to **Normal keyboard** and repeat B3 to B6 to see whether the game then takes the real keyboard and mouse while still counting two controllers. Record what happens in the notes.

### 1C. Rumble

| # | What to do | What should happen | Result |
|---|-----------|--------------------|--------|
| R1 | Devices page → pick the DS4 → **Rumble test**. | The DS4 vibrates for about 0.3 s. | |
| R2 | In the game, take damage or use a rumble-heavy attack with the DS4 player. | The DS4 vibrates, and only the DS4 (not another pad). | |
| R3 | Game settings: turn controller vibration off (if the game has the option), repeat R2. | No vibration. Turn it on again. | |
| R4 | Dashboard → right-click the DS4 card → **Identify**. | The DS4 rumbles and the light bar blinks 3 times, then shows the slot colour (P1 blue, P2 red, P3 green, P4 pink). | |

### 1D. Disconnect and reconnect in the middle of a game

| # | What to do | What should happen | Result |
|---|-----------|--------------------|--------|
| D1 | During co-op, **unplug the USB DS4**. Count 5 seconds, plug it back in. | The slot shows "Waiting for reconnect". The game does **not** remove player 2. After replugging the DS4 returns to Player 2 and works again. | |
| D2 | Unplug the DS4 and wait **more than 10 seconds**. | The slot empties and the game sees the second pad leave (the player drops out). | |
| D3 | Plug it back in. | It takes the free slot again (Player 2) and the game can re-join it. | |
| D4 | Switch the DS4 from USB to Bluetooth (unplug, hold PS + Share to pair if needed, or use it already paired). | Same device, same slot, works. The card says Bluetooth. | |
| D5 | Turn the Bluetooth DS4 off (hold the PS button ~10 s) and on again within 10 s. | The player stays in the game and the pad comes back. | |
| D6 | Pause emulation from the dashboard or the tray. Un-pause. | While paused, pads are neutral and the real DS4 is visible again (`joy.cpl` shows it). After un-pausing, it is hidden again and works. | |
| D7 | Let the PC sleep for a minute and wake it, with the game running. | The controllers reconnect within a few seconds, no stuck slot. If the game lost the pads, leaving to its main menu and back should fix it. | |

### 1E. Exit and safety

| # | What to do | What should happen | Result |
|---|-----------|--------------------|--------|
| E1 | Exit Controlability from the tray menu while the DS4 is assigned. | The virtual pads disappear; the DS4 shows up in `joy.cpl` as a normal game controller again. | |
| E2 | Start it again, assign the DS4, then kill `Controlability.exe` in Task Manager. | The DS4 is hidden from `joy.cpl` now (that is expected after a kill). Start Controlability again: the DS4 is visible and then re-assigned. If it ever stays missing: Settings → Hiding → **Unhide all now**, or `Controlability.Cli unhide-all`. | |

### 1F. Steam version vs Microsoft Store / Xbox app version

Both versions read controllers through XInput, so the Controlability setup is the same. What differs is what sits between the controller and the game.

| # | What to do | What should happen | Result |
|---|-----------|--------------------|--------|
| V1 | **Steam:** with *PlayStation Configuration Support* still **on**, start the game with the DS4 and Controlability running. | Expect the Steam tip on the dashboard ("Steam is running"). Possible problem: the game sees an extra, unmapped pad or doubled input. Note what happens. | |
| V2 | **Steam:** turn Steam's PlayStation support off (see "Before you start") and repeat B3–B6. | Clean: no extra pad, no doubled input. | |
| V3 | **Microsoft Store / Xbox app (Game Pass):** find the program name (it lives under `WindowsApps`, so Task Manager → Details may show the name but "Open file location" can be denied; that is fine, you only need the name). Add it to the Mode B list and repeat B1–B6. | Same behaviour as Steam. Note any difference (for example the game ignoring Mode B, or the name not matching). | |
| V4 | **Microsoft Store / Xbox app:** start the game through the Xbox app, then check Controlability's *Running programs* list. | The game's program is listed once it has a window. | |
| V5 | **Either:** the game starts through a launcher and then opens a second process. | Make sure the name in the Mode B list is the one that owns the game window. | |

---

## Part 2 — Any other XInput game (short test, about 5 minutes)

Use this for every game you add to `compatibility.md`. Pick Mode A or B depending on what the game accepts.

| # | What to do | What should happen | Result |
|---|-----------|--------------------|--------|
| X1 | Start Controlability, connect a controller. Check the dashboard. | Controller assigned to Player 1 (Mode A) or Player 2 (Mode B). No driver banner. | |
| X2 | Start the game, go to the controller settings or a menu. | The game shows Xbox button prompts and responds to the pad. No doubled input (no "second controller" showing up). | |
| X3 | Move both sticks fully, press every button and both triggers. | Everything reacts; sticks rest in the centre; triggers go smoothly from 0 to full. | |
| X4 | Trigger some rumble (a hit, an explosion). | The controller vibrates. | |
| X5 | **Co-op games:** use the game's local multiplayer with two pads (Mode A) or keyboard + pad (Mode B). | Each player is separate. Say in the notes whether the game accepts a real keyboard next to a pad (Mode A) or only controllers (Mode B). | |
| X6 | Unplug and replug the controller (or toggle Bluetooth) within 10 s. | The player stays in the game. | |
| X7 | Quit the game, exit Controlability from the tray. | The real controller is visible in `joy.cpl` again. | |

Then add a row to `docs/compatibility.md`:

- **Mode A:** yes / no / partly (what part).
- **Mode B:** yes / no / partly.
- **Notes:** program name, store, anything unusual (anti-cheat, launcher, needs Steam Input off).

## How to report a result

Paste the filled table rows (or just the failing ones) plus:

1. Windows version, the controller(s) and how they were connected (USB/Bluetooth).
2. Which version of the game (Steam / Microsoft Store) and which mode.
3. The output of Settings → Advanced → **Copy diagnostics** (it contains versions, devices, settings and the last 200 log lines).
