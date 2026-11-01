namespace UndercoverLite.Core;

/// <summary>
/// Read-only first-run diagnostics. Never launches PCSX2, writes settings,
/// probes BIOS files, or assumes an emulator option is enabled.
/// </summary>
public static class SetupDoctor
{
    public enum Severity { Ready, Notice, ActionNeeded, Blocked }

    public sealed record Finding(Severity Level, string Topic, string Detail, string NextStep);

    public sealed record Result(IReadOnlyList<Finding> Findings, string? VerifiedSerial)
    {
        public int Blockers => Findings.Count(x => x.Level == Severity.Blocked);
        public int ActionItems => Findings.Count(x => x.Level is Severity.Blocked or Severity.ActionNeeded);

        /// <summary>Safe to share publicly: never prints absolute paths or user file names.</summary>
        public string CopyableReport()
        {
            var lines = new List<string>
            {
                "Undercover Lite PC | Setup Doctor 0.11",
                "PCSX2 target: 2.8.2 Qt (actual binary version not inspected)",
                "Disc: " + (VerifiedSerial ?? "unverified / not selected"),
                $"Blocking findings: {Blockers}; attention items: {ActionItems - Blockers}",
                ""
            };
            foreach (var item in Findings)
            {
                lines.Add($"[{item.Level}] {item.Topic}: {item.Detail}");
                if (!string.IsNullOrWhiteSpace(item.NextStep))
                    lines.Add("  Next: " + item.NextStep);
            }
            lines.Add("");
            lines.Add("No local paths, game image bytes or BIOS content included.");
            return string.Join(Environment.NewLine, lines);
        }
    }

    // Hash-named textures catalogued from the user's own PCSX2 captures.
    private static readonly HashSet<string> KnownPrompts = new(StringComparer.OrdinalIgnoreCase)
    {
        "fa7c4c608af78f38-99e1a7bd2d4d93c5-00001554.png", // Cross
        "3ef5ed33ce7400e6-5d5080dd80f7a56d-00001554.png", // Circle
        "3932bc820cff2295-7e347471dfd31f91-00001554.png", // Triangle
        "6a9f9e4d69bb02d2-be3e24ef4b371b8d-00001554.png", // Start
        "565c987dcc9942d1-be3e24ef4b371b8d-00001554.png", // Select
        "d2faf9a272a7cd2d-3959933ff5fb8b26-00001554.png", // L1
        "b504e4faa31b1c73-d78b51572ac25e65-00001554.png", // L2
        "5e8b3773662cfffd-15ab2dbca5c4e797-00001554.png", // R1
        "ae0846b61eea3645-9c05bbf6e9ad7089-00001554.png", // R2
        "99752cf8c7f203b4-37a640d9a823710-00001554.png", // R3
        "b99d9ef8125d8894-f3379f9602408f67-00002213.png"  // controller atlas
    };

    public static Result Check(LauncherSettings settings, string pcsx2DataDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var items = new List<Finding>();
        void Add(Severity kind, string topic, string detail, string next = "") =>
            items.Add(new(kind, topic, detail, next));

        if (string.IsNullOrWhiteSpace(settings.Pcsx2Executable) ||
            !File.Exists(settings.Pcsx2Executable))
            Add(Severity.Blocked, "PCSX2 executable", "No existing executable is selected.",
                "Choose pcsx2-qt.exe from your local PCSX2 2.8.2 installation.");
        else if (!string.Equals(Path.GetFileName(settings.Pcsx2Executable), "pcsx2-qt.exe",
                     StringComparison.OrdinalIgnoreCase))
            Add(Severity.ActionNeeded, "PCSX2 executable",
                "An executable is selected but is not named pcsx2-qt.exe.",
                "Verify that the file is the official PCSX2 Qt executable.");
        else Add(Severity.Ready, "PCSX2 executable",
                "pcsx2-qt.exe exists; executable version and startup are not verified.");

        string? serial = null;
        if (string.IsNullOrWhiteSpace(settings.GameImage) || !File.Exists(settings.GameImage))
            Add(Severity.Blocked, "Game image", "No existing game image is selected.",
                "Choose your own Undercover PS2 ISO or supported compressed image.");
        else
        {
            // The existing inspector only reads a bounded amount of ISO9660 metadata.
            var disc = Ps2DiscIdentifier.Inspect(settings.GameImage);
            if (disc.Recognition == DiscRecognition.Undercover)
            {
                serial = disc.Serial;
                Add(Severity.Ready, "Game identification", "Undercover PS2 serial verified: " + serial + ".");
            }
            else if (disc.Recognition == DiscRecognition.DifferentGame)
                Add(Severity.Blocked, "Game identification",
                    "The ISO BOOT2 serial is not a known Undercover PS2 edition.",
                    "Select the intended Undercover disc image. The launcher has a manual override.");
            else if (disc.Recognition == DiscRecognition.NotInspected)
                Add(Severity.ActionNeeded, "Game identification",
                    "This compressed or non-ISO image exists but its disc serial was not inspected.",
                    "Confirm the serial in PCSX2. Automatic glyph installation requires a verified ISO.");
            else Add(Severity.ActionNeeded, "Game identification",
                    "The image exists but its Undercover PS2 identity could not be confirmed.",
                    "Check the image in PCSX2 or use a verified uncompressed ISO.");
        }

        if (!settings.IsolatePcsx2Data)
            Add(Severity.ActionNeeded, "PCSX2 profile",
                "Isolated emulator data is disabled. Texture location cannot be inferred reliably.",
                "Check PCSX2 Graphics > Texture Replacement > Search Directory manually.");
        else Add(Severity.Ready, "PCSX2 profile",
                "Isolated emulator data is selected; does not prove BIOS or controller setup.");

        try
        {
            var root = GlyphPackManager.GetTextureSearchDirectory(
                pcsx2DataDirectory, settings.TextureSearchDirectory);
            var explicitlySelected = !string.IsNullOrWhiteSpace(settings.TextureSearchDirectory);
            if (!Directory.Exists(root))
                Add(Severity.Notice, "Texture search directory",
                    "The selected texture search directory does not exist yet.",
                    "Start PCSX2 setup or confirm its Search Directory; the doctor won't create folders.");
            else Add(explicitlySelected ? Severity.Ready : Severity.Notice,
                "Texture search directory",
                explicitlySelected
                    ? "The explicitly selected texture directory exists (PCSX2 live setting unverified)."
                    : "The inferred texture directory exists (PCSX2 live setting unverified).",
                explicitlySelected ? "" : "Compare this folder with the Search Directory shown in PCSX2.");

            if (settings.IsolatePcsx2Data)
            {
                var stale = Path.Combine(Path.GetFullPath(pcsx2DataDirectory), "textures");
                var active = Path.GetFullPath(root);
                if (!string.Equals(stale, active, StringComparison.OrdinalIgnoreCase) && Directory.Exists(stale) &&
                    Directory.EnumerateFileSystemEntries(stale).Any())
                    Add(Severity.ActionNeeded, "Legacy texture directory",
                        "Files exist in the old direct-textures location, separate from the selected directory.",
                        "If glyphs are missing, reinstall your pack using the active PCSX2 Search Directory.");
            }

            if (serial is not null)
            {
                var gameRoot = GlyphPackManager.GetGameTextureDirectory(
                    pcsx2DataDirectory, serial, root);
                var replacements = Path.Combine(gameRoot, "replacements");
                if (!Directory.Exists(replacements))
                    Add(Severity.Notice, "Replacement textures",
                        "No replacements folder exists for this verified game serial.",
                        "This is normal before installing a glyph pack.");
                else
                {
                    var textureFiles = Directory.EnumerateFiles(replacements)
                        .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    p.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                        .Take(20001)
                        .ToArray();
                    var matching = textureFiles.Count(p => KnownPrompts.Contains(Path.GetFileName(p)));
                    Add(Severity.Ready, "Replacement textures",
                        $"{textureFiles.Length}{(textureFiles.Length == 20001 ? "+" : "")} PNG/DDS files; " +
                        $"{matching} match the 11 catalogued Undercover button/diagram hashes. " +
                        "Presence does not prove PCSX2 loaded them.");
                }
                try
                {
                    var name = ManagedGlyphThemes.ActivePack(serial, pcsx2DataDirectory, root);
                    Add(Severity.Notice, "Managed glyph theme",
                        name is null ? "No Undercover Lite-managed theme state is registered."
                            : "A managed theme state is registered (file integrity not checked by this report).",
                        name is null ? "You can adopt byte-identical older files in the glyph-theme window." : "");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
                {
                    Add(Severity.ActionNeeded, "Managed glyph theme",
                        "A glyph ownership state file could not be read or validated.",
                        "Check theme switching in the launcher before modifying replacement files.");
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            Add(Severity.ActionNeeded, "Texture paths",
                "Texture search directories could not be inspected.",
                "Confirm the path and permissions in PCSX2 Graphics Settings.");
        }

        try
        {
            var alignment = InputAlignmentAudit.Inspect(
                pcsx2DataDirectory, serial, settings.TextureSearchDirectory);
            Add(alignment.ThemeKnownMatched == false ? Severity.ActionNeeded :
                alignment.ThemeKnownMatched == true ? Severity.Ready : Severity.Notice,
                "Input/icon alignment", alignment.Message, alignment.NextStep);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or
                                   InvalidDataException or ArgumentException or
                                   System.Text.Json.JsonException)
        {
            Add(Severity.ActionNeeded, "Input/icon alignment",
                "The keyboard profile or registered glyph theme could not be inspected safely.",
                "Verify the profile and theme in PCSX2 and Undercover Lite before changing any files.");
        }

        Add(Severity.Notice, "Runtime verification",
            "BIOS readiness, current PCSX2 Load Textures setting, key bindings and actual game rendering are not checked.",
            "Verify them inside PCSX2 when you are back at your computer.");
        return new(items.AsReadOnly(), serial);
    }
}
