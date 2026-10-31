# Companion menu: Windows-side session controls (prototype 0.3)

This is **not** an in-game pause-menu patch. Undercover Lite displays a normal, topmost Windows window during a PCSX2 game session. It can be toggled by **Ctrl+Shift+F10** (global hotkey, opt-out available) or by restoring the launcher from the taskbar and choosing **Session menu**.

## What it does

- Prevents a second game from being started by the same launcher session.
- Shows options for the **next launch**: fullscreen, fast boot, and PCSX2 main-window visibility. Saving does not change the live emulator session.
- Requests that PCSX2 close its main window when one exists. If PCSX2 has no closable main window, it does not force-terminate it.
- Closes the companion panel and unregisters the hotkey when the process started by the launcher exits.

## What it does **not** do

- It does not pause the PS2 game or change PCSX2 live graphics, video, sound, controller mappings or BIOS settings.
- It does not patch game memory, intercept DirectX or Vulkan, inject DLLs or render its own graphics inside PCSX2.
- It is not guaranteed to appear on top of *exclusive fullscreen*. Switch PCSX2 to windowed/borderless presentation if the panel is obscured.
- It does not control PCSX2 instances launched outside Undercover Lite.
- The hotkey is available only while Undercover Lite stays open. Closing it leaves the game running but unregisters the hotkey.

## Windows testing checklist

1. Extract PCSX2 v2.8.2 and select an independently supplied PS2 image and BIOS.
2. Launch, verify that a second launch is disabled while PCSX2 runs.
3. With the emulator windowed, press Ctrl+Shift+F10: companion opens. Press again: it closes.
4. Save options and confirm `launcher.json` reflects the **next-launch** settings, not the live session.
5. Request graceful exit and verify PCSX2 can close without forced termination.
6. Alternatively exit PCSX2 normally and verify the launcher restores and the launch button becomes enabled.
7. Toggle off the hotkey, relaunch, and verify companion is accessible via the launcher button only.
8. If another program owns Ctrl+Shift+F10, verify the launcher is still usable and reports hotkey unavailability.

Automatic .NET smoke tests only cover source-agnostic session-gate logic, settings serialization and command planning. Actual keyboard focus, global hotkey and PCSX2-window behavior require Windows gameplay testing.