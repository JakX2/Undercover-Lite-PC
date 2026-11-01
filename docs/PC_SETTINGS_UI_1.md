# Undercover Lite PC | Unified PC Settings UI-1 (prototype 0.12)

The six-tab **PC Settings** window is now a real Windows WinForms screen, reachable by clicking **PC Settings** on the main launcher. The visual style follows the dark, near-black UI with amber accents in the design blueprint. This screen is **external to the PS2 game** and is not a replacement for Exient's original menus.

## Six tabs

| Tab | Enabled today | Read-only or unavailable pending verified backend |
|---|---|---|
| Graphics | Fullscreen/windowed launch preference; Manage glyph themes | Monitor resolution, internal resolution, V-Sync, aspect ratio, renderer, anisotropic filtering, game-native effects |
| Controls | PC Input Lab, steering presets, hotkey diagnostic, declared keyboard reference | Live active profile detection, context-aware menu controls, per-action binding editor |
| Audio | Open PCSX2 configuration | Master/music/SFX/speech volume, output device |
| Interface | Manage glyph themes, Setup Doctor alignment check | HUD scaling, subtitle/language controls, live diagram |
| Advanced | Fast boot, hide main emulator window, isolated data, companion hotkey, PCSX2/game/texture paths, open PCSX2 | Game-engine diagnostic toggles |
| Troubleshooting | Setup Doctor, copy safe report, check disc image, hotkey audit, input lab, glyph manager | Bulk reset, restore from backups |

**No fake settings:** all unavailable rows are disabled and carry explanatory text. The Graphics tab's Display Mode is a *next launch* fullscreen/windowed launcher preference, **not** an OS mode/resolution change.

## How to use

1. Open the latest Undercover Lite Windows launcher.
2. Click **PC Settings** in the main button row.
3. The six tabs appear in the dark style. Explore them with mouse or keyboard. Options that are not implemented are clearly read-only.
4. Under Graphics, switch between **Fullscreen** and **Windowed**. Under Advanced, change existing supported launcher toggles and choose file locations. These do not change an already running game; **Apply changes** saves them to the launcher and they take effect on the next launch.
5. Controls, Interface and Troubleshooting link to the existing real windows for driving profiles, glyph themes and diagnostics, with no automatic activation of PCSX2 profiles.
6. When you close the settings window with pending edits, choose **Yes** to save, **No** to discard, or **Cancel** to stay.

### Data integrity

- The window takes both the persisted launcher settings and any currently unsaved values displayed in the main launcher.
- Pressing Apply reads the latest file, then applies **only launcher fields actually edited** relative to the previously saved baseline. It does not replace the complete settings object.
- Settings independently saved by the glyph manager or PC Input Lab, such as Texture Search Directory and PcPromptLayout, are preserved.
- The existing main window reloads the committed settings on return, but preserves its unsaved fields if the PC Settings dialog is cancelled without saving.
- The Glyph Manager can update the texture folder from inside the PC Settings screen; its new value is pulled into the settings screen only if the texture path was not edited there independently.
- There is **no automatic modification of PCSX2.ini**, save games, BIOS, texture mods, or original disc image by this new settings form. Actions that open existing tools retain their respective confirmations.

## Runtime and validation limits

The window doesn't know which PCSX2 per-game input profile is active. It cannot claim that the 0.11 UI theme is being displayed inside the game or that keyboard prompts have been remapped for menus.

The **Configure PCSX2** action starts the configured emulator settings interface, but cannot guarantee navigation directly to a particular graphics or audio tab. The temporary PCSX2 process object is not used to kill the emulator.

**CI:** existing launcher smoke tests plus new pure-core UI settings delta-merge tests. UI itself still requires human Windows testing for DPI, resizing, navigation and dark color fidelity.

## Next milestones

- **UI-2:** verify precise PCSX2 2.8.2 per-game graphics configuration schema, then connect internal resolution, V-Sync and rendering options safely.
- **UI-3:** read the actual active game input profile to check whether it matches displayed keycaps.
- **UI-4:** native-looking overlay or integrated game UI only after explicit research. No promise of game-engine FPS mods without independent testing.
