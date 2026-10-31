# Undercover Lite PC 🏎️

**An experimental Windows launcher for _Need for Speed: Undercover_ (PS2), powered by PCSX2.**

Undercover Lite PC aims to make the original PS2 game convenient to launch and configure like a Windows title, without rewriting its game engine. It is **not** a native PC port or a game download. This repository is independent from the Undercover PS2/Wii → Carbon PC native-engine conversion research project.

> **Status: prototype 0.2 source code, not yet gameplay-validated.** The first milestone is a standalone Windows launcher. Native in-game graphics settings, keyboard-specific button glyphs, and a PC-style pause menu are **future work**, not features shipped here.

## What works in the first prototype

- Windows desktop launcher with paths to your installed **PCSX2 Qt** executable and **your own PS2 disc image**.
- Open PCSX2 setup from the launcher to set up your BIOS, graphics settings and keyboard/controller bindings.
- Choose fullscreen, fast boot, hide PCSX2's main UI, and isolate PCSX2 application data for this project.
- Boot the chosen game through PCSX2 with the modern documented `-datapath`, `-nogui`/`-batch`, `-fullscreen`/`-nofullscreen`, `-fastboot`/`-slowboot`, and `-- <disc>` flags.
- Save launcher preferences in `%LOCALAPPDATA%\UndercoverLitePC\launcher.json` (not in the Git repository).
- No special game binaries, BIOS, saves, copyrighted art or emulator releases are included.

## Prototype 0.2 additions

- **Check image:** Inspects an uncompressed ISO's `SYSTEM.CNF` / `BOOT2` record and reports the identified PS2 game serial without relying on the filename. Supports known US, PAL and Japanese Undercover PS2 serials. It warns, but lets you confirm and continue, when a different game, an unknown ISO or a compressed image is selected.
- **First-run input guide:** Click the keyboard/controller setup link in the launcher to see the PCSX2 setup steps and suggested keyboard layout. This is guidance only; **it does not modify the PCSX2 controller profile**.
- Bounded, read-only ISO9660 directory parsing and synthetic disc-image smoke tests. See [identification details](docs/GAME_IDENTIFICATION.md).

## Verified PCSX2 v2.8.2 package layout

The official public `pcsx2-v2.8.2-windows-x64-Qt.7z` archive has been inspected directly. It contains `pcsx2-qt.exe` at the archive root, Qt DLLs at the same level, `QtPlugins/`, `resources/`, and `translations/`. The archive does not include a game image or a PS2 BIOS. This is a **file-layout check**, not a Windows gameplay test.

You can extract PCSX2 to a separate directory beside the launcher to auto-select it on first run:

```text
UndercoverLite.exe
pcsx2-v2.8.2-windows-x64-Qt/
  pcsx2-qt.exe
  QtPlugins/
  resources/
  translations/
  ... other official files ...
```

The launcher also looks for `pcsx2-qt.exe` directly next to it, or under `PCSX2/`, `pcsx2/`, or `emulator/`. Auto-detection only selects a path; it **does not** verify emulator version, configure your BIOS or bundle PCSX2. An explicit saved path always wins. You can still select PCSX2 manually using **Browse**.

Official release: https://github.com/PCSX2/pcsx2/releases/tag/v2.8.2

## Windows setup (prototype)

1. Install [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or use the self-contained build produced by the CI workflow.
2. Download and configure a current [PCSX2](https://pcsx2.net/) Qt release independently. **You must supply your own legally obtained BIOS and game image.**
3. Download/build `UndercoverLite.exe` and run it.
4. Use **Browse** to select your `pcsx2-qt.exe` and your Undercover PS2 `.iso` (or compatible disc image). Click **Check image** to identify an uncompressed ISO.
5. Click **Configure PCSX2** to complete the first-run BIOS wizard and keyboard/controller setup. The launcher provides a first-run input guide link. If **Separate PCSX2 settings and saves** is checked, this instance uses its own PCSX2 configuration and memory cards under `%LOCALAPPDATA%\UndercoverLitePC\pcsx2-data`.
6. Click **Launch Undercover**. The emulator handles game execution and rendering.

**Important:** `-datapath` does not override PCSX2's own `-portable` mode; if you have `portable.ini`/`portable.txt` beside PCSX2, that emulator's portable configuration takes precedence. Use a normal installed PCSX2 binary if you want the isolated datapath behavior.

## Build from source

Prerequisites: Windows 10/11, .NET 8 SDK, Visual Studio 2022 or `dotnet` CLI.

```powershell
dotnet build src/UndercoverLite/UndercoverLite.csproj -c Release
dotnet run --project tests/UndercoverLite.Core.SmokeTests/UndercoverLite.Core.SmokeTests.csproj -c Release
dotnet publish src/UndercoverLite/UndercoverLite.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The **Build** workflow compiles the Windows launcher and runs the core smoke checks on GitHub-hosted Windows runners. The published artifact contains only this launcher, **not** PCSX2, BIOS or any game data. PCSX2 itself has separate license and distribution requirements.

## Roadmap

| Milestone | Status |
|---|---|
| 0.1: Windows launcher, isolated config, original game launching | Initial source prototype |
| 0.2a: ISO identification and guided keyboard setup | Source implementation; Windows tests pending |
| 0.2b: Automatic PCSX2 input profiles and complete first-run wizard | Planned |
| 0.3: In-game PC options overlay | Research required |
| 0.4: Keyboard glyph replacements in the PS2 UI | Research required |
| 0.5: Graphics/performance and widescreen presets | Testing required |
| 1.0: Polished, portable PC-like experience | Long-term |

Not every graphics change can be safely applied while PS2 emulation is running. Some options will require restarting the emulator. Keyboard input already works through PCSX2's mapping; displaying **keyboard glyphs inside the emulated game** is a separate texture/patch project.

## Project layout

```text
src/UndercoverLite/          Windows Forms launcher
src/UndercoverLite.Core/     Settings and PCSX2 process integration
tests/UndercoverLite.Core.SmokeTests/  Dependency-free smoke checks
docs/                        Design decisions and planned features
.github/workflows/           Windows build, smoke tests and artifact
```

## Development boundaries

This repository is code for an independent launcher, not a source for the game or BIOS. Do not commit or bundle ISO/CHD images, BIOS ROMs, proprietary game textures or bundled third-party emulator binaries. The launcher is MIT-licensed; it does not relicense PCSX2. When distributing modified PCSX2 binaries, comply with PCSX2's GPL terms.

PCSX2 command-line reference: [PCSX2 official CLI documentation](https://pcsx2.net/docs/advanced/cli/).