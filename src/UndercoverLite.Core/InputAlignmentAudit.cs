namespace UndercoverLite.Core;

/// <summary>
/// Read-only alignment checks for this launcher's *declared* input profile and
/// glyph-theme identity. Cannot determine active PCSX2 game input profile or
/// recognize the pixels of independently installed textures.
/// </summary>
public static class InputAlignmentAudit
{
    public sealed record Observation(
        bool ExpectedProfileExists, bool ExpectedProfileUnmodified,
        bool? ThemeKnownMatched, string Message, string NextStep);

    public static Observation Inspect(string pcsx2DataDirectory, string? gameSerial,
        string? textureSearchDirectory = null)
    {
        var path = Path.Combine(
            UndercoverKeyboardProfile.GetProfileDirectory(pcsx2DataDirectory),
            UndercoverKeyboardProfile.FileName);

        var exists = File.Exists(path);
        var valid = false;
        if (exists)
        {
            var info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 ||
                info.Length > 1024 * 1024)
                throw new InvalidDataException("Input profile is a link or too large to inspect.");
            valid = string.Equals(
                File.ReadAllText(path).Replace("\r\n", "\n"),
                UndercoverKeyboardProfile.RenderIni().Replace("\r\n", "\n"),
                StringComparison.Ordinal);
        }

        string? activeTheme = null;
        if (!string.IsNullOrEmpty(gameSerial))
            activeTheme = ManagedGlyphThemes.ActivePack(
                gameSerial, pcsx2DataDirectory, textureSearchDirectory);

        // Only the new 0.11 theme was generated from this exact source.
        // Name may be registered while PNGs were independently edited; do not
        // claim per-pixel integrity or actual in-game load.
        var modern = activeTheme?.Contains(
            "matched driving keys 0.11", StringComparison.OrdinalIgnoreCase) == true;
        var outdated = activeTheme?.Contains(
            "keyboard diagram", StringComparison.OrdinalIgnoreCase) == true ||
            activeTheme?.Contains("driving-first", StringComparison.OrdinalIgnoreCase) == true;

        if (modern && valid)
            return new(true, true, true,
                "The registered 0.11 glyph theme and unmodified DrivingFirst input-profile file use matching declared keys. " +
                "Active PCSX2 selection and actual texture pixels are not verified.",
                "Select UndercoverLite-DrivingFirst in Undercover Game Properties > Summary > Input Profile.");

        if (outdated)
            return new(exists, valid, false,
                "The registered older driving-first artwork contains known incorrect key labels " +
                "(such as Esc for Start or B for Speedbreaker).",
                "Switch to the 0.11 matched-driving-first glyph theme, and select its matching input profile in PCSX2.");

        if (modern && !valid)
            return new(exists, false, false,
                exists
                    ? "The 0.11 glyph theme is registered, but the installed DrivingFirst profile differs from the declared keys."
                    : "The 0.11 glyph theme is registered, but the matching DrivingFirst profile file is missing.",
                "Install the unmodified DrivingFirst profile from PC Input Lab and manually select it in PCSX2.");

        if (!exists)
            return new(false, false, null,
                "The DrivingFirst input-profile file is not installed in the isolated PCSX2 folder. " +
                "Custom or different profiles cannot be validated here.",
                "Install the profile from PC Input Lab, then assign it to Undercover in PCSX2.");

        if (!valid)
            return new(true, false, null,
                "The DrivingFirst input-profile file has different contents from Undercover Lite's built-in mapping. " +
                "Any previously generated glyphs may not match.",
                "Inspect your custom input profile and use a glyph theme matching those exact PS2 bindings.");

        return new(true, true, null,
            "The declared DrivingFirst profile file is intact. No registered 0.11 theme was found for the verified serial. " +
            "The actual PCSX2 active input profile is not known.",
            "Install the 0.11 glyph theme for the same serial, then manually select DrivingFirst in PCSX2.");
    }
}
