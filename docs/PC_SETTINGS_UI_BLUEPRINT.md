# UNDERCOVER LITE PC | PC Settings UI blueprint

**Design revision:** 2026-11-01. **Target:** Exient's Need for Speed: Undercover (PS2) running through the existing PCSX2-backed Windows launcher, not EA Black Box's original PC release.

## Design contract

The menu should feel like a genuine PC game's options screen, but it must never pretend an unimplemented option works. The first implementation is a standalone launcher/companion window, **not yet an injected in-game menu**.

**Status:** EXISTING = available through an existing launcher workflow, not necessarily in this unified screen; ADAPTER = PCSX2 feature or integration that needs verified config handling; RESEARCH = requires deeper PCSX2/game investigation; REFERENCE = truthful read-only display.

**Apply:** LAUNCH = on next launch; PCSX2 = manually in emulator until a safe adapter exists; READ = read-only; FUTURE = disabled until implemented.

**Header:** UNDERCOVER | PC SETTINGS. Subtitle: Exient PS2 edition • PCSX2 powered. Fixed tabs: **Graphics, Controls, Audio, Interface, Advanced, Troubleshooting**. Scroll the current tab's body; footer has **Back, Restore page defaults, Apply changes**, but disable Apply if there is nothing functional to apply.

## 1. Graphics

### Display
| Exact label | Type, options, default | Helper text | Status / Apply |
|---|---|---|---|
| Display mode | Dropdown: Fullscreen (default), Windowed. Borderless disabled | Choose how Undercover opens on your desktop. | EXISTING (first two) / LAUNCH |
| Resolution | Read-only: Managed by PCSX2. Later list detected desktop resolutions | Window output resolution is different from the game's internal resolution. | ADAPTER / PCSX2 |
| Aspect ratio | Dropdown: Game default, 4:3, 16:9 (after validation) | Widescreen output is not the same as a correctly patched widescreen HUD. | ADAPTER / PCSX2 |
| V-Sync | Toggle: Use PCSX2 setting initially | Sync the displayed frames to your monitor; may add input latency. | ADAPTER / PCSX2 |
| Frame rate | Read-only: Original game timing | An unsupported 60 FPS or unlimited mode could break game physics and timing. | RESEARCH / READ |

### Rendering
| Exact label | Type, options, default | Helper text | Status / Apply |
|---|---|---|---|
| Internal resolution | Dropdown: Native 1×, 2×, 3×, 4×, 6× after verifying settings | Higher values improve clarity and may lower performance. | ADAPTER / PCSX2 |
| Anisotropic filtering | Dropdown: Off, 2×, 4×, 8×, 16× | Improves texture clarity on angled surfaces when supported. | ADAPTER / PCSX2 |
| Renderer | Dropdown: Automatic, Vulkan, Direct3D 12, OpenGL where supported | Not all options are available on every PCSX2 build or machine. | ADAPTER / PCSX2 |
| Texture replacements | Action: Manage texture packs; actual Load Textures toggle pending | Replace verified Undercover button sprites without overwriting other texture mods. | EXISTING pack manager; ADAPTER toggle / LAUNCH + PCSX2 |
| Preload / async textures | Disabled toggle | Exact PCSX2 setting and performance tradeoffs must be verified first. | RESEARCH / FUTURE |
| Widescreen patches | Disabled toggle | Only available if a compatible Exient PS2 patch is independently verified. | RESEARCH / FUTURE |
| Motion blur / bloom | Disabled setting | Would require reliable game-specific graphics changes, not a generic emulator switch. | RESEARCH / FUTURE |

## 2. Controls

### Profiles
| Exact label | Type, options, default | Helper text | Status / Apply |
|---|---|---|---|
| Input device | Read-only: Managed by PCSX2 | A profile file on disk does not prove which input device is being used. | REFERENCE / READ |
| Input profile | Selector preview: Shared, DrivingFirst, Steering preset variants; Open PCSX2 input settings | You must select the actual active game input profile in PCSX2 Game Properties. | EXISTING installers; ADAPTER active-profile reading / PCSX2 |
| Keyboard steering strength | Dropdown: Gentle 65%, Balanced 85%, Full 100% | Limits stick magnitude, not gradual keyboard steering smoothing. | EXISTING 0.10 presets / PCSX2 |
| Extra menu keys | Toggle, default Off: Enter = Cross, Backspace = Triangle | These additional keys also trigger Accelerate and Handbrake while driving. | EXISTING 0.10 menu variants / PCSX2 |
| Input hotkey conflicts | Action: Inspect conflicts | PCSX2 can use Space for emulator pause while this layout uses it as handbrake. | EXISTING / READ |
| Rebind individual keys | Disabled button: Coming later | True key capture and PS2 input remapping need a validated PCSX2 integration. | RESEARCH / FUTURE |

### Reference binding grid
**Only matches the intended mappings when UndercoverLite-DrivingFirst is selected in PCSX2.** It does not establish live active bindings.

| Game action | PC key | PS2 input |
|---|---|---|
| Accelerate / Menu Accept | W | Cross |
| Brake / Reverse | S | Circle |
| Steer left / right | A / D | Left stick |
| Handbrake / Menu Back | Space | Triangle |
| Alternative handbrake | C | L2 |
| Nitrous | Shift | R2 |
| Speedbreaker / Name-entry Delete | X | Square |
| Gear down / up | Q / E | L1 / R1 |
| Pause / Name-entry Done | F | Start |
| Reset car | R | Select |
| Change view | V | R3 |
| Menu navigation | Arrow keys | D-pad |
| Camera / right stick | I / J / K / L | Right stick |
| Left-stick click | Z | L3 |

**Warning row:** Displayed key labels may not match controls if an old 0.7 texture theme or another active input profile is used. Actions: Open Setup Doctor, Manage glyphs, Open PCSX2 input properties. Do not claim automatic recognition of active input profile.

## 3. Audio

These are deliberately disabled design slots until a real PCSX2 or Exient game setting is available. The current launcher does **not** expose working volume controls.

| Exact label | Type, suggested default | Helper text | Status |
|---|---|---|---|
| Master volume | Disabled slider 0–100; PCSX2 managed | Adjust game output only after PCSX2 audio setting integration is tested. | ADAPTER |
| Music volume | Disabled slider 0–100 | Separate music mixing requires a confirmed game setting or patch. | RESEARCH |
| Sound effects | Disabled slider 0–100 | Independent effects volume cannot be promised without game support. | RESEARCH |
| Speech volume | Disabled slider 0–100 | Requires separate dialogue volume control in the game. | RESEARCH |
| Output device | Disabled dropdown: System default | Select speakers or headset after testing PCSX2 output-device handling. | ADAPTER |
| Mute when unfocused | Disabled toggle, default Off | Requires robust focus detection and a safe muting backend. | RESEARCH |
| Audio latency | Disabled dropdown: Default | Only expose documented, verified PCSX2 audio ranges. | ADAPTER |

**Audio empty-state microcopy:** Audio is currently configured in PCSX2. These controls will become available as their backends are verified. Action: Configure PCSX2.

## 4. Interface

| Exact label | Type, suggested default | Helper text | Status / Apply |
|---|---|---|---|
| Button glyph theme | Action/selector: Original PS2, Matched Keyboard 0.11, User-imported themes | Installed texture labels must agree with selected PS2-to-key input bindings. | EXISTING theme manager / LAUNCH |
| Keyboard controller diagram | Action: Preview diagram | Displays original replacement artwork, not a live control overlay. | ADAPTER / READ |
| Displayed input style | Read-only: Static texture theme | One PS2 Cross icon is reused for Accept and Accelerate in different contexts. | REFERENCE |
| HUD scale | Disabled slider: 100% | Internal render upscaling does not resize independent HUD elements. | RESEARCH |
| Safe area | Disabled slider | Requires game-specific UI anchor or HUD patches. | RESEARCH |
| Subtitles | Disabled toggle | Verify the actual Exient game feature before binding it. | RESEARCH |
| Menu text size | Disabled dropdown | Requires game text/layout work beyond controller sprite replacement. | RESEARCH |
| Language | Disabled dropdown | Depends on installed game's edition and assets. | RESEARCH |

## 5. Advanced

### Startup and files
| Exact label | Type, default | Helper text | Status / Apply |
|---|---|---|---|
| Fast boot | Toggle, On | Skip PS2 startup sequence on next launch. | EXISTING / LAUNCH |
| Hide PCSX2 main window | Toggle, On | Launch directly into game view when supported. | EXISTING / LAUNCH |
| Isolated PCSX2 data | Toggle, On; confirm when disabling | Keep Undercover Lite's saves/settings separate; changing it does not migrate files. | EXISTING / LAUNCH |
| Companion hotkey | Toggle, On | Ctrl+Shift+F10 opens the external companion window while the game is running. | EXISTING / LAUNCH |
| PCSX2 executable | File picker | Select your PCSX2 Qt program; filename alone does not verify version. | EXISTING / LAUNCH |
| Game disc image | File picker | Select your own legally obtained PS2 image. | EXISTING / LAUNCH |
| Texture search directory | Folder picker | Match the Search Directory actually configured in PCSX2 graphics settings. | EXISTING / LAUNCH |
| Configure PCSX2 | Action | Open emulator settings; do not silently rewrite global emulator configuration. | EXISTING |

### Experimental
| Exact label | Type, default | Helper text | Status |
|---|---|---|---|
| Texture dumping | Disabled toggle, Off | Developer-only capture mode; storage and PCSX2 binding require verification. | RESEARCH |
| Detailed diagnostic logging | Disabled toggle, Off | Bound and opt-in logs; do not collect BIOS, ISO, or private paths for sharing. | RESEARCH |
| Borderless desktop integration | Disabled toggle, Off | Requires actual PCSX2 windowing behavior to be tested. | RESEARCH |

