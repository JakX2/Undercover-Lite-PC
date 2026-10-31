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