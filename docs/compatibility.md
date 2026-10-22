# Game compatibility

Results from hands-on tests (see [game-test-plan.md](game-test-plan.md)). Anything not listed has not been tested; the generic XInput test in the test plan takes about 5 minutes.

- **Mode A** (keyboard-first co-op): the keyboard stays a keyboard; controllers become virtual Xbox pads.
- **Mode B** (keyboard-as-controller): the keyboard and mouse become virtual pad 1; controllers start at pad 2.
- ✅ works · ❌ does not work · ➖ not tested yet · ⚠ works with a catch (see notes).

| Game | Store | Mode A | Mode B | Notes |
|------|-------|--------|--------|-------|
| Minecraft Dungeons | Steam | ❌ keyboard is not counted as a player | ✅ | The game only counts controllers as local players. With one DS4 in Mode A it shows "connect two or more controllers". Use Mode B (keyboard = Player 1, DS4 = Player 2) and add the game's program under Settings → Game for keyboard mode. Rumble, unplug/replug and the Normal keyboard style also work. Turn off Steam's PlayStation Configuration Support. |
| Minecraft Dungeons | Microsoft Store / Xbox app | ❌ keyboard is not counted as a player | ✅ | Same result as Steam. |
| Rounds | Steam | ✅ keyboard + controller | ➖ | Accepts a real keyboard next to a pad, so Mode A is enough. |

## How to read the table

- **Mode A: no** means that one controller plus the keyboard does not start local co-op in that game.
- **Mode B: yes** means that keyboard and mouse as pad 1, plus a real controller as pad 2, play together with no doubled input.
- "Notes" lists the program name that worked, launchers, and anything that needs a setting.

## Adding a game

Run the generic test (Part 2 of [game-test-plan.md](game-test-plan.md)) and add a row with the game, the store, the result for each mode and a short note. If the game misbehaves, check [troubleshooting.md](troubleshooting.md) first.

## Known limits that affect compatibility

- Output is an **Xbox 360** controller only (no DualShock output). Games that need a DualShock-specific feature (touchpad or gyro passed raw) cannot get it.
- Games with kernel-level **anti-cheat** may refuse virtual controllers or HidHide. See [troubleshooting.md](troubleshooting.md#anti-cheat-games).
- A real wired **Xbox 360** controller is ignored by Controlability (it shares the USB ID of the virtual pads); the game can use it directly.
- Mode B swallows keys only while a listed game has the focus, and it needs the game's exact program name.
