# Undercover Lite PC 0.7: keyboard controller diagram

Status: **original artwork generated and verified structurally; NOT tested inside the PS2 game yet**.

## Why this exists

The developer already tested an original 32×32 button texture in a live PCSX2 2.8.2 session and confirmed **Accept [W]** displayed on the main menu. A second private 97-PNG dump revealed the full PS2 controller illustration is itself a separate **256×256** texture with this original PCSX2 filename:

`b99d9ef8125d8894-f3379f9602408f67-00002213.png`

This isn't one of the ten 32×32 glyph images in the earlier proof-of-concept. Replacing the individual Cross/Circle/etc. icons alone cannot alter the large controller art in **Options → Controls**.

## New deliverable

`tools/build_keyboard_controller_atlas.py` draws an **original, transparent 256×256 PC keyboard graphic**, including the layout suggestions below, and writes PCSX2-hash-named packs for the three documented Undercover PS2 serials. All bitmap pixels are generated, not extracted from the original controller sprite. The generator caps alpha to 128, matching the original dump's channel range, but that does not guarantee correct in-game blending.

The original user-supplied sprite images aren't committed to the public repository. For the user-facing **complete** experimental test pack, the developer also received a private ZIP containing **11 images per serial**: 10 original keyboard keycaps from the preceding `driving-first` theme plus one new controller diagram. Only the Cross [W] keycap has already been validated in the game. The diagram itself and other regional hashes remain unverified.

## Proposed PS2-to-PC driving reference

| Game action | Original PS2 control | Suggested PC key |
| --- | --- | --- |
| Accelerate | Cross | W |
| Brake / Reverse | Circle | S |
| Steer | Left analog stick | A / D |
| Handbrake | Triangle / L2 | Space / C |
| Speedbreaker | Square | B (standalone Square glyph not yet captured) |
| Nitrous | R2 | Shift |
| Shift down / up | L1 / R1 | Q / E |
| Change view | R3 | V |
| Reset car | Select | R |
| Pause | Start | Esc |
| World map, SMS, events, pursuit | D-pad | 1–4 (research proposal) |
| Freer camera | Right analog stick | Arrow keys (research proposal) |

**These are suggested labels, NOT functional bindings.** Nothing in the generator edits PCSX2 controller profiles. For example, W mapped to Cross also makes W the Accept key in menus. A real PC-like distinction between **Enter to Accept** and **W to Accelerate** requires context-aware input routing or verified game patches. The 0.6 theme switcher is deliberately only between game sessions.

## Build original art yourself

Install Python 3.11+ and Pillow (for example `python -m pip install -r tools/requirements.txt`) and run:

```shell
python tools/build_keyboard_controller_atlas.py --out build/keyboard-diagram
```

This generates **atlas-only** replacement packs (one image per serial). If you activate such a pack in the 0.6 theme manager, any previously managed glyphs absent from it will be removed. To generate a **composite pack**, supply the private original-keycap proof ZIP:

```shell
python tools/build_keyboard_controller_atlas.py \
  --out build/keyboard-diagram-complete \
  --source-poc /path/to/Undercover_Lite_PC_Input_Glyph_Proof_0_5b.zip
```

That generates 11 textures per serial and preserves the original `driving-first` individual icons during an upgrade.

## How to test in Undercover Lite 0.6+

1. Back up your existing PCSX2 `replacements` folder. Fully close Undercover and PCSX2.
2. If you have already installed the `driving-first` 0.5b pack with the older launcher, **Activate / Switch** to the same 0.5b pack and explicitly adopt the byte-identical files when prompted. Any modified/non-matching files block adoption, by design.
3. **Activate / Switch** to the **complete** `driving-first-plus-controller` pack for your *actual* verified game serial.
4. Confirm the selected Search Directory exactly matches **PCSX2 → Settings → Graphics → Texture Replacement**, and that **Load Textures** is enabled.
5. Launch Undercover and visit **Options → Controls**. Take a screenshot to see whether the original PS2 gamepad image is replaced with the keyboard schematic. Check its alignment with existing lines and on-screen labels.
6. To revert, switch back to the earlier 10-image `driving-first` pack, or **Disable managed glyphs** for the original PS2 prompts.

**Important limitations:** This is a renderer texture replacement. The original on-screen controls labels, line anchors and some PS2-specific prompts are rendered outside the 256×256 image. They remain unchanged. The 256×256 atlas may be scaled or blended differently in game. Do not claim the diagram is fully integrated before a real screenshot confirms it.

## Next research targets

Capture a standalone **Square** glyph while pressing Delete/Speedbreaker; validate the large diagram; inventory UI font/string assets for more native-looking control names; identify a safe input profile that doesn't sacrifice PC menu usability.