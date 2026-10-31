# Undercover Lite PC 0.4: glyph replacement research and workflow

Status: **workflow and installer implemented; zero actual Undercover UI sprites identified or shipped**. This project does not include copyrighted game art, dumps, firmware, BIOS, input hacks or a modified PCSX2 binary.

## What is supported

PCSX2 v2.8.2 has texture dumping and replacement under **Settings > Graphics > Texture Replacement** (the exact grouping can vary by version). Enable **Dump Textures** temporarily, visit menus and gameplay scenes, then turn dumping OFF. PCSX2 writes discovered textures with hashed names to a game-specific folder, conventionally:

```
<PCSX2 data>/textures/<SERIAL>/dumps/
<PCSX2 data>/textures/<SERIAL>/replacements/
```

The renderer's lookup uses the PS2 serial, not the original ISO filename. An Undercover Lite setup using isolated PCSX2 data and a recognized US ISO uses:

```
%LOCALAPPDATA%/UndercoverLitePC/pcsx2-data/textures/SLUS-21801/dumps/
%LOCALAPPDATA%/UndercoverLitePC/pcsx2-data/textures/SLUS-21801/replacements/
```

Do not guess the location if `IsolatePcsx2Data` is disabled, if PCSX2 was started with `-portable`, or if the PCSX2 texture directory has been moved in its settings. Use **Tools > Open Data Directory** in PCSX2 and examine its Graphics texture replacement settings instead. The launcher deliberately does not touch those external configurations.

The official PCSX2 code uses separate `dumps` and `replacements` directories, and recognizes hash-based filenames. Documentation: https://github.com/PCSX2/pcsx2/blob/master/pcsx2/GS/Renderers/HW/GSTextureReplacements.cpp

## Repeatable capture checklist

1. Select a verified Undercover PS2 ISO in the launcher and configure PCSX2 2.8.2 independently (with your own BIOS).
2. Enable **Dump Textures** in PCSX2 graphics settings. Keep **Load Textures** disabled for the initial inspection to reduce confusion.
3. Visit *main menu*: highlight Play/Options and the prompt bar. Record the screen context.
4. Visit *pause menu*: select categories, confirm, cancel, switch tabs. Capture screenshots to map each hash to its on-screen use.
5. Visit *garage and gameplay tutorials*: trigger contextual control prompts and steering/action symbols. Note which buttons appear in which states.
6. Close the game normally; **disable Dump Textures**. Copy dumps into a **private working directory** for inspection; many files are unrelated to icons.
7. Locate 2D sprite sheets containing PS2 symbols. Preserve UV regions, canvas dimensions, alpha and the original hashed filename. Some texture hashes vary with palettes, animations, regions or emulator behavior; inspect rather than assume a one-to-one icon sheet.
8. Create *user-authored replacement* PNGs/DDS files and test in PCSX2 by enabling **Load Textures**. Keep menu glyphs aligned with your actual keyboard mappings.

NOTE: Texture dumping is a renderer-level operation. There is no guarantee every prompt is a simple atlas or that a dump will capture all UI states. Animated or palette-swapped elements may require several variants, and replacements can occasionally affect unrelated UI elements sharing a texture.

## Pack format (0.4)

The source repository provides **metadata-only templates** under `glyph-packs/templates/`. Each usable user-created pack has the following structure:

```
my-keyboard-pack/
├── glyph-pack.json
└── replacements/
    ├── <original-PCSX2-hash>.png
    └── <another-original-PCSX2-hash>.png
```

Example manifest:

```json
{
  "schemaVersion": 1,
  "id": "keyboard-qwerty-us",
  "name": "Undercover keyboard QWERTY (US)",
  "gameSerial": "SLUS-21801",
  "inputProfile": "keyboard-qwerty",
  "description": "UI prompts for a specifically configured keyboard mapping"
}
```

`gameSerial` must match the recognized ISO serial, not just its region family. The first installer accepts only flattened `replacements/` directories and conventional PCSX2 hash-named `.png`/`.dds` files; the original filenames must be retained. Metadata does **not** configure keyboard input. The pack does not work until it contains actual replacement images.

## Safe installation in the launcher

1. Before launching a game, click **Button glyphs / texture packs...** in Undercover Lite 0.4.
2. For automatic installation, keep **Separate PCSX2 settings and saves** enabled and select a recognizable **uncompressed ISO**. Compressed CHD/CSO/ZSO game images remain launchable but cannot yet be inspected for a region-safe automated import.
3. Click **Install a glyph pack...**, choose your authored pack directory and review its filename count, serial and destination. Confirm before any copy.
4. Installation never overwrites an existing **different** replacement image. Identical files are skipped. If a conflict exists, resolve it manually; do not destroy someone else's texture pack. The importer does not modify PCSX2 graphics flags or original game files.
5. Open PCSX2 configuration, enable **Load Textures**, and restart emulation. Verify the display and bindings match.

No runtime glyph theme switching or PS2 UI hooks are included. The companion menu is an external Windows window. A dynamic keyboard/gamepad theme selector would require separate input detection and safe texture reload behavior, which is not implemented in 0.4.

## Suggested next research handoff

If sharing captures for private development, send **a small ZIP of the suspected prompt texture hashes and corresponding screenshots showing where they appear**, plus the Undercover ISO's game serial. Avoid uploading a BIOS or game image. We can catalog the identified prompt atlases and design region-specific key glyphs without committing the original dumped sprites to the public repository.