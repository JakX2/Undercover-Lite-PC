# Undercover Lite PC 🏎️

**An experimental Windows launcher for _Need for Speed: Undercover_ (PS2), powered by PCSX2.**

Undercover Lite PC aims to make the original PS2 game convenient to launch and configure like a Windows title, without rewriting its game engine. It is **not** a native PC port or a game download. This repository is independent from the Undercover PS2/Wii → Carbon PC native-engine conversion research project.

> **Status: prototype 0.5 input UX foundation; the developer has confirmed real PS2 gameplay through 0.3, while the later features still need in-game validation.** The first milestone is a standalone Windows launcher. Native in-game graphics settings, keyboard-specific button glyphs, and a PC-style pause menu are **future work**, not features shipped here.

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

## Prototype 0.3: PC-style companion menu (experimental)

- **Session tracking:** The launcher keeps the returned PCSX2 process handle alive, prevents duplicate launches, and returns to a ready state when that emulator process exits. The launcher minimizes while the game runs.
- **Companion menu:** While the launcher and game session are running, press **Ctrl+Shift+F10** to show/hide a separate, topmost Windows companion panel. You can also restore the launcher from the taskbar and click **Session menu**. The hotkey can be disabled in the launcher.
- **Next-launch options:** Save fullscreen, fast-boot and PCSX2 main-window choices for the **next launch only**. The panel does **not** change game graphics or controller bindings live.
- **Graceful exit request:** Offers a close request to PCSX2's main window, where one exists; it never kills the game process. If PCSX2 does not expose a closable window, exit through PCSX2 normally.
- **Lifecycle safety:** When PCSX2 exits, the launcher stops the hotkey, closes the companion panel and restores the launch button. Closing the launcher while PCSX2 is running warns that the game will remain running and the hotkey will stop working.

**Important limitations:** This is an **external Windows form**, not an injected overlay or the PS2 pause menu. It may be obscured by exclusive fullscreen; use PCSX2 windowed/borderless mode if needed. It cannot configure PCSX2's live renderer, audio or keyboard controls and does not pause the PS2 game. See [Companion menu technical notes](docs/COMPANION_MENU.md).

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
6. Click **Launch Undercover**. While PCSX2 is running, press **Ctrl+Shift+F10** to open the optional external companion panel, or restore the launcher and click **Session menu**. The emulator still handles game execution and rendering.

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
| 0.3: Session companion menu (external Windows panel) | Experimental source implemented; in-game integration still research |
| 0.4: Keyboard glyph texture-pack workflow and safe importer | Foundation implemented; first real sprites pending |
| 0.5: PC prompt-label suggestions and name-entry grid planning | Foundation implemented; no emulator keyboard injection |
| 0.6: Real in-game keyboard prompts and input themes | Awaiting texture dumps and in-game testing |
| 0.7: Direct keyboard name entry / native-feeling text input | Research required |
| 0.8: Graphics/performance and widescreen presets | Testing required |
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

## Prototype 0.4: PC input glyph texture packs (foundation)

- **Button glyphs / texture packs** link opens an opt-in setup and installation window from the launcher. It describes capturing Undercover UI textures in PCSX2 2.8.2 and locates the per-serial dumps and replacements directories for verified ISO images using isolated PCSX2 data.
- **Safe user-provided pack import:** `glyph-pack.json` + `replacements/` with hash-named PNG/DDS images. Checks region serial, previews new/existing/conflicting textures, requests confirmation, skips already-identical files and **never overwrites differing existing replacements**. It does not edit game files or PCSX2's graphics settings. Automatic import is disabled for compressed images/unverified game IDs and non-isolated PCSX2 configurations.
- **Blank keyboard pack template** in `glyph-packs/templates/keyboard-qwerty-us/`. No original UI textures or completed keyboard icon pack are bundled or claimed to work yet. Replacing image sprites is separate from configuring key bindings.
- See the [glyph dumping and authoring guide](docs/GLYPH_PACKS.md) for capturing the actual PS2 prompt atlases. The user must enable **Load Textures** in PCSX2 before the replacement images appear in-game.

**Version 0.4 is a technical foundation, not a verified visual mod or dynamic keyboard/gamepad UI switcher.**

## Prototype 0.5: PC input and name-entry planning

- **PC input / name-entry lab...** in the launcher edits ten proposed PC key labels (six shown directly in the supplied name-entry screenshot plus D-pad navigation). Labels are saved in launcher JSON and checked for duplicate assignments. **This does not rebind PCSX2 or edit Undercover.**
- Includes **original vector SVG keycaps** for Enter, Esc, Backspace, F, Q and E in `assets/pc-keycaps/`. These are design sources without any PCSX2 replacement hashes. Actual in-game replacements still need Undercover's texture dump captures.
- The **name-entry grid planner** previews possible D-pad moves from a presumed `A` starting cell. It does not send input and its navigation assumptions are unverified.
- The screenshot of actual 0.3 gameplay is now documented in [PC input experience](docs/PC_INPUT_EXPERIENCE.md), with a staged plan for real glyph replacements, controller themes, and eventual direct text typing.

**0.5 does not yet provide an integrated PS2 menu, automatic keyboard profile or actual direct typing.**
## Texture capture: real Undercover PS2 button sprites (0.5b research)

A private 13-image PCSX2 texture dump was identified: ten **individual 32×32 prompt icons** (Cross, Circle, Triangle, Start, Select, L1, L2, R1, R2, R3), one **256×256 controller graphic**, and two graphics requiring more research. The standalone Square icon remains missing. The original game art is **not** committed.

Original experimental 32×32 PC keycap replacement art and two candidate profiles were prepared as a **private proof of concept**, not a validated in-game mod. Region-specific hash compatibility, real controller bindings and dynamic menu-vs-driving prompts remain unverified. See [verified texture capture inventory](docs/VERIFIED_TEXTURE_CAPTURE_2026-10-31.md) for exact hashed filenames and testing scope.


## Prototype 0.6: Safe switching of working keyboard glyph themes

After the developer confirmed **Accept [W]** appeared inside Undercover on PCSX2 2.8.2, the next milestone adds **Activate / Switch** and **Disable managed glyphs** to the launcher. Undercover Lite now keeps SHA-256 ownership records for textures it installs, allows switching between the experimental menu-reference and driving-first packs, and blocks changes to unrelated or externally modified texture files. Existing icons installed by older Undercover Lite versions can be adopted **only when byte-identical and after explicit confirmation**. The game must be closed during changes; this is not real-time context switching.

Read the [managed theme switching guide](docs/THEME_SWITCHING.md) before trying it with an existing theme. Gamepad bindings, menu-vs-driving prompt contexts, and the large controller diagram are still separate development tasks.


## Prototype 0.7: PC keyboard diagram for the Controls screen (experimental)

The developer's full 97-image PCSX2 dump contains a separate **256×256 controller image** with original hash filename `b99d9ef8125d8894-f3379f9602408f67-00002213.png`. This is not replaced by earlier 32×32 keyboard icons. An **original transparent keyboard schematic** has now been generated under the exact hash to test replacement in **Options → Controls**. It illustrates WASD driving actions, Q/E gear shifts, Shift nitrous, and other proposed keys. This does **not** set controller bindings or change game-engine text.

The 0.7 user-facing proof pack combines the existing ten driving-first keyboard sprites with the new controller atlas (11 PNGs per game serial). The repo includes a reproducible game-data-free Python/Pillow generator, CI validations and an atlas-only Actions artifact. For complete packs, run the generator with your previously generated 0.5b keyboard-keycap proof ZIP using `--source-poc`. Source game texture images are never committed.

**Only the individual Cross → W glyph has so far been confirmed working in-game; the large controller atlas still needs validation.** Consult [the 0.7 controller-diagram guide](docs/CONTROLLER_DIAGRAM_0_7.md), and keep a backup of your existing textures before trying new themes.


## Prototype 0.8: Read-only Setup Doctor

A **Setup Doctor...** link in the Windows launcher now checks PCSX2 executable selection, verified Undercover ISO serials, isolated profile choices, texture search folder existence, the old-versus-nested texture directory bug, and the presence of our 11 known prompt/controller hashes. It also reports managed glyph state without implying that textures are actually loaded in-game.

The doctor **never launches PCSX2 or modifies your configuration, game files or textures**. Its **Copy safe report** action excludes local paths and filenames, so troubleshooting information can be shared more safely. BIOS readiness, live controller bindings and texture-loading settings remain unverified and must be tested in PCSX2. See [Setup Doctor 0.8 guide](docs/SETUP_DOCTOR_0_8.md).



## Prototype 0.9: Real keyboard driving profile (opt-in)

The launcher now creates a standalone, game-specific PCSX2 Qt **UndercoverLite-DrivingFirst** input profile from **PC input / name-entry lab**. This is the first implemented **button-to-key configuration**, not just key-label artwork. The profile matches the confirmed Cross → W replacement and the game's PS2 driving scheme: W accelerate, S brake, A/D steering, Space handbrake, Shift nitrous, Q/E gear changes, arrow keys D-pad navigation, and F Start/Pause.

