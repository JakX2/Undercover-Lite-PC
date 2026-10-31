# Architecture and scope

## What 0.1 actually is

`UndercoverLite.exe` is an ordinary .NET 8 Windows Forms GUI. It saves launch preferences locally and starts a **separate, user-provided PCSX2 executable** with explicit argument boundaries (`ProcessStartInfo.ArgumentList`). The game executes within PCSX2.

`UndercoverLite.Core` contains launch/settings logic so it can be tested without Windows Forms. `UndercoverLite.Core.SmokeTests` is a dependency-free console executable running path/option/serialization/validation checks. The first-run launcher opens PCSX2's native configuration UI; it cannot yet configure mappings for the user or change emulated game menu assets.

## Configuration

- Launcher data folder: `%LOCALAPPDATA%\UndercoverLitePC`.
- Launcher JSON: `%LOCALAPPDATA%\UndercoverLitePC\launcher.json`.
- If isolated settings enabled, PCSX2 `-datapath` points to `%LOCALAPPDATA%\UndercoverLitePC\pcsx2-data`.
- BIOS files must be added by the user through PCSX2. The launcher does not store BIOS contents or distribute any game data.
- Caution: `-portable` / PCSX2 portable marker can override `-datapath`. See PCSX2 docs.

## Known limitations

- No executable-level patching, overlays, hooks, mouse UI integration, automatic controller remapping, glyph replacement or in-game graphics menu yet.
- No PS2 gameplay timing or 60 FPS modifications.
- No emulator packaging; PCSX2 is installed independently.
- No automatic game title/region verification in this milestone. Selecting a readable file does **not** confirm it contains Undercover.
- Cannot perform an interactive Windows PCSX2/game smoke test in the source-only development environment; GitHub Actions will validate compilation and pure core code on Windows.

## Planned integrations

Next: guided PCSX2 setup, optional per-game settings/graphics presets without clobbering existing user configurations, keyboard mapping profiles, and an optional PC shell/overlay. Research any UI replacements as game-specific assets; the frontend is not native code in this design. Prefer stable PCSX2-supported extension mechanisms over runtime hooking.

## Reference

Official PCSX2 CLI: https://pcsx2.net/docs/advanced/cli/

## Prototype 0.2a: Disc identity and setup guidance

`Ps2DiscIdentifier` reads bounded ISO9660 directory entries from an uncompressed ISO and matches the PS2 `SYSTEM.CNF` `BOOT2` serial to a documented Undercover serial list. It never treats filenames as proof of identity, never modifies disc bytes, and does not decode CHD/CSO/ZSO/GZ. A foreign/unknown/unsupported image prompts the user to confirm before launch. The `InputGuideForm` documents manual PCSX2 controller mapping but does not mutate PCSX2 settings.

See `docs/GAME_IDENTIFICATION.md` for supported serials, threat model, and limitations.

## 0.3 session companion architecture

A `GameSessionGate` maintains single-session state, while `MainForm` retains the returned `Process` object for the duration of the emulation session. A WinForms timer polls `Process.HasExited` (no memory hooks, no process scanning, no child-process injection). Only the executable started by this launcher counts as its active session. Once that process exits, its handle is disposed and the launcher becomes ready again.

`MainForm` registers a Windows global **Ctrl+Shift+F10** hotkey only for a running session, if the user has not opted out and `RegisterHotKey` succeeds. Registration failures are nonfatal. The companion (`CompanionMenuForm`) is a regular **topmost Windows Form**, not a native in-game overlay, and may be hidden behind exclusive fullscreen PCSX2 presentation. The launcher keeps all process/game data separate from the companion. The panel can save **next-launch** preferences and request a graceful window close, but never kills the process or changes settings inside a running PS2 game.

`OnFormClosing` warns that exiting the launcher disables the global hotkey without exiting PCSX2. No screenshots, OS keyboard hooks, emulator memory modifications, or game assets are collected. Further in-game options/menu integration is blocked pending research and Windows integration testing.

## 0.4 texture glyph workflow

`GlyphPackForm` is an external WinForms dialog opened from the launcher, not a game overlay. `GlyphPackManager` in Core validates a user-authored `glyph-pack.json`, verifies the exact game serial against Undercover ISO identification, preflights hash-named PNG/DDS replacements, and copies new assets only into the launcher-controlled PCSX2 `textures/<SERIAL>/replacements` folder. Existing different assets are not overwritten; identical assets are skipped. A failure rolls back newly created files where possible. It never modifies `PCSX2.ini`, original game assets, or input mappings. A completed texture pack has not been built yet.

Automatic folder handling requires isolated PCSX2 data and a verified uncompressed ISO; portable-mode override and custom texture directory settings can make the computed folder incorrect, so this must be checked in real PCSX2 2.8.2 testing. See `docs/GLYPH_PACKS.md` for capture steps and the pack format.