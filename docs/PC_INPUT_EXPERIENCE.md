# Undercover Lite PC: keyboard prompts and name-entry UX

## Evidence and status (October 31, 2026)

The developer supplied a **real Undercover Lite PC 0.3 gameplay screenshot** showing the PS2 "ENTER NAME" screen. This is evidence that a 0.3 launcher session reached the game on their PC, **not** proof that 0.4/0.5 glyph replacements or text entry work.

Visible screenshot actions:

| Original on-screen prompt | Proposed PC glyph | Original PS2 input |
| --- | --- | --- |
| Accept / blue Cross | Enter | Cross |
| Back / green Triangle | Esc | Triangle |
| Delete / pink Square | Backspace | Square |
| Done / START | F | Start |
| Previous/next tab | Q / E | L1 / R1 |

**Important:** `Cross = Enter` and `Start = Enter` at the same time do not work as a clean one-to-one PCSX2 profile. The screenshot's `Done` action therefore uses `F` as a *suggested* distinct keyboard binding. The launcher has a **PC input / name-entry lab** with editable suggestions. Changing them does **not** rebind PCSX2 or change the game textures.

## Proposed PC input rollout

1. **0.5: PC prompt-label lab / original vector keycaps (source complete).** Edit the six screenshot action labels plus D-pad, store key suggestions in launcher JSON, reject duplicate assignments. The repo includes original SVG keycap sources. No game textures are distributed.
2. **Atlas mapping:** Capture screenshots and hashed texture dumps from 2.8.2 while showing main menu, name entry, pause, race HUD. Identify which specific atlas cells correspond to the buttons, including palette/animated variants. Screenshots alone are insufficient to build correct hash-named replacements.
3. **0.6: Region-aware real glyph packs.** Compose original keyboard icon artwork onto user-supplied atlas dumps, preserving exact dimensions/alpha/hash filenames. Test US/PAL/JP separately, and only enable a pack when PCSX2 bindings match its labeled keys.
4. **Controller themes:** Optional Xbox and PlayStation glyph sets. Dynamic switching would require a proven safe texture reload mechanism; otherwise use next-launch switches.
5. **Direct keyboard name typing:** Investigate the emulated game text-input state, PCSX2-supported input paths, and a safe way to bridge arbitrary PC text into the game's name buffer. Direct input must not write unknown game memory or blindly inject keys into other windows. A full solution requires a repeatable verified in-game test before enabling any automation.

## Name-entry grid planner

The screenshot shows 13 columns of A-M on row 1, N-Z on row 2, and 1-0 on row 3. The **PC input / name-entry lab** offers an experimental *read-only* navigation plan. It assumes the cursor starts on `A`, D-pad moves by one cell and `Cross` selects. Example: entering `N` suggests `Down, Cross, Start`.

There is **no automatic typing or key injection**. We do not know the initial cursor cell, navigation wrap behavior, acceptance delays, name length limit or semantics of the empty tiles on row 3. The planner rejects spaces and symbols rather than guessing they work. Its goal is to make the next implementation/test phase concrete.

## Needed files for the first real in-game prompt change

- A small set of texture dumps from `textures/<SERIAL>/dumps` while the name-entry screen is visible (ideally same screen as screenshot).
- A screenshot identifying the visual context of each dump.
- The serial reported by `Check image`. **No BIOS or game ISO needed.**

After identifying actual sprite textures, use the existing 0.4 **Button glyphs / texture packs** installer to test new art safely. The original SVG keycaps can be a style source but are not themselves installable replacements.