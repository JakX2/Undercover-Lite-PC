# Undercover Lite PC 0.16: Exact PS2 executable CRC matching

Version 0.16 improves the earlier saved-input-profile and saved-graphics-override inspectors with an **exact PCSX2 2.8.2 BOOT2 ELF CRC** computed directly from the user's uncompressed Undercover PS2 ISO.

## Verified source and algorithm

PCSX2 v2.8.2's **ElfObject::GetCRC()** in **pcsx2/Elfheader.cpp** is not polynomial CRC-32. It computes a single 32-bit XOR of all consecutive **little-endian 32-bit words** in the **entire executable file**, ignoring the final 1–3 bytes if the length is not divisible by 4.

PCSX2 obtains the boot ELF name from **SYSTEM.CNF → BOOT2**, loads that file through the disc ISO filesystem in **pcsx2/CDVD/CDVD.cpp**, and uses its CRC to locate game config files through **VMManager::GetGameSettingsPath**. Source locations:

- https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/Elfheader.cpp
- https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/CDVD/CDVD.cpp
- https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/VMManager.cpp

Game settings are named **SERIAL_EIGHTHEXCRC.ini**. PCSX2 v2.8.2 can fall back to a legacy **EIGHTHEXCRC.ini** when the named file doesn't exist.

## What is new?

1. The **Troubleshooting** tab has a read-only **Identify ELF CRC...** action.
2. **Setup Doctor** shows the CRC derived from BOOT2 ELF, in hexadecimal, when verified.
3. **Saved input profile inspection** now picks the exact game settings INI by ISO ELF CRC. It can distinguish a correct config from several older/mismatched CRC files for the same disc serial.
4. **Saved graphics override inspection** uses the same exact CRC and correctly reports the game's saved resolution even when several other CRCs exist.
5. The lookup correctly favors the exact serial-and-CRC config over the legacy CRC-only fallback. If neither exists, it does not guess a different CRC even if there is just one other file.
6. If an ELF can't be identified, the older conservative serial-only unique-candidate diagnostic is still used, with its limitations clearly stated.

## How to check

Open **Undercover Lite PC → PC Settings → Troubleshooting → Identify ELF CRC...** after selecting your uncompressed PS2 game ISO. The tool reads SYSTEM.CNF and the referenced boot executable inside the ISO, and displays the verified **8-character hexadecimal XOR CRC**.

Then run **PC Settings → Controls → Inspect game profile...**, **Graphics → Inspect game graphics...**, or **Setup Doctor**. When the ELF CRC is verified, these checks select the precise corresponding saved game configuration instead of guessing.

The tool does **not** extract copyrighted files, alter the ISO, run PCSX2, change saved profiles or patch game memory.

## Limits and safety

- Uncompressed **2048-byte-sector ISO9660** PS2 images only. CHD/CSO/ZSO/other compressed formats are not inspected.
- The disc serial must be recognized by the existing bounded ISO inspector, and BOOT2 must reference a supported CDROM executable path. Unknown or malformed paths are not guessed.
- The executable must be a valid-looking **32-bit little-endian ELF** and is read only, with no more than 256 MiB processed. ELF data is streamed through a small memory buffer. No executable is saved to disk.
- Directory traversal is bounded, ISO record extents are validated, and the boot path is restricted to simple components. Custom ISO layouts may be reported as unsupported.
- This determines the **ELF CRC from the selected ISO**, not a live emulator state's executable CRC. An ELF override, different loaded image, patches or open emulator process can still change the effective runtime configuration.
- When PCSX2's custom GameSettings folder is configured, the inspectors still refuse arbitrary folder discovery. Their checks remain read-only.
- Shared and non-isolated PCSX2 profiles are not modified or silently scanned.
- Safe reports include only game serial and CRC, not absolute personal paths, original filenames, ELF data or BIOS information.

## Validation

Synthetic tests create a minimal ISO9660 disc plus a 67-byte fake boot ELF and verify the exact word-XOR CRC, the ignored trailing 3 bytes, single-word changes, non-ELF rejection, and missing-ELF refusal. They then create multiple game INIs for the same serial, verify the exact file is chosen for **both** input and graphics, test the CRC-only legacy fallback, test serial-specific precedence and reject wrong CRCs. Tests use no copyrighted game or BIOS bytes.

Windows CI can verify file handling and C# compilation. Actual PCSX2 2.8.2 gameplay testing is still necessary to confirm the selected ISO is the one PCSX2 is running and that the controls/rendering match saved settings.
