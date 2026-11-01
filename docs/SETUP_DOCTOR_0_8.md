# Undercover Lite PC 0.8: Setup Doctor

This milestone adds **Setup Doctor...** to the launcher's bottom row. It is a **read-only diagnostic**, designed for troubleshooting real PCSX2 2.8.2 installations without needing to share your ISO, BIOS, screenshots of private folders, or whole configuration files.

## What it checks

- Whether the configured emulator executable exists and is named `pcsx2-qt.exe`. **It does not execute the binary or verify its version.**
- Whether the game image exists, and, for uncompressed ISO images, whether its bounded `SYSTEM.CNF` boot serial matches a known Undercover PS2 edition. Compressed images are **not inspected** and are not treated as broken.
- Whether the launcher is using its isolated emulator profile, or an external profile requiring manual path confirmation.
- Whether the selected/inferred PCSX2 **Texture Replacement > Search Directory** exists.
- Whether a potentially stale `pcsx2-data/textures` folder contains files while the selected directory is elsewhere. This helps catch the real folder mismatch discovered in the 0.5 gameplay test.
- If the disc serial is verified, the number of PNG/DDS files inside `textures/<SERIAL>/replacements`, and how many filenames match the **eleven documented Undercover UI hashes** (10 small button glyphs and the separate controller atlas).
- Whether a managed theme *record* is present. It intentionally does not claim to check all hashes' integrity or to prove PCSX2 has actually loaded them.

## What it cannot verify

The doctor does not inspect BIOS contents, launch PCSX2, read emulator runtime state, verify current controller bindings, discover whether **Load Textures** is enabled, or assess in-game quality. It cannot certify frame rate, audio or a successful boot.

The **Copy safe report** button copies the diagnostic levels, serial when verified and next steps, but **does not include any absolute local paths, game filename or user directory names**. Diagnostic checks leave settings, the game ISO and texture folders untouched. Textures are never automatically moved or deleted.

## Quick use

1. Run Undercover Lite PC (0.8 or later) and select your game and PCSX2 Qt executable.
2. Click **Setup Doctor...**.
3. Review blocking issues, attention items and notices. The game can still launch when a compressed disc image is marked unverified.
4. Compare any texture directory findings to the **Search Directory displayed in PCSX2 Graphics > Texture Replacement**.
5. Optionally use **Copy safe report** when discussing the issue on GitHub. It excludes your local paths.

The documented **Cross → W** replacement was already confirmed working in-game. The doctor can find the corresponding file in a replacement folder, but file presence isn't equivalent to in-game verification. The 256×256 controller diagram introduced in 0.7 is still experimental.

## Validation

Synthetic smoke tests check missing executables/images, valid ISO recognition, nonstandard executable names, unverified CHD, confirmed different-game ISO, PCSX2's nested texture folder, stale legacy texture directories, custom search directories, non-isolated profile warnings, nonmutating checks and the redaction of local paths. They do not use proprietary game bytes or require a BIOS.

The new functionality is a first-run and troubleshooting aid. A future milestone could make configuration truly one-click, but that would require verifying actual PCSX2 keyboard configuration fields and safe input mapping behavior rather than guessing and overwriting the player's settings.
