# Undercover Lite PC 0.15: Saved game-specific graphics override inspector

This update helps explain why changing the isolated PCSX2 rendering scale in **PC Settings → Graphics** might not visibly affect Exient's Undercover. It is a **read-only** comparison of the global setting and an identifiable saved Undercover game override. It is not a frame-time monitor, native game graphics patch, live emulator data reader or automatic game override editor.

## Verified PCSX2 2.8.2 behavior

The original PCSX2 **VMManager.cpp** creates per-game configuration names **SERIAL_ELFCRC.ini** and looks up a serial-specific INI using the current executable CRC, falling back to a legacy CRC-only INI when appropriate. These files live inside the **GameSettings** directory, normally under the PCSX2 data root. The graphics setting from **Pcsx2Config.cpp** and **GraphicsSettingsWidget.cpp** is **[EmuCore/GS] upscale_multiplier**.

Source files:
- https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/VMManager.cpp
- https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/Pcsx2Config.cpp
- https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2-qt/Settings/GraphicsSettingsWidget.cpp

## How to use

1. In the Undercover Lite launcher, select your own **uncompressed Undercover PS2 ISO**, with isolated PCSX2 data enabled.
2. Open **PC Settings → Graphics → Undercover graphics override → Inspect game graphics...**.
3. If a single matching **SERIAL_EIGHTHEXCRC.ini** file is found, the inspector reads its saved scale and compares it with the global PCSX2 setting.
4. If the saved game value differs, the tool warns that the per-game override **may supersede** the scale selected in the launcher.
5. Open **PCSX2 → Undercover Game Properties → Graphics** to inspect the actual per-game setting. The tool never changes or deletes game configuration files.
6. **Setup Doctor** includes a **Saved game graphics override** finding and a path-safe next step.

A result such as **Global 3×; saved game 2×** means that a possible override exists. It does **not** prove that PCSX2 is currently running that game executable CRC. A running process can also hold older values in memory.

## Conservative behavior

- Requires the verified Undercover serial from a bounded ISO9660 reader. Compressed images, unsupported images and unknown serials are not guessed.
- Only inspects isolated PCSX2 configuration, never shared profile settings.
- Does not create configuration files or game settings directories.
- Refuses arbitrary custom **[Folders] GameSettings** locations and redirected/symlinked directories.
- Requires a single exact **VERIFIEDSERIAL_EIGHTHEXCRC.ini** match. Multiple CRC variants are reported as ambiguous, not arbitrarily selected.
- Does not automatically assume global settings are in effect when no matching per-game INI exists; a legacy CRC-only file or another location may be used.
- Rejects invalid/unreadable, oversized, non-UTF-8, duplicated-key or non-finite graphics settings instead of presenting incorrect values.
- Reports numeric scales and explanations only. Copied Setup Doctor reports contain no absolute game paths or emulator configuration paths.
- Never updates global graphics settings, per-game graphics settings, controller profiles, BIOS, game images, saves or texture replacements.

## Automated validation

Synthetic Windows smoke tests cover uninitialized PCSX2 folders, non-isolated profile refusal, compressed image limitations, missing game files, unique matching serial/CRC, other game/region INI exclusion, a conflicting per-game scale, identical settings, no scale override, legitimate non-integer multipliers, invalid floats, duplicate keys, multiple CRC variants, custom settings folders, privacy-safe reports and read-only file preservation.

Automated tests do not certify which CRC the game loads or verify rendering in a real PCSX2 session.