## 6. Troubleshooting

| Exact label | Type | Helper text | Status |
|---|---|---|---|
| Run Setup Doctor | Action | Inspect executable, image, texture folders and declared input alignment. | EXISTING |
| Copy safe report | Action | Copy troubleshooting findings without user paths or BIOS/game bytes. | EXISTING |
| Check game image | Action | Identify supported Undercover PS2 ISO editions by boot serial. | EXISTING |
| Check input/icon alignment | Read-only diagnostic | Compares installed profile contents and registered artwork, not PCSX2 live selection. | EXISTING |
| Inspect PCSX2 hotkeys | Read-only diagnostic | Detect potential Space/Escape hotkey collisions without editing bindings. | EXISTING |
| Manage glyph themes | Action | Safely activate, switch or disable tracked user texture replacements. | EXISTING |
| Install keyboard profile | Action | Generate a separate input profile, requiring manual game-specific selection. | EXISTING |
| Open configuration folder | Action | Show local files, never include paths in exported safe report. | ADAPTER |
| Back up launcher settings | Action, future | Export launch preferences without BIOS, games or saves. | RESEARCH |
| Restore launcher defaults | Action + confirmation, future | Reset only launcher preferences, never PCSX2 setup, game saves or mods. | RESEARCH |

**Diagnostic states:** Disc recognized, PCSX2 executable exists, texture root found, managed theme registered, input-profile file present, active profile unverified, actual texture rendering unverified.

## Visual design system

- Background: **#0B0D11**. Surface: **#181D27**. Dividers: **#353C47**. Main text: **#F0F2F5**. Muted text: **#AEB8C8**. Selected tab and actionable focus: **amber #FFB640**.
- Fonts: Segoe UI/system sans. Header: 20–24 px. Group headings: 14–16 px. Setting labels: 12–14 px. Helper text: 11–12 px.
- Desktop target: 980×720, min 800×600, DPI-aware. Fixed header and footer, scrollable tab contents.
- Rows: left label + one-line helper, right input, second line for availability/when it applies. Use plain text for state rather than decorative badges.
- Keyboard support: focus order, visible focus indicators, true native-accessible controls, legible contrast, generous clickable targets.
- **Footer semantics:** Back (discard pending changes only with warning), Restore page defaults (only where implemented), Apply changes (disabled until there is a real save). Never present fake working sliders.
- **Errors:** on conflict, cancel, or invalid path, preserve existing user files. Only known and explicitly approved file mutations allowed.

### Text wireframe

    UNDERCOVER | PC SETTINGS                 Exient PS2 · PCSX2 powered
    Graphics | Controls | Audio | Interface | Advanced | Troubleshooting

    DISPLAY
    Display mode                             [Fullscreen        v]
    Choose how Undercover opens.             Applies on next launch

    Resolution                               [Managed by PCSX2   ]
    Desktop mode is separate from internal rendering.

    RENDERING
    Internal resolution                      [PCSX2 managed      ]
    Increase image clarity.                  Configure in PCSX2

                                              [Back] [Apply changes]

## Implementation plan

1. **UI-1: Working shell.** New WinForms settings screen; six tabs and descriptive rows; wire only existing launcher preferences and actions. Preserve every advanced property in launcher.json during save. Route to PCSX2 setup, Setup Doctor, glyph manager and PC input lab. Use disabled/read-only placeholders for the rest.
2. **UI-2: Verified PCSX2 graphics adapter.** Verify actual PCSX2 2.8.2 config paths/keys/layers first. Add atomic backup and user confirmation for graphics changes. Start with internal resolution, renderer and V-Sync after testing.
3. **UI-3: Actual input selection awareness.** Discover selected game-specific profile from authoritative PCSX2 config, then compare to the active glyph theme. Never conflate installed profile files with active controller inputs.
4. **UI-4: Deeper game integration.** Optional external menu overlay; if native-style Exient engine settings, audio submixes, HUD scaling, framerate unlock and context-sensitive keyboard controls are desired, treat each as its own reverse-engineering milestone.

## Acceptance criteria

1. Every enabled control has a real action. Every unavailable control clearly states so.
2. No misleading claims about game settings, PCSX2 settings, selected input profile, or verified in-game visuals.
3. Closing, cancelling or failed saves never change game, BIOS, PCSX2 shared config, saves or unrelated mods.
4. Each option has an owner, backend, validation, and activation lifecycle; later config edits include backups.
5. Preserves JSON preferences owned by other Undercover Lite windows, including custom glyph paths and input suggestions.
6. Works with keyboard navigation and high-DPI Windows UI.
7. Safe reports omit private local file paths.
