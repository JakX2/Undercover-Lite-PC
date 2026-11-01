# Undercover Lite PC 0.17: exact game-specific resolution control

This milestone adds an opt-in **Undercover-only internal resolution** selector to PC Settings > Graphics. It writes only the `[EmuCore/GS] upscale_multiplier` setting in an **existing, exactly verified** `SERIAL_EIGHTHEXCRC.ini` in the isolated PCSX2 2.8.2 `gamesettings` directory. The global PCSX2.ini is never edited by this action.

## Procedure

1. Select your own **uncompressed Undercover PS2 ISO**, keep isolated PCSX2 data enabled, and complete PCSX2 setup.
2. If the launcher says the game-specific INI is missing, open Undercover's **Game Properties** within PCSX2 and create/save a per-game setting there first. The launcher deliberately does not fabricate a game-specific INI.
3. Exit the game and **close all PCSX2 windows** before using the editor.
4. Open **PC Settings > Graphics > Undercover-only resolution**, select Native (1×), 2×, 3×, 4× or 6×, then choose **Apply for game...**.
5. Confirm the warning. The editor re-reads the verified BOOT2 ELF CRC, selected target and the global/game configuration snapshots. If either configuration has changed, it refuses the edit.
6. Restart PCSX2 to assess the actual result in game.

## Constraints and safeguards

- Exact ISO-derived serial and PCSX2 ELF word-XOR CRC are required; no serial-only guess, wrong CRC, or legacy CRC-only fallback is edited.
- Only existing exact files are editable. No settings folders or INIs are created.
- No shared profile or custom/redirected GameSettings path is edited. The editor checks existing path ancestors for reparse points.
- Rejects huge/binary/malformed UTF-8 files, duplicate relevant sections/keys, unsupported scale values and stale preview hashes.
- Original file is saved with a unique timestamped `.bak` suffix before the staged replacement, and unrelated INI fields are preserved. No-op writes do not create backups.
- The source ISO, BIOS, saves, input bindings, global graphics and unrelated regional/CRC profiles remain untouched.
- This changes a **saved PCSX2 per-game rendering preference**, not the PS2 executable, Exient engine or in-game native menu. The launcher cannot prove which settings a running PCSX2 instance uses; manual gameplay validation remains necessary.

Synthetic Windows tests cover exact file targeting, absence refusal, legacy refusal, unrelated file preservation, backup/no-op, stale game/global snapshots, malformed settings and shared/custom folder refusal.
