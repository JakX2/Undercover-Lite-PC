# PlayStation 2 disc identification (prototype 0.2)

The launcher can identify the selected **uncompressed ISO** without extracting the entire disc. It reads only the ISO9660 primary volume descriptor at sector 16, bounded root directory records, and the small `SYSTEM.CNF` file, looking specifically at the `BOOT2` line. The filename itself does **not** prove the title.

Verified Need for Speed: Undercover PS2 serials presently recognized:

- North America: `SLUS-21801`
- PAL: `SLES-55349`, `SLES-55350`, `SLES-55351`, `SLES-55352`, `SLES-55353`
- Japan: `SLPM-55127`, `SLPM-55244`

Source: [PCSX2 game's database wiki entry](https://wiki.pcsx2.net/Need_for_Speed:_Undercover).

The original U.S. release boot field commonly resembles `BOOT2 = cdrom0:\SLUS_218.01;1`. The launcher normalizes it to `SLUS-21801`.

**Scope and safety:**

- This is **serial identification only**, not a CRC check, anti-tamper system, authenticity proof, complete image integrity validation, or automatic localization detection.
- Other PS2 disc serials trigger an explicit confirmation before starting the game, rather than launching a wrongly selected ISO without notice.
- Malformed or unreadable ISOs also receive a clear warning and can still be launched after confirmation (useful for uncommon dumps).
- `.chd`, `.cso`, `.zso`, `.gz` are *not* decoded by the launcher; they can still be launched via PCSX2 after a warning.
- Maximum inspected root-directory size is 4 MiB; maximum `SYSTEM.CNF` size is 64 KiB. Bounds are validated before seeking. All reads are read-only.
- Do not upload or commit disc images or a BIOS to this repository.

This code is deliberately dependency-free and tested against synthetic ISO9660 images containing fake `SYSTEM.CNF`, not copyrighted game data. It does not write or alter images or patch PS2 code.