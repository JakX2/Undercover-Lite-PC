# Undercover Lite PC 0.13 | UI-2: real internal rendering resolution

This milestone implements the first **real PCSX2 2.8.2 graphics setting** in the six-tab PC Settings window. It changes **only** the isolated emulator's global **[EmuCore/GS] upscale_multiplier** float in its existing **PCSX2.ini**; every other PCSX2 setting is preserved. A full copy of the original INI is saved before any replacement.

## Verified from PCSX2 2.8.2 source

- **pcsx2/Pcsx2Config.cpp** loads the GSOptions property UpscaleMultiplier from the **upscale_multiplier** INI setting.
- **pcsx2-qt/Settings/GraphicsSettingsWidget.cpp** uses section **EmuCore/GS**, float key **upscale_multiplier**, default **1.0**. UI includes multipliers 1×, 2×, 3×, 4×, and 6×.
- **pcsx2-qt/QtHost.cpp** loads **EmuFolders::Settings/PCSX2.ini**.
- **pcsx2/VMManager.cpp** loads **per-game** override INI files by game serial and **ELF CRC**. A per-game override of the same GS value can supersede the global value.
- The observed isolated PCSX2 -datapath layout is **pcsx2-data/PCSX2/inis/PCSX2.ini**. Older/custom installations may use **pcsx2-data/inis/PCSX2.ini**. The adapter detects existing files in both locations and **refuses** to guess if both exist.

Primary sources:
- https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/Pcsx2Config.cpp
- https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2-qt/Settings/GraphicsSettingsWidget.cpp
- https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/VMManager.cpp

## Using the graphics control

1. Complete PCSX2's first-time setup so **PCSX2.ini** exists. The launcher never creates a fake initial emulator config.
2. Exit Undercover and **close every PCSX2 window**; do not edit in parallel.
3. Open Undercover Lite PC **PC Settings → Graphics → Rendering → Internal resolution**.
4. Select **Native (1×), 2×, 3×, 4× or 6×**.
5. Click **Apply scale...**, review the warning and approve. This action is **separate** from the bottom **Apply changes** button, which saves launcher preferences only.
6. The original PCSX2.ini is preserved next to it in a timestamped file ending **.undercoverlite-...bak**. Only the GS multiplier is edited. Restart PCSX2 and check graphical quality.
7. If the result does not change, inspect **Undercover → PCSX2 Game Properties → Graphics**. A per-game setting based on the disc's ELF CRC, or a different emulator profile, can take precedence. The adapter does not silently modify game-specific overrides.

This is a global preference **within our isolated Undercover Lite emulator profile**. It is not a native Exient game option, screen-resolution change, or FPS unlock.

## Guardrails

- Refuses non-isolated PCSX2 data and does not create PCSX2.ini.
- Refuses ambiguous direct/nested INI paths, symlink/reparse config paths, unsupported encodings, huge or malformed INIs, duplicate GS sections/keys or invalid values.
- Stores a SHA-256 snapshot. If the configuration changes before Apply, **no edit is attempted**.
- Makes a complete original-file backup before writing. Uses a temporary file and same-directory replacement. Backups are never automatically deleted.
- Preserves unrelated sections, comments and newline style. Updates only the GS multiplier or adds the missing key/section.
- Restricts scales to documented 1×, 2×, 3×, 4×, 6×.
- Disables editor if the Undercover game session is running. Other PCSX2 processes must be closed manually.
- Does not change per-game CRC overrides, other renderer options, the game ISO, BIOS or texture mods.

## Verification

Synthetic smoke tests cover missing emulator setup, exact setting read/write, backups, unrelated INI lines, CRLF, no-op values, stale edits, unsupported multipliers, shared-profile refusal, missing sections, UTF-8 BOM, duplicate sections/keys and ambiguous folders.

Automated tests do not prove the actual running game uses the global scale or that higher scales preserve correct rendering. Both need real PCSX2 testing.
