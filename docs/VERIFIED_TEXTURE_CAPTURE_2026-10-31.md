# Verified PS2 UI sprite capture: 31 October 2026

The developer supplied a private PCSX2 v2.8.2 dump containing **13 hash-named PNG textures** from *Need for Speed: Undercover (PS2)* and a gameplay screenshot of the in-game controller setup. Original game images are **not stored in this repository**.

## Inventory

Twelve textures are 32×32 RGBA, and one controller graphic is 256×256 RGBA. The original textures have alpha maximum 128.

| PS2 icon / texture | PCSX2 texture hash filename | Status |
| --- | --- | --- |
| Cross | `fa7c4c608af78f38-99e1a7bd2d4d93c5-00001554.png` | Identified |
| Circle | `3ef5ed33ce7400e6-5d5080dd80f7a56d-00001554.png` | Identified |
| Triangle | `3932bc820cff2295-7e347471dfd31f91-00001554.png` | Identified |
| Start | `6a9f9e4d69bb02d2-be3e24ef4b371b8d-00001554.png` | Identified |
| Select | `565c987dcc9942d1-be3e24ef4b371b8d-00001554.png` | Identified |
| L1 | `d2faf9a272a7cd2d-3959933ff5fb8b26-00001554.png` | Identified |
| L2 | `b504e4faa31b1c73-d78b51572ac25e65-00001554.png` | Identified |
| R1 | `5e8b3773662cfffd-15ab2dbca5c4e797-00001554.png` | Identified |
| R2 | `ae0846b61eea3645-9c05bbf6e9ad7089-00001554.png` | Identified |
| R3 | `99752cf8c7f203b4-37a640d9a823710-00001554.png` | Identified |
| UI direction arrow | `69ea8c2fb43a2498-dcb9d50e534b2cef-00001554.png` | Unchanged pending context |
| Small circular UI control | `d9ea94303472d598-e0b71404565ba08e-00001554.png` | Unresolved |
| DualShock/controller screen art | `b99d9ef8125d8894-f3379f9602408f67-00002213.png` | Identified, **256×256**; unchanged |

A separate **Square** glyph was **not** included in the dump. The Square in the controller graphic does not prove there is a separate 32×32 Square replacement. Further game states should be captured to identify it.

## Correct input-context caveat

On the PS2 controls screenshot: **Cross = Accelerate**, **Circle = Brake/Reverse**, **Triangle = Handbrake**, **L1 = Shift Down**, **R1 = Shift Up**, **L2 = alternative Handbrake**, **R2 = Nitrous**, **R3 = Change View**, **Select = Reset Car**, and **Start = Pause**. Square = Speedbreaker in the screenshot, but the standalone glyph is missing.

The same buttons do other things in the menu: Cross = Accept, Triangle = Back, Start = Done, Square = Delete. Therefore replacing Cross with "ENTER" **also shows ENTER for Accelerate** during driving, and replacing Start with "F" **also shows F for Pause**. A single static glyph pack cannot implement context-sensitive prompts. Context-aware controls require future game/emulator integration or a deliberately unified keyboard layout and matching PCSX2 bindings.

## Experimental graphical proof of concept

A private, user-facing 0.5b proof-of-concept package was produced containing **two original keyboard keycap themes** as 32×32 transparent PNGs retaining the exact 10 captured glyph hash filenames:

- `menu-reference`: Cross=ENT, Circle=S, Triangle=ESC, Start=F, Select=R, L1=Q, R1=E, L2=C, R2=SHIFT, R3=V.
- `driving-first`: Cross=W, Circle=S, Triangle=SPACE, Start=ESC, Select=R, L1=Q, R1=E, L2=C, R2=SHIFT, R3=V.

Original user-supplied game texture bitmaps and the private proof ZIP are **not committed**. Both themes are *unverified in game*. Images do not set real keyboard bindings. Enable PCSX2 **Load Textures**, use a matching game serial/profile in the 0.4+ glyph importer, and verify every icon in-game. Back up other texture mods first. No promise is made that the captured hashes work across US, PAL and JP releases; the dump did not include game-serial metadata.

## Next practical steps

1. Capture Square/Delete/Speedbreaker in actual menu and gameplay contexts with PCSX2 dumping enabled.
2. Run a single 10-image proof pack in the exact same game build that produced the dump, matching actual keyboard mappings.
3. Photograph the results, note missing/reused icons, and refine the 256×256 controller atlas separately.
4. Only after proven glyph replacement, research context-dependent prompts and true direct keyboard name entry.
