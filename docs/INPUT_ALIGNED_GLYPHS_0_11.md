# 0.11: Input-aligned glyph correction

The earlier 0.7 controller diagram and 0.5b driving-first button sprites contain some incorrect key labels. We confirmed the mismatch directly in the source repository:

| Action | Old 0.7 controller graphic | Real UndercoverLite-DrivingFirst profile |
| --- | --- | --- |
| Speedbreaker (PS2 Square) | **B** | **X** |
| Pause (PS2 Start) | **Esc** | **F** |
| D-pad navigation | **1–4** | **Arrow keys** |
| Right-stick camera | **Arrow keys** | **I / J / K / L** |
| Name-entry Done (Start) | **Esc** in the 0.5b 32px artwork | **F** |

This is a concrete bug, independent of user configuration. **Do not rely on the old 0.7 diagram as an accurate control reference.**

The new **0.11 input-aligned** pack is generated directly from `UndercoverKeyboardProfile.cs`, the code that also generates PCSX2's `UndercoverLite-DrivingFirst.ini`. It produces ten original 32×32 keyboard glyphs and a new original 256×256 controller diagram, all under the actual captured PCSX2 texture hashes, for all three known PS2 game serials. A CI regression test forces generation to fail if the C# mapping changes without the artwork generator being updated. The Square icon is **still missing from the original texture captures**; its entry is displayed in the controller diagram but no standalone Square file is fabricated.

### Installing and testing

1. Close the game and PCSX2. Keep backups of your existing replacement textures.
2. In the Undercover Lite launcher, use PC Input Lab → **Install driving profile**. This creates the `UndercoverLite-DrivingFirst` input profile only if it doesn't already exist; it never overwrites a changed user profile.
3. In PCSX2, **manually select** `UndercoverLite-DrivingFirst` under **Undercover → Properties → Summary → Input Profile**. **Creating a profile does not activate it.**
4. Inspect PCSX2 → Settings → Hotkeys. **Space** may have an emulator pause function as well as the game's handbrake; rebind the emulator hotkey manually if necessary.
5. Open Undercover Lite → **Button glyphs / texture packs → Activate / Switch**. Select the new `packs/<YOUR GAME SERIAL>/matched-driving-first` directory from the 0.11 ZIP. If your old 0.5b sprites are managed, switching will remove/replace only those tracked sprites. If the old theme was installed before 0.6, first adopt byte-identical old textures in the 0.6 manager or manually back up/remove the conflicting files. The new manager refuses to overwrite unknown image mods.
6. Relaunch PCSX2 and inspect the menu, name-entry and controller settings screen. **The large 0.11 controller image still needs in-game verification.** Known data says only the Cross → W 32px replacement has been confirmed in game.

### What "1:1" can mean at this stage

`UndercoverLite-DrivingFirst` maps physical PC keys to **PS2 controller inputs** as indicated by the new artwork. It does not enable *context-aware* keyboard actions. The PS2 Cross button accelerates while driving and selects menu items, so **W = Accelerate and W = Accept**. Triangle means Handbrake while driving and Back in menus, so **Space = Handbrake and Space = Back**. Start is **F**, used for Pause or the name-entry Done function.

PCSX2's per-game profile **must actually be selected** for these mappings to apply. A matching glyph PNG on disk cannot prove that. If the new artwork doesn't reflect actual keys in-game, the next diagnostics are the active PCSX2 input profile, any conflicting emulator hotkeys, and the exact PS2 game control scheme.

This pack aligns the **graphics and declared input profile by construction**, not the live running game. We do not claim it has been tested with real PCSX2 UI recognition across regions.

Original player-dumped PS2 textures are never included. Source: `tools/build_synced_input_glyphs.py`; tests: `tests/check_synced_input_glyphs.py`; GitHub Actions publishes the ZIP artifact.

### Scope of labels

Known hash captures: Cross, Circle, Triangle, Start, Select, L1, L2, R1, R2, R3. The standalone Square prompt remains unknown. The controller diagram names Square's corresponding **X** key because the PS2 input action is known, not because its isolated glyph has been captured. Arrow keys are PS2 D-pad and IJKL the right analog stick under the existing `0.9` input profile.

**Do not use this 0.11 theme with `menu-reference` or a different custom PCSX2 input profile**, unless you verify the mappings and update the artwork.


### Read-only Setup Doctor check

Undercover Lite's **Setup Doctor** now checks for an intact `UndercoverLite-DrivingFirst.ini` and whether the registered managed glyph theme is the old known-mismatched controller artwork or the new `matched driving keys 0.11` theme. It reports a warning for the known old artwork, or a provisional declared-key match when the 0.11 theme and unchanged input-profile file are both present.

**This is not proof of what the live PCSX2 process is using.** The launcher cannot yet verify which profile Undercover has selected in PCSX2 or whether a texture was edited outside the theme manager. A copied safe report contains only findings, not local paths or game files.
