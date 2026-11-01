# Undercover Lite PC UI-3 (0.14): Saved Undercover input-profile detection

This adds read-only inspection of the **input profile PCSX2 saved for a particular Undercover game entry**. It helps answer why a keyboard label may not match the key that actually works. It reads **saved configuration on disk**, not live controller input or PCSX2 memory.

## Verified PCSX2 2.8.2 behavior

From the original v2.8.2 source:

- **pcsx2/VMManager.cpp**, GetGameSettingsPath: per-game INI names are **SERIAL_EIGHTHEXELFCRC.ini**, inside EmuFolders::GameSettings. When no serial-specific INI exists, PCSX2 can fall back to a legacy **CRC.ini** path.
- **pcsx2/VMManager.cpp**, UpdateGameSettingsLayer: a game-specific INI's **[EmuCore] InputProfileName** selects the input profile. With no name, PCSX2 uses Shared mappings.
- **pcsx2-qt/Settings/GameSummaryWidget.cpp**, onInputProfileChanged: PCSX2 writes/removes the same **[EmuCore] InputProfileName** key in Undercover's Game Properties.
- **pcsx2/Pcsx2Config.cpp**, EmuFolders::LoadConfig: GameSettings and InputProfiles folders are relative to PCSX2's data root unless configured otherwise.

Sources: https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/VMManager.cpp and https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2-qt/Settings/GameSummaryWidget.cpp

## How it works

Open **PC Settings → Controls → Saved game input selection → Inspect game profile...**. The result shows what the unique saved game-specific INI selects, if one can be determined safely. **Setup Doctor** now includes a "Saved game input profile" finding and, when using the 0.11 matched glyph theme, warns if the saved game selection is Shared, a different/custom layout, missing, or modified.

1. The configured game image must be an uncompressed ISO with its **Undercover PS2 serial verified** by our bounded ISO reader.
2. The launcher must use isolated PCSX2 data. This check reads only the expected **PCSX2.ini** under that data root. It refuses ambiguous direct/nested roots.
3. The game-settings and input-profile directories must use their expected default names. If PCSX2 has custom directories in **[Folders]**, the doctor reports an unsupported custom location rather than reading arbitrary paths.
4. Only a file named **VERIFIED_SERIAL_XXXXXXXX.ini** where X is hexadecimal is considered a match. Unrelated games and other Undercover regions are ignored.
5. If **zero** matching serial/CRC files are found, the selection is **unverified**, not automatically assumed Shared. The real game may have no saved override, use a legacy CRC-only file or use a different data root.
6. If **multiple** matching CRC-specific files exist, the result is **ambiguous**. This tool never guesses which executable CRC PCSX2 has actually loaded.
7. If **exactly one** matching game INI exists, the saved **InputProfileName** is read. Missing/blank means **Shared**. A named profile is checked against its file and against our known 0.9 DrivingFirst or 0.10 steering-menu variants. Any custom or altered file is reported honestly.

It does not switch the profile, change game configs, create folders, read BIOS content, or alter textures.

## Why this is not live validation

An emulator that is **already running** may still have an old input profile in memory. The saved game INI may be unused if PCSX2 is launching a different executable CRC, disc region or custom GameSettings folder. The tool cannot yet prove which keypress reached the game, and it cannot make Cross act differently in driving and menus. The detected disk selection is evidence, **not a runtime controller reading**.

### Safe testing

Run the launcher with your known Undercover ISO selected, open **Controls** and inspect the saved game selection. If it reports **UndercoverLite-DrivingFirst** and matching source, use the **0.11 matched-driving-first** keyboard glyph theme. If it reports **Shared**, select the matching profile manually in **PCSX2 → right-click Undercover → Properties → Summary → Input Profile**. Close PCSX2 before configuration changes and restart to test.

If multiple CRC configurations are present, check the correct game entry/CRC inside PCSX2 first. Do not delete settings files arbitrarily.

## Automated regression checks

Synthetic tests cover isolated profile handling, missing emulator configuration, compressed disc refusal, exact game serial + CRC matching, rejection of legacy/other-game files, detecting Shared versus named profile, verifying known input profiles, rejecting modified/custom bindings, duplicate keys, unsafe paths, custom folder warnings, ambiguous CRCs and safe-report path redaction. No copyrighted game images or BIOS are shipped.