The install is **opt-in, isolated to the Undercover Lite PCSX2 data folder and never overwrites differing existing profiles**. You must still assign it to the game under **PCSX2 > Undercover Properties > Summary > Input Profile**; Undercover Lite does not edit shared controls or force the game to use a profile. Key behavior is the same in menus and driving, so W = Accept and Space = Back are unavoidable with this static PS2 button mapping. The result has passed source-level tests only until we receive an actual in-game control test. See [0.9 input-profile guide](docs/KEYBOARD_PROFILE_0_9.md).



## Prototype 0.10: Steering and menu key controls

The **PC Input Lab** now has a **Steering presets...** screen with three distinct PCSX2 input profiles: Gentle (65%), Balanced (85%), and Full (100%) keyboard steering magnitude. An optional menu-friendly variant adds Enter as another Cross/Accept binding and Backspace as another Triangle/Back binding, without removing W/Space. These are still shared PS2 inputs, so the added keys also have in-game driving effects.

**Important discovery from the official PCSX2 2.8.2 source:** the default emulator **Space hotkey toggles pause**, potentially conflicting with our Space handbrake. The Steering Lab includes a read-only hotkey check and guides you to change that emulator hotkey manually if necessary. The tool never silently edits shared PCSX2 hotkeys or applies profiles to a game. Use Undercover Game Properties > Summary > Input Profile to select one. See [Steering and Menu 0.10 guide](docs/STEERING_AND_MENU_0_10.md).



## 0.11: Correct the old controller key labels

**The developer reported the on-screen keys still didn't match the keys to press.** We found concrete mismatches: the previous 0.7 controller art said **B = Speedbreaker, Esc = Pause, 1–4 = D-pad and arrows = right stick**, whereas the generated 0.9/0.10 input profiles use **X, F, arrows and IJKL**, respectively.

A new original 0.11 sprite generator reads the **same C# controller mapping source as the PCSX2 input profile**, producing all ten captured 32px PS2 icons and a corrected 256px diagram. The GitHub Actions downloadable ZIP includes 11 original replacement PNGs per region, with automated drift detection. It does **not** automatically activate any profile or make PS2 buttons context-aware, and still needs gameplay testing. See [0.11 artwork/input alignment](docs/INPUT_ALIGNED_GLYPHS_0_11.md).



The updated **Setup Doctor** also flags the known 0.7 false labels and reports whether `UndercoverLite-DrivingFirst.ini` agrees with a registered 0.11 theme. **It does not inspect the active PCSX2 profile selection**, so you must still select the correct profile manually.


## Proposed unified PC Settings screen

A complete [six-tab PC Settings UI blueprint](docs/PC_SETTINGS_UI_BLUEPRINT.md) defines the exact Graphics, Controls, Audio, Interface, Advanced and Troubleshooting options, control types, helper text, backend readiness, safety rules and staged implementation plan. This is **a design specification only**, not a built in-game menu or newly enabled graphics/audio controls. The design separates existing Undercover Lite workflows from PCSX2 integration work and game-specific reverse engineering.


## Prototype 0.12: Unified PC Settings window (UI-1)

The Windows launcher now has a **PC Settings** button opening a real six-tab dark menu: Graphics, Controls, Audio, Interface, Advanced and Troubleshooting. It implements only settings that already have a functional backend, including fullscreen/windowed startup, fast boot, hiding the main emulator window, isolated PCSX2 data, companion hotkey, and executable/game/texture paths. Other options are clearly disabled or read-only until a tested PCSX2 adapter or a game-specific patch exists. Each page can open the relevant real input, glyph and diagnostic tools.

Changes are merged into the most recently saved configuration **only for fields you edited**, preserving glyph paths and PC Input Lab preferences saved independently. This is an external launcher menu, **not an injected PS2 game menu**, and it does not silently rewrite PCSX2 graphics/audio settings. See [PC Settings UI-1 guide](docs/PC_SETTINGS_UI_1.md) and [full design blueprint](docs/PC_SETTINGS_UI_BLUEPRINT.md).


## Prototype 0.13: working PCSX2 internal resolution (UI-2)

The six-tab PC Settings window now supports a **real internal-resolution selector** (Native 1×, 2×, 3×, 4×, 6×). It edits only the existing **isolated** PCSX2 2.8.2 setting **[EmuCore/GS] upscale_multiplier**, with an original-file backup, input validation and concurrent-edit checks. The Graphics tab has its own **Apply scale...** confirmation, separate from saving launcher preferences.

**Important:** this edits the isolated emulator's **global** scale setting. A game-specific Undercover PCSX2 configuration based on the disc ELF CRC can override it. No game-specific overrides, original PS2 assets, BIOS, saves or other graphic settings are modified. Read [UI-2 graphics scale guide](docs/GRAPHICS_SCALE_UI_2.md).
