# 0.6: Managed PCSX2 button-glyph themes

## Confirmed baseline

In a real Undercover Lite PCSX2 2.8.2 session, the developer successfully replaced a **Cross / Accept** prompt with an original **W** keycap graphic. This demonstrates working texture lookup for the captured hash and the corrected Search Directory. It does **not** imply that PCSX2 keyboard mappings were changed, that all glyph hashes are loaded, or that the 256x256 controller diagram was replaced.

## Why a theme manager?

The 0.5b proof ZIP contains both **driving-first** (Cross → W) and **menu-reference** (Cross → ENT) themes for the same hashed PS2 icons. Installing one directly over the other was correctly blocked by the 0.4/0.5 safety importer. The 0.6 manager makes switching these themes practical **without overwriting unrelated texture mods**.

## Switching an existing legacy installation

1. Close the game and PCSX2.
2. Run the latest Undercover Lite launcher. Confirm the game serial and the **Texture Replacement Search Directory** exactly match those shown in PCSX2 Graphics Settings.
3. Open **Button glyphs / texture packs**.
4. If the currently installed theme came from the old installer, first select **Activate / Switch** and choose the **same pack** currently visible in the game. For the screenshot showing **Accept [W]**, that's likely the **driving-first** theme, but confirm its exact files first.
5. If the existing files are *byte-identical* to the selected pack, tick **I own matching glyphs from the old installer; allow adopting those exact files**. Review the preview and confirm. This records ownership without overwriting those identical files.
6. Select **Activate / Switch** again and choose the other theme (for example, **menu-reference**) for the same game serial. Review the preview. Unknown conflicting files are blocked.
7. Restart PCSX2 and visit the menu to check the icons. The theme manager does not switch textures live.

**If existing icons aren't byte-identical**, do **not** force adoption. Use **Open replacements** to identify the conflicting files and move your old glyph images to a safe backup folder manually. Then activate the new theme. The manager will never silently delete or overwrite unknown mod files.

## Disabling a managed theme

Use **Disable managed glyphs** after closing the game. The tool compares the current SHA-256 of each tracked replacement against its last installed value, and removes **only unchanged tracked files**. The next game session will show the original PS2 art for those hashes unless another replacement mechanism is active.

The managed state is stored at:

    <PCSX2 Search Directory>/<GAME SERIAL>/.undercoverlite-glyph-state.json

Only the local install state and image hashes are stored. No original game textures are committed to the GitHub repository, and no game ISO or BIOS is copied.

## Behavior and limitations

- Works on recognized, uncompressed Undercover ISO versions, with the correct PCSX2 texture Search Directory. User-supplied texture hashes may differ by region.
- Default mode never adopts old files without explicit confirmation.
- If an existing non-managed replacement differs from the selected theme, switching is blocked.
- If a previously managed file was edited or removed externally, switching and disabling are blocked. Preserve the edited file and resolve the mismatch manually.
- All bytes are staged in memory, up to 64 MiB for incoming and 64 MiB for existing managed content, before mutation. Best-effort restoration is attempted if a write fails. A forced power-off during a multi-file switch is not a transactional operation; backups remain advisable.
- This is a **static artwork switcher**. A single Cross icon can represent Accept in menus and Accelerate in gameplay. Undercover Lite doesn't currently perform context-sensitive UI switching or modify actual PCSX2 keyboard binds.

## Validation

The project includes synthetic-file smoke tests for first activation, switching overlapping hashes, removal, unknown-file preservation, edited-file refusal, opt-in adoption of legacy icons, and foreign conflicting texture rejection. These tests don't replace a hands-on PCSX2 gameplay test.
