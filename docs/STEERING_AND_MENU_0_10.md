# Undercover Lite PC 0.10: Steering and Menu Key Lab

This is a **separate, opt-in, reversible-by-selection experiment** for PCSX2 2.8.2. The launcher now generates one of six game-specific input profiles. None of them overwrite the original **UndercoverLite-DrivingFirst** profile from 0.9, shared controller mappings, PCSX2 hotkeys, the game, or the BIOS.

## Three steering strengths

The profiles set `[Pad1] AxisScale = 1.000000` and the individual `LLeftScale` and `LRightScale` values:

| Preset | Binding strength | Meaning |
| --- | --- | --- |
| Gentle | 0.65 | Limits digital A/D left-stick horizontal magnitude to 65% |
| Balanced | 0.85 | Limits digital A/D left-stick horizontal magnitude to 85% |
| Full | 1.00 | Full left/right magnitude |

**This does not create gradual steering** or ramp-up acceleration. A keyboard press still produces an immediate analog-stick target; the profile changes its **maximum magnitude**. PCSX2's 2.8.2 implementation of per-binding scaling is in `InputManager::AddPadBindings` (it reads each `<Binding>Scale` key), and DualShock 2's `AxisScale` is implemented in `PadDualshock2`. Handling will have to be measured in actual gameplay.

## Optional additional menu keys

When selected, a profile contains the normal `Cross = Keyboard/W` **and another** `Cross = Keyboard/Return`; it also adds `Triangle = Keyboard/Backspace` alongside `Triangle = Keyboard/Space`. PCSX2 2.8.2 stores multi-bindings as **repeated INI entries**, not simultaneous-key chords, and its input manager processes each entry.

This makes **Enter another Accept key** and **Backspace another Back key** in menus. But the same PC inputs still affect the same PS2 buttons **while driving**: Enter also accelerates, Backspace also handbrakes. It does not solve context-dependent switching or true native name entry.

## Critical PCSX2 hotkey conflict

**PCSX2 2.8.2 defaults Space to its own TogglePause hotkey**, while the proposed Undercover layout uses Space for handbrake. Escape similarly opens PCSX2's own pause menu, so it is not safe to assume Escape is a game Back button. The Steering Lab reads the isolated `PCSX2.ini` **Hotkeys** section when present and warns about potentially conflicting bindings.

The tool **never edits emulator hotkeys**. Before driving, use **Configure PCSX2 → Settings → Hotkeys** to inspect the Space assignment and, if needed, manually change it to a nonconflicting host key. Input profiles are not hotkey profiles.

## Using the new screen

1. Close PCSX2 and Undercover, then launch Undercover Lite 0.10 or newer.
2. Open **PC input / name-entry lab → Steering presets...**.
3. Choose **Gentle / Balanced / Full** and optionally enable **Enter/Backspace menu shortcuts**.
4. Read the default-hotkey warning and preview; click **Install selected profile...** to save an independent INI. Existing different files with the same profile name are never overwritten.
5. In PCSX2, right-click Undercover in the game list → **Properties → Summary → Input Profile** and choose the new profile. It is not activated automatically.
6. Check actual steering, gear shifting, pause behavior and menus. Switch to another profile or **Shared** through the same dropdown if needed.

PCSX2 documents input profiles and the fact that they cannot be switched dynamically while playing: https://pcsx2.net/docs/configuration/controllers/

## Validation

Automated tests cover all three steering strengths, separate filenames, legitimate repeated INI keys for additional menu bindings, safe install, identical reinstall, third-party profile conflict protection, refusal to modify external PCSX2 data, detection of conflicting Space/Escape hotkeys and alternative custom hotkeys. Real handling, bindings, and game-screen behavior remain **unverified until user testing**.

No copyrighted game resources, BIOS or PCSX2 binaries are included.
