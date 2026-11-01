using UndercoverLite.Core;

internal static class Program
{
    private static int _passed;

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
        _passed++;
    }

    private static void ExpectThrows<T>(Action action, string description) where T : Exception
    {
        try { action(); }
        catch (T) { Check(true, description); return; }
        throw new Exception("FAIL: expected " + typeof(T).Name + ": " + description);
    }

    // Tiny ISO9660 fixture. No game bytes or BIOS are included in the tests.
    private static void WriteSyntheticIso(string file, string systemCnf)
    {
        const int sectorBytes = 2048;
        const int rootSector = 20;
        const int cnfSector = 21;
        var image = new byte[40 * sectorBytes];
        int pvd = 16 * sectorBytes;
        image[pvd] = 1;
        System.Text.Encoding.ASCII.GetBytes("CD001").CopyTo(image, pvd + 1);
        image[pvd + 6] = 1;
        WriteRecord(image, pvd + 156, new byte[] { 0 }, rootSector, sectorBytes, directory: true);
        int dir = rootSector * sectorBytes;
        dir += WriteRecord(image, dir, new byte[] { 0 }, rootSector, sectorBytes, directory: true);
        dir += WriteRecord(image, dir, new byte[] { 1 }, rootSector, sectorBytes, directory: true);
        var cnf = System.Text.Encoding.ASCII.GetBytes(systemCnf);
        dir += WriteRecord(image, dir, System.Text.Encoding.ASCII.GetBytes("SYSTEM.CNF;1"), cnfSector, cnf.Length);
        cnf.CopyTo(image, cnfSector * sectorBytes);
        File.WriteAllBytes(file, image);
    }

    private static int WriteRecord(byte[] image, int offset, byte[] name, int sector, int length, bool directory = false)
    {
        int size = 33 + name.Length + (name.Length % 2 == 0 ? 1 : 0);
        image[offset] = (byte)size;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + 2, 4), (uint)sector);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + 10, 4), (uint)length);
        image[offset + 25] = directory ? (byte)2 : (byte)0;
        image[offset + 32] = (byte)name.Length;
        name.CopyTo(image, offset + 33);
        return size;
    }

    public static int Main()
    {
        var temp = Path.Combine(Path.GetTempPath(), "UndercoverLite-Smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var store = new LauncherSettingsStore(temp);
            Check(!File.Exists(store.FilePath), "fresh data directory has no settings file");
            var defaults = store.Load();
            Check(defaults.Fullscreen && defaults.HideEmulatorWindow && defaults.IsolatePcsx2Data,
                "first-run safe defaults");
            Check(defaults.EnableCompanionHotkey, "companion hotkey defaults enabled for session only");
            var sessionGate = new GameSessionGate();
            Check(!sessionGate.IsRunning, "session starts idle");
            Check(sessionGate.TryEnter() && sessionGate.IsRunning, "session guard enters running state");
            Check(!sessionGate.TryEnter(), "session guard prevents duplicate game launches");
            sessionGate.Release();
            Check(!sessionGate.IsRunning && sessionGate.TryEnter(), "session guard allows a later new game");
            sessionGate.Release();
            var exe = Path.Combine(temp, "PCSX2 with spaces", "pcsx2-qt.exe");
            var iso = Path.Combine(temp, "My Games", "Undercover PS2.iso");
            Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
            Directory.CreateDirectory(Path.GetDirectoryName(iso)!);
            File.WriteAllBytes(exe, Array.Empty<byte>());
            File.WriteAllBytes(iso, Array.Empty<byte>());
            Check(Pcsx2InstallLocator.FindNextToLauncher(temp) is null,
                "auto-detection does not guess when PCSX2 is missing");
            var portable = Path.Combine(temp, "pcsx2-v2.8.2-windows-x64-Qt");
            Directory.CreateDirectory(portable);
            var portableExe = Path.Combine(portable, "pcsx2-qt.exe");
            File.WriteAllBytes(portableExe, Array.Empty<byte>());
            Check(Pcsx2InstallLocator.FindNextToLauncher(temp) == portableExe,
                "auto-detects v2.8.2 extracted beside launcher");
            var directExe = Path.Combine(temp, "pcsx2-qt.exe");
            File.WriteAllBytes(directExe, Array.Empty<byte>());
            Check(Pcsx2InstallLocator.FindNextToLauncher(temp) == directExe,
                "prefers PCSX2 Qt directly beside launcher");
            ExpectThrows<ArgumentException>(() => Pcsx2InstallLocator.FindNextToLauncher(""),
                "rejects missing launcher directory");
            var settings = new LauncherSettings { Pcsx2Executable = exe, GameImage = iso };
            store.Save(settings);
            Check(File.Exists(store.FilePath), "JSON config created");
            var loaded = store.Load();
            Check(loaded.Pcsx2Executable == exe && loaded.GameImage == iso, "settings round-trip paths with spaces");
            Check(loaded.FastBoot && loaded.Fullscreen, "settings round-trip switches");
            Check(loaded.EnableCompanionHotkey, "hotkey preference round-trips");
            var startInfo = Pcsx2Launcher.CreateStartInfo(loaded, store.Pcsx2DataDirectory, launchingGame: true);
            var expected = new[] { "-datapath", Path.GetFullPath(store.Pcsx2DataDirectory), "-nogui", "-fullscreen", "-fastboot", "--", Path.GetFullPath(iso) };
            Check(startInfo.ArgumentList.SequenceEqual(expected), "CLI uses official PCSX2 Qt argument order");
            Check(startInfo.ArgumentList.Last() == Path.GetFullPath(iso), "image path is one safe argument, not split by spaces");
            Check(startInfo.WorkingDirectory == Path.GetDirectoryName(exe), "PCSX2 starts in executable directory");
            Check(!startInfo.UseShellExecute, "no shell string evaluation");
            Check(!Directory.Exists(store.Pcsx2DataDirectory), "planning a launch does not modify emulator data");
            var configInfo = Pcsx2Launcher.CreateStartInfo(loaded, store.Pcsx2DataDirectory, launchingGame: false);
            Check(configInfo.ArgumentList.SequenceEqual(new[] { "-datapath", Path.GetFullPath(store.Pcsx2DataDirectory) }),
                "PCSX2 setup mode does not boot the game");
            loaded.Fullscreen = false;
            loaded.FastBoot = false;
            loaded.HideEmulatorWindow = false;
            loaded.IsolatePcsx2Data = false;
            loaded.EnableCompanionHotkey = false;
            var alternate = Pcsx2Launcher.CreateStartInfo(loaded, store.Pcsx2DataDirectory, launchingGame: true);
            Check(alternate.ArgumentList.SequenceEqual(new[] { "-batch", "-nofullscreen", "-slowboot", "--", Path.GetFullPath(iso) }),
                "all launch preferences produce corresponding flags");
            var missingGame = new LauncherSettings { Pcsx2Executable = exe };
            Pcsx2Launcher.Validate(missingGame, launchingGame: false);
            Check(true, "PCSX2 can be configured before choosing an ISO");
            ExpectThrows<InvalidOperationException>(() => Pcsx2Launcher.Validate(missingGame, launchingGame: true), "game image required for launch");
            ExpectThrows<InvalidOperationException>(() => Pcsx2Launcher.Validate(new LauncherSettings(), launchingGame: false), "PCSX2 exe required");
            var saved = File.ReadAllText(store.FilePath);
            File.WriteAllText(store.FilePath, "{invalid json");
            ExpectThrows<System.Text.Json.JsonException>(() => store.Load(), "broken JSON surfaces an error");
            Check(File.ReadAllText(store.FilePath) == "{invalid json", "broken JSON remains untouched");
            File.WriteAllText(store.FilePath, saved);
            store.Save(loaded);
            Check(!File.Exists(store.FilePath + ".tmp"), "atomic save removes temporary file");
            Check(!store.Load().Fullscreen && !store.Load().IsolatePcsx2Data, "updated settings persisted");
            Check(!store.Load().EnableCompanionHotkey, "opted-out companion hotkey remains disabled");
            // Verify disc identity without loading the game or relying on filenames.
            Check(Ps2DiscIdentifier.IdentifySystemCnf("BOOT2 = cdrom0:\\SLUS_218.01;1\n").IsUndercover,
                "Undercover US BOOT2 serial recognized");
            Check(Ps2DiscIdentifier.IdentifySystemCnf("BOOT2 = cdrom0:\\SLES_553.53;1\n").IsUndercover,
                "Undercover PAL BOOT2 serial recognized");
            Check(Ps2DiscIdentifier.IdentifySystemCnf("BOOT2 = cdrom0:\\SLPM_551.27;1\n").IsUndercover,
                "Undercover Japanese BOOT2 serial recognized");
            Check(Ps2DiscIdentifier.IdentifySystemCnf("BOOT2 = cdrom0:\\SLUS_212.67;1").Recognition == DiscRecognition.DifferentGame,
                "different PS2 title is explicitly identified");
            Check(Ps2DiscIdentifier.IdentifySystemCnf("NAME = SLUS_218.01\nBOOT2 = other\n").Recognition == DiscRecognition.Unknown,
                "cannot spoof identity with unrelated SYSTEM.CNF line");
            var validDisc = Path.Combine(temp, "verified.iso");
            WriteSyntheticIso(validDisc, "BOOT2 = cdrom0:\\SLUS_218.01;1\nVER = 1.00\n");
            var verified = Ps2DiscIdentifier.Inspect(validDisc);
            Check(verified.IsUndercover && verified.Serial == "SLUS-21801",
                "reads SYSTEM.CNF from a bounded synthetic ISO9660 root directory");
            Check(Ps2DiscIdentifier.Inspect(Path.Combine(temp, "missing.iso")).Recognition == DiscRecognition.Unknown,
                "missing ISO is not accepted as verified Undercover");
            var badIso = Path.Combine(temp, "bad.iso");
            File.WriteAllBytes(badIso, new byte[32768]);
            Check(Ps2DiscIdentifier.Inspect(badIso).Recognition == DiscRecognition.Unknown,
                "invalid ISO9660 volume descriptor is rejected");
            var compressed = Path.Combine(temp, "game.chd");
            File.WriteAllBytes(compressed, [1, 2, 3]);
            Check(Ps2DiscIdentifier.Inspect(compressed).Recognition == DiscRecognition.NotInspected,
                "CHD game can still launch, but identification is not claimed");
            // Test parsing/identity is read-only and does not change the image.
            Check(Ps2DiscIdentifier.Inspect(validDisc).IsUndercover && new FileInfo(validDisc).Length == 40 * 2048,
                "disc identity check leaves the ISO bytes untouched");
            // 0.4 user-owned glyph-pack workflow: no original NFS image or artwork is required.
            var glyphData = Path.Combine(temp, "isolated-glyph-data");
            var glyphFolder = Path.Combine(temp, "user-pack");
            var glyphReplacementFiles = Path.Combine(glyphFolder, "replacements");
            Directory.CreateDirectory(glyphReplacementFiles);
            File.WriteAllText(Path.Combine(glyphFolder, "glyph-pack.json"),
                """{"schemaVersion":1,"id":"keyboard-demo","name":"Test keyboard pack","gameSerial":"SLUS-21801","inputProfile":"keyboard"}""");
            var glyphName = "1234567890abcdef-00000000.png";
            var glyphPath = Path.Combine(glyphReplacementFiles, glyphName);
            File.WriteAllBytes(glyphPath, [137, 80, 78, 71, 13, 10, 26, 10]); // test bytes, not copyrighted art
            var nestedSearch = Path.Combine(glyphData, "PCSX2", "textures");
            var expectedGlyphDir = Path.Combine(nestedSearch, "SLUS-21801", "replacements");
            Check(GlyphPackManager.GetTextureSearchDirectory(glyphData) == nestedSearch,
                "PCSX2 2.8.2 isolated Search Directory includes PCSX2 subfolder");
            Check(GlyphPackManager.GetGameTextureDirectory(glyphData, "SLUS-21801") ==
                Path.Combine(nestedSearch, "SLUS-21801"), "serial-scoped PCSX2 texture path");
            Check(!Directory.Exists(glyphData), "glyph plan does not create emulator directories");
            var legacyData = Path.Combine(temp, "legacy-layout");
            var legacySearch = Path.Combine(legacyData, "textures");
            Directory.CreateDirectory(legacySearch);
            Check(GlyphPackManager.GetTextureSearchDirectory(legacyData) == legacySearch,
                "older direct-textures layouts remain discoverable");
            Directory.CreateDirectory(Path.Combine(legacyData, "PCSX2"));
            Check(GlyphPackManager.GetTextureSearchDirectory(legacyData) ==
                Path.Combine(legacyData, "PCSX2", "textures"),
                "PCSX2-managed folder takes precedence over stale old installer path");
            var customSearch = Path.Combine(temp, "user-configured-search");
            Check(GlyphPackManager.GetTextureSearchDirectory(glyphData, customSearch) ==
                customSearch, "explicit PCSX2 Search Directory wins over guessing");
            Check(!Directory.Exists(customSearch), "search path resolution is read-only");
            var emptyPackPreview = GlyphPackManager.Inspect(glyphFolder, "SLUS-21801", glyphData);
            Check(emptyPackPreview.NewFiles == 1 && emptyPackPreview.ConflictingFiles == 0,
                "glyph pack preview finds exactly one new replacement");
            var customPreview = GlyphPackManager.Inspect(glyphFolder, "SLUS-21801", glyphData, customSearch);
            Check(customPreview.DestinationDirectory ==
                Path.Combine(customSearch, "SLUS-21801", "replacements"),
                "custom Search Directory maps to exact PCSX2 replacement path");
            Check(!Directory.Exists(customSearch), "custom pack preview does not create folders");
            Check(!Directory.Exists(glyphData), "glyph pack preview is read-only");
            ExpectThrows<InvalidDataException>(() => GlyphPackManager.Inspect(glyphFolder, "SLES-55353", glyphData),
                "US glyph pack cannot install for PAL game serial");
            var installedGlyphs = GlyphPackManager.Install(glyphFolder, "SLUS-21801", glyphData);
            Check(installedGlyphs.NewFiles == 1 && File.Exists(Path.Combine(expectedGlyphDir, glyphName)),
                "glyph pack installs only to serial-scoped replacement directory");
            Check(GlyphPackManager.Inspect(glyphFolder, "SLUS-21801", glyphData).IdenticalFiles == 1,
                "reinstall identifies identical texture files");
            Check(GlyphPackManager.Install(glyphFolder, "SLUS-21801", glyphData).NewFiles == 0,
                "identical reinstall does not replace existing data");
            var existingGlyph = Path.Combine(expectedGlyphDir, glyphName);
            File.WriteAllBytes(existingGlyph, [99, 88, 77]);
            Check(GlyphPackManager.Inspect(glyphFolder, "SLUS-21801", glyphData).ConflictingFiles == 1,
                "different pre-existing texture reported as conflict");
            ExpectThrows<IOException>(() => GlyphPackManager.Install(glyphFolder, "SLUS-21801", glyphData),
                "never overwrite existing different texture");
            Check(File.ReadAllBytes(existingGlyph).SequenceEqual(new byte[] { 99, 88, 77 }),
                "existing replacement bytes remain unchanged after conflict");
            var badGlyphName = Path.Combine(glyphReplacementFiles, "not-a-texture.exe");
            File.WriteAllText(badGlyphName, "test");
            ExpectThrows<InvalidDataException>(() => GlyphPackManager.Inspect(glyphFolder, "SLUS-21801", glyphData),
                "rejects unexpected filenames rather than copying arbitrary files");
            File.Delete(badGlyphName);
            ExpectThrows<ArgumentException>(() => GlyphPackManager.GetGameTextureDirectory(glyphData, "../wrong"),
                "invalid serial cannot traverse data directory");
            Check(!File.Exists(Path.Combine(glyphData, "PCSX2.ini")),
                "glyph pack installer does not alter PCSX2 settings");
            loaded.TextureSearchDirectory = customSearch;
            loaded.PcPromptLayout = new PcPromptLayout { Done = "F" };
            store.Save(loaded);
            Check(store.Load().TextureSearchDirectory == customSearch &&
                store.Load().PcPromptLayout.Done == "F",
                "custom search location and PC Input Lab settings persist together");
            // 0.5 PC prompt-label profile / name-entry planning (no input injection).
            var pcLabels = new PcPromptLayout();
            pcLabels.Validate();
            Check(pcLabels.Accept == "Enter" && pcLabels.Done == "F" && pcLabels.Delete == "Backspace",
                "distinct Cross, Start and Square PC labels");
            Check(pcLabels.Actions().Count == 10, "screenshot prompt actions and D-pad all represented");
            pcLabels.Done = "Enter";
            ExpectThrows<ArgumentException>(() => pcLabels.Validate(),
                "cannot accidentally assign Enter to both Cross and Start");
            pcLabels.Done = "F";
            loaded.PcPromptLayout = pcLabels;
            store.Save(loaded);
            Check(store.Load().PcPromptLayout.Accept == "Enter" &&
                  store.Load().PcPromptLayout.Done == "F", "PC label suggestions persist in launcher JSON");
            Check(NameEntryGridPlanner.Plan("A").SequenceEqual(new[] { GridAction.Accept, GridAction.Done }),
                "name A uses initial cursor and finish action");
            Check(NameEntryGridPlanner.Plan("N").SequenceEqual(new[] { GridAction.Down, GridAction.Accept, GridAction.Done }),
                "name N navigates to second alphabet row");
            Check(NameEntryGridPlanner.Plan("1").SequenceEqual(new[] { GridAction.Down, GridAction.Down, GridAction.Accept, GridAction.Done }),
                "digit 1 navigates to short numeric row");
            Check(NameEntryGridPlanner.Plan("9Z").SequenceEqual(
                new[] { GridAction.Down, GridAction.Down, GridAction.Right, GridAction.Right, GridAction.Right,
                    GridAction.Right, GridAction.Right, GridAction.Right, GridAction.Right, GridAction.Right,
                    GridAction.Accept, GridAction.Up, GridAction.Right, GridAction.Right,
                    GridAction.Right, GridAction.Right, GridAction.Accept, GridAction.Done }),
                "planner exits short digit row before moving to column 12");
            ExpectThrows<ArgumentException>(() => NameEntryGridPlanner.Plan("NAME WITH SPACES"),
                "uncaptured space tile is not silently invented");
            Check(!File.Exists(Path.Combine(glyphData, "input-bindings.ini")),
                "PC prompt suggestions do not edit PCSX2 input bindings");
            // 0.6: manager only touches its own hashes, supports switching and explicit adoption.
            var managedData = Path.Combine(temp, "managed-theme-data");
            var themeA = Path.Combine(temp, "glyph-theme-A");
            var themeB = Path.Combine(temp, "glyph-theme-B");
            var sourceA = Path.Combine(themeA, "replacements");
            var sourceB = Path.Combine(themeB, "replacements");
            Directory.CreateDirectory(sourceA);
            Directory.CreateDirectory(sourceB);
            File.WriteAllText(Path.Combine(themeA, "glyph-pack.json"),
                """{"schemaVersion":1,"id":"theme-a","name":"Theme A","gameSerial":"SLUS-21801","inputProfile":"keyboard"}""");
            File.WriteAllText(Path.Combine(themeB, "glyph-pack.json"),
                """{"schemaVersion":1,"id":"theme-b","name":"Theme B","gameSerial":"SLUS-21801","inputProfile":"keyboard"}""");
            var sharedIcon = "1111111111111111-00001554.png";
            var oldOnlyIcon = "2222222222222222-00001554.png";
            var newOnlyIcon = "3333333333333333-00001554.png";
            File.WriteAllBytes(Path.Combine(sourceA, sharedIcon), [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(sourceA, oldOnlyIcon), [2, 4, 6]);
            File.WriteAllBytes(Path.Combine(sourceB, sharedIcon), [9, 8, 7]);
            File.WriteAllBytes(Path.Combine(sourceB, newOnlyIcon), [3, 6, 9]);
            var themeRoot = GlyphPackManager.GetGameTextureDirectory(managedData, "SLUS-21801");
            var actualReplacements = Path.Combine(themeRoot, "replacements");
            var initialPlan = ManagedGlyphThemes.Preview(themeA, "SLUS-21801", managedData);
            Check(initialPlan.CanApply && initialPlan.NewFiles == 2 && initialPlan.IdenticalUnmanaged == 0,
                "theme preview counts new files without touching PCSX2 data");
            Check(!Directory.Exists(managedData), "theme preview is read-only");
            var activateA = ManagedGlyphThemes.Activate(themeA, "SLUS-21801", managedData);
            Check(activateA.Changed == 2 && ManagedGlyphThemes.ActivePack("SLUS-21801", managedData) == "Theme A",
                "first theme activation tracks installed glyphs");
            Check(File.Exists(Path.Combine(actualReplacements, oldOnlyIcon)),
                "first theme files appear under correct PCSX2 search root");
            var planB = ManagedGlyphThemes.Preview(themeB, "SLUS-21801", managedData);
            Check(planB.CanApply && planB.ReplaceManaged == 1 &&
                planB.RemoveManaged == 1 && planB.NewFiles == 1,
                "theme switch preview counts update, removal and new file");
            var activateB = ManagedGlyphThemes.Activate(themeB, "SLUS-21801", managedData);
            Check(activateB.Changed == 3 && ManagedGlyphThemes.ActivePack("SLUS-21801", managedData) == "Theme B",
                "switch activates selected theme without external file changes");
            Check(!File.Exists(Path.Combine(actualReplacements, oldOnlyIcon)) &&
                File.ReadAllBytes(Path.Combine(actualReplacements, sharedIcon)).SequenceEqual(new byte[] { 9, 8, 7 }) &&
                File.Exists(Path.Combine(actualReplacements, newOnlyIcon)),
                "old icons are removed, shared icons replaced and new icons added");
            var unrelatedIcon = "4444444444444444-00001554.png";
            File.WriteAllBytes(Path.Combine(actualReplacements, unrelatedIcon), [55, 66, 77]);
            File.WriteAllBytes(Path.Combine(actualReplacements, sharedIcon), [66, 55, 44]);
            Check(ManagedGlyphThemes.Preview(themeA, "SLUS-21801", managedData).ModifiedManaged == 1,
                "external modification of a tracked icon is reported");
            ExpectThrows<IOException>(() => ManagedGlyphThemes.Deactivate("SLUS-21801", managedData),
                "deactivation refuses to delete a modified managed glyph");
            Check(File.ReadAllBytes(Path.Combine(actualReplacements, sharedIcon)).SequenceEqual(new byte[] { 66, 55, 44 }),
                "modified external file remains unchanged after refusal");
            File.WriteAllBytes(Path.Combine(actualReplacements, sharedIcon), [9, 8, 7]);
            var disabledB = ManagedGlyphThemes.Deactivate("SLUS-21801", managedData);
            Check(disabledB.Changed == 2 && ManagedGlyphThemes.ActivePack("SLUS-21801", managedData) is null,
                "disable removes only tracked glyphs and clears active state");
            Check(File.ReadAllBytes(Path.Combine(actualReplacements, unrelatedIcon)).SequenceEqual(new byte[] { 55, 66, 77 }),
                "disable preserves unrelated replacement textures");
            // Older Undercover Lite already installed W keycaps with no ownership state.
            File.Copy(Path.Combine(sourceA, sharedIcon), Path.Combine(actualReplacements, sharedIcon));
            var adoptPlan = ManagedGlyphThemes.Preview(themeA, "SLUS-21801", managedData);
            Check(adoptPlan.IdenticalUnmanaged == 1 && adoptPlan.NewFiles == 1,
                "existing byte-identical legacy glyph is identified as adoptable");
            ExpectThrows<InvalidOperationException>(() => ManagedGlyphThemes.Activate(themeA, "SLUS-21801", managedData),
                "legacy glyph cannot be silently adopted");
            var adopted = ManagedGlyphThemes.Activate(themeA, "SLUS-21801", managedData,
                adoptIdenticalUnmanaged: true);
            Check(adopted.Changed == 1 && ManagedGlyphThemes.ActivePack("SLUS-21801", managedData) == "Theme A",
                "explicit consent adopts identical old glyph and installs remaining icon");
            ManagedGlyphThemes.Deactivate("SLUS-21801", managedData);
            Check(!File.Exists(Path.Combine(actualReplacements, sharedIcon)) &&
                File.Exists(Path.Combine(actualReplacements, unrelatedIcon)),
                "adopted glyph can be disabled while unrelated artwork survives");
            File.WriteAllBytes(Path.Combine(actualReplacements, sharedIcon), [4, 5, 6]);
            Check(ManagedGlyphThemes.Preview(themeA, "SLUS-21801", managedData).ForeignConflicts == 1,
                "nonidentical unmanaged glyph is a hard conflict");
            ExpectThrows<IOException>(() => ManagedGlyphThemes.Activate(themeA, "SLUS-21801", managedData,
                adoptIdenticalUnmanaged: true), "cannot overwrite a foreign differing glyph even with adoption");
            Check(File.ReadAllBytes(Path.Combine(actualReplacements, sharedIcon)).SequenceEqual(new byte[] { 4, 5, 6 }),
                "conflicting foreign icon bytes preserved");
            Check(ManagedGlyphThemes.ActivePack("SLUS-21801", managedData) is null,
                "foreign conflict does not leave behind an active managed state");
            // 0.8: read-only setup diagnosis, privacy-safe report, and real texture folder regression.
            var doctorData = Path.Combine(temp, "doctor-pcsx2-data");
            var missingReport = SetupDoctor.Check(new LauncherSettings(), doctorData);
            Check(missingReport.Blockers == 2 &&
                  missingReport.Findings.Any(f => f.Topic == "PCSX2 executable" &&
                      f.Level == SetupDoctor.Severity.Blocked) &&
                  missingReport.Findings.Any(f => f.Topic == "Game image" &&
                      f.Level == SetupDoctor.Severity.Blocked),
                "Setup Doctor blocks missing emulator and game selections");
            Check(!Directory.Exists(doctorData), "read-only diagnostics do not create PCSX2 data");
            Check(!missingReport.CopyableReport().Contains(temp, StringComparison.OrdinalIgnoreCase),
                "shareable diagnostic report does not leak filesystem paths");

            var healthy = new LauncherSettings
            {
                Pcsx2Executable = exe, GameImage = validDisc, IsolatePcsx2Data = true
            };
            // A nonexistent fake exe named "PCSX2 with spaces" is still checked by basename, not executed.
            var foundReport = SetupDoctor.Check(healthy, doctorData);
            Check(foundReport.Blockers == 0 && foundReport.VerifiedSerial == "SLUS-21801",
                "Setup Doctor recognizes valid ISO without launching emulator");
            var unexpectedExecutable = Path.Combine(temp, "something-else.exe");
            File.WriteAllBytes(unexpectedExecutable, Array.Empty<byte>());
            healthy.Pcsx2Executable = unexpectedExecutable;
            Check(SetupDoctor.Check(healthy, doctorData).Findings.Any(f =>
                f.Topic == "PCSX2 executable" &&
                f.Level == SetupDoctor.Severity.ActionNeeded),
                "nonstandard executable name gives an actionable warning");
            var reportText = foundReport.CopyableReport();
            Check(reportText.Contains("SLUS-21801") && !reportText.Contains(temp, StringComparison.OrdinalIgnoreCase) &&
                  !reportText.Contains(Path.GetDirectoryName(validDisc)!, StringComparison.OrdinalIgnoreCase),
                "copyable report includes serial but omits absolute game path");
            healthy.Pcsx2Executable = portableExe;
            var qtReport = SetupDoctor.Check(healthy, doctorData);
            Check(qtReport.Findings.Any(f => f.Topic == "PCSX2 executable" &&
                      f.Level == SetupDoctor.Severity.Ready),
                "correct pcsx2-qt.exe name and existing file are detected");
            healthy.GameImage = compressed;
            Check(SetupDoctor.Check(healthy, doctorData).Findings.Any(f =>
                f.Topic == "Game identification" && f.Level == SetupDoctor.Severity.ActionNeeded),
                "CHD remains launchable but cannot be claimed as verified");
            var differentIso = Path.Combine(temp, "other-test.iso");
            WriteSyntheticIso(differentIso, "BOOT2 = cdrom0:\\SLUS_212.67;1\n");
            healthy.GameImage = differentIso;
            Check(SetupDoctor.Check(healthy, doctorData).Findings.Any(f =>
                f.Topic == "Game identification" && f.Level == SetupDoctor.Severity.Blocked),
                "Setup Doctor catches a confirmed different game");
            healthy.GameImage = validDisc;

            var doctorSearch = Path.Combine(doctorData, "PCSX2", "textures");
            var doctorReplacement = Path.Combine(doctorSearch, "SLUS-21801", "replacements");
            Directory.CreateDirectory(doctorReplacement);
            File.WriteAllBytes(Path.Combine(doctorReplacement,
                "fa7c4c608af78f38-99e1a7bd2d4d93c5-00001554.png"), [137, 80, 78, 71]);
            File.WriteAllBytes(Path.Combine(doctorReplacement, "abcdefabcdefabcd-00001554.png"), [1, 2, 3]);
            var atlasReport = SetupDoctor.Check(healthy, doctorData);
            Check(atlasReport.Findings.Any(f => f.Topic == "Replacement textures" &&
                f.Detail.Contains("2 PNG/DDS files; 1 match")),
                "correct isolated 2.8.2 nested path detects actual Undercover glyph hash");
            var legacyTextureDir = Path.Combine(doctorData, "textures");
            Directory.CreateDirectory(legacyTextureDir);
            File.WriteAllBytes(Path.Combine(legacyTextureDir, "misplaced.png"), [1, 2, 3]);
            Check(SetupDoctor.Check(healthy, doctorData).Findings.Any(f =>
                f.Topic == "Legacy texture directory" && f.Level == SetupDoctor.Severity.ActionNeeded),
                "read-only doctor flags stale direct-textures installer layout");
            healthy.TextureSearchDirectory = Path.Combine(temp, "custom-emulator-texture-dir");
            Check(SetupDoctor.Check(healthy, doctorData).Findings.Any(f =>
                f.Topic == "Texture search directory" && f.Level == SetupDoctor.Severity.Notice),
                "explicit but missing custom texture directory produces a notice");
            healthy.IsolatePcsx2Data = false;
            Check(SetupDoctor.Check(healthy, doctorData).Findings.Any(f =>
                f.Topic == "PCSX2 profile" && f.Level == SetupDoctor.Severity.ActionNeeded),
                "non-isolated profile warns that PCSX2 texture location requires manual verification");
            Check(!Directory.Exists(healthy.TextureSearchDirectory),
                "diagnostics don't create explicitly selected texture folders");
            // 0.9: real PCSX2 Qt input profile generation, without changing shared mappings.
            var inputProfileData = Path.Combine(temp, "qt-profile-test");
            var name = UndercoverKeyboardProfile.FileName;
            var first = UndercoverKeyboardProfile.Inspect(inputProfileData);
            Check(first.Status == UndercoverKeyboardProfile.InstallStatus.New &&
                  first.DestinationFile == Path.Combine(inputProfileData, "PCSX2", "inputprofiles", name),
                "driving-first profile resolves isolated PCSX2 2.8.2 path");
            Check(!Directory.Exists(inputProfileData),
                "previewing keyboard profile does not create or modify emulator folders");
            var bindings = UndercoverKeyboardProfile.GetBindings();
            Check(bindings.Count == 22 && bindings.Select(x => x.Ps2).Distinct().Count() == bindings.Count,
                "all PS2 controls use unique profile INI fields");
            Check(bindings.Select(x => x.PcKey).Distinct().Count() == bindings.Count,
                "driving-first preset avoids duplicate host keys");
            var rendered = UndercoverKeyboardProfile.RenderIni();
            Check(rendered.Contains("[Pad1]\nType = DualShock2") &&
                  rendered.Contains("Cross = Keyboard/W") &&
                  rendered.Contains("Circle = Keyboard/S") &&
                  rendered.Contains("Triangle = Keyboard/Space") &&
                  rendered.Contains("Square = Keyboard/X") &&
                  rendered.Contains("R2 = Keyboard/Shift") &&
                  rendered.Contains("LLeft = Keyboard/A") &&
                  rendered.Contains("LRight = Keyboard/D") &&
                  rendered.Contains("Up = Keyboard/Up") &&
                  rendered.Contains("Start = Keyboard/F"),
                "profile encodes verified Undercover driving-first PS2 mappings");
            var profileSettings = new LauncherSettings { IsolatePcsx2Data = true };
            var installed = UndercoverKeyboardProfile.Install(profileSettings, inputProfileData);
            Check(installed.Status == UndercoverKeyboardProfile.InstallStatus.AlreadyInstalled,
                "new keyboard profile is created and can be read back");
            Check(File.ReadAllText(installed.DestinationFile) == rendered,
                "generated PCSX2 keyboard profile round-trips exact UTF-8 INI contents");
            Check(!File.Exists(Path.Combine(inputProfileData, "PCSX2", "inis", "PCSX2.ini")),
                "installing input profile never changes shared PCSX2.ini");
            Check(UndercoverKeyboardProfile.Inspect(inputProfileData).Status ==
                  UndercoverKeyboardProfile.InstallStatus.AlreadyInstalled,
                "already installed keyboard profile detected by content");
            var originalBytes = File.ReadAllBytes(installed.DestinationFile);
            UndercoverKeyboardProfile.Install(profileSettings, inputProfileData);
            Check(File.ReadAllBytes(installed.DestinationFile).SequenceEqual(originalBytes),
                "idempotent installation preserves existing identical profile bytes");
            File.WriteAllText(installed.DestinationFile, "# user modified profile\n");
            Check(UndercoverKeyboardProfile.Inspect(inputProfileData).Status ==
                  UndercoverKeyboardProfile.InstallStatus.Conflict,
                "modified profile recognized as a conflict");
            ExpectThrows<IOException>(() =>
                UndercoverKeyboardProfile.Install(profileSettings, inputProfileData),
                "profile installer refuses to overwrite modified user settings");
            Check(File.ReadAllText(installed.DestinationFile) == "# user modified profile\n",
                "nonidentical user's profile is left completely untouched");
            var externalSettings = new LauncherSettings { IsolatePcsx2Data = false };
            var externalDir = Path.Combine(temp, "another-emulator-data");
            ExpectThrows<InvalidOperationException>(() =>
                UndercoverKeyboardProfile.Install(externalSettings, externalDir),
                "profile auto-installer refuses to alter non-isolated external PCSX2 data");
            Check(!Directory.Exists(externalDir), "refusal does not create external profile data");
            var legacyProfileData = Path.Combine(temp, "legacy-profile-layout");
            Directory.CreateDirectory(Path.Combine(legacyProfileData, "inputprofiles"));
            Check(UndercoverKeyboardProfile.GetProfileDirectory(legacyProfileData) ==
                  Path.Combine(legacyProfileData, "inputprofiles"),
                "old direct inputprofiles layout remains supported");
            Directory.CreateDirectory(Path.Combine(legacyProfileData, "PCSX2"));
            Check(UndercoverKeyboardProfile.GetProfileDirectory(legacyProfileData) ==
                  Path.Combine(legacyProfileData, "PCSX2", "inputprofiles"),
                "nested PCSX2 profile directory wins when PCSX2 subdirectory exists");
            // 0.10: official 2.8.2 per-binding half-axis scale + repeated INI bindings.
            var steeringData = Path.Combine(temp, "pcsx2-steering-test");
            var gentle = new SteeringProfiles.Choice(SteeringProfiles.Strength.Gentle, false);
            var balancedMenu = new SteeringProfiles.Choice(SteeringProfiles.Strength.Balanced, true);
            var full = new SteeringProfiles.Choice(SteeringProfiles.Strength.Full, false);
            Check(SteeringProfiles.Percent(gentle.Steering) == 65 &&
                  SteeringProfiles.Percent(balancedMenu.Steering) == 85 &&
                  SteeringProfiles.Percent(full.Steering) == 100,
                "steering strength variants represent real 65/85/100 percent half-axis scales");
            Check(gentle.FileName != full.FileName &&
                  gentle.FileName != balancedMenu.FileName,
                "steering strength presets use non-colliding profile file names");
            var iniGentle = SteeringProfiles.RenderIni(gentle);
            Check(iniGentle.Contains("AxisScale = 1.000000\nLLeftScale = 0.65\nLRightScale = 0.65") &&
                  iniGentle.Contains("Cross = Keyboard/W") &&
                  !iniGentle.Contains("Cross = Keyboard/Return"),
                "gentle preset scales only PS2 left-stick steering and keeps base menu bindings");
            var iniMenu = SteeringProfiles.RenderIni(balancedMenu);
            Check(iniMenu.Contains("LLeftScale = 0.85\nLRightScale = 0.85") &&
                  iniMenu.Contains("Cross = Keyboard/W\nCross = Keyboard/Return") &&
                  iniMenu.Contains("Triangle = Keyboard/Space\nTriangle = Keyboard/Backspace"),
                "menu-friendly variant adds legitimate second PC input to Cross and Triangle");
            Check(iniMenu.Split('\n').Count(line => line.StartsWith("Cross = ")) == 2 &&
                  iniMenu.Split('\n').Count(line => line.StartsWith("Triangle = ")) == 2,
                "INI generated as repeated keys (not accidental chord requiring simultaneous presses)");
            Check(SteeringProfiles.RenderIni(full).Contains("LLeftScale = 1.00\nLRightScale = 1.00"),
                "full-strength variant restores unscaled keyboard steering");
            var freshSteering = SteeringProfiles.Inspect(steeringData, balancedMenu);
            Check(freshSteering.Status == UndercoverKeyboardProfile.InstallStatus.New &&
                  freshSteering.Destination.EndsWith(balancedMenu.FileName),
                "steering preset preview selects a unique input profile");
            Check(!Directory.Exists(steeringData), "steering preview does not create files");
            var installedSteer = SteeringProfiles.Install(
                new LauncherSettings { IsolatePcsx2Data = true }, steeringData, balancedMenu);
            Check(installedSteer.Status == UndercoverKeyboardProfile.InstallStatus.AlreadyInstalled &&
                  File.ReadAllText(installedSteer.Destination) == iniMenu,
                "selected steering profile installs without altering shared controls");
            var otherSteer = SteeringProfiles.Install(
                new LauncherSettings { IsolatePcsx2Data = true }, steeringData, gentle);
            Check(File.Exists(otherSteer.Destination) &&
                  File.ReadAllText(installedSteer.Destination) == iniMenu,
                "installing a different strength creates separate profile without changing first");
            File.WriteAllText(installedSteer.Destination, "# custom user controller layout\n");
            Check(SteeringProfiles.Inspect(steeringData, balancedMenu).Status ==
                  UndercoverKeyboardProfile.InstallStatus.Conflict,
                "different existing profile is treated as user-owned conflict");
            ExpectThrows<IOException>(() =>
                SteeringProfiles.Install(new LauncherSettings { IsolatePcsx2Data = true }, steeringData, balancedMenu),
                "steering installer refuses to overwrite conflicting input profile");
            Check(File.ReadAllText(installedSteer.Destination) == "# custom user controller layout\n",
                "conflicting user profile remains unchanged");
            var nonIsolatedSteer = Path.Combine(temp, "steer-external-data");
            ExpectThrows<InvalidOperationException>(() =>
                SteeringProfiles.Install(new LauncherSettings { IsolatePcsx2Data = false }, nonIsolatedSteer, gentle),
                "steering presets never silently change nonisolated PCSX2 data");
            Check(!Directory.Exists(nonIsolatedSteer),
                "external profile directory is untouched after refusal");

            // Current PCSX2 2.8.2 source maps Space to TogglePause by default.
            var emptyAuditData = Path.Combine(temp, "no-pcsx2-settings-yet");
            var defaultAudit = Pcsx2HotkeyAudit.Scan(emptyAuditData);
            Check(defaultAudit.Findings.Any(f => f.Description.Contains("Space")) &&
                  defaultAudit.Findings.Any(f => f.Description.Contains("Escape")) &&
                  !Directory.Exists(emptyAuditData),
                "hotkey auditor warns about default Space/Escape conflicts without creating config");
            var iniSettings = Path.Combine(steeringData, "PCSX2", "inis", "PCSX2.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(iniSettings)!);
            File.WriteAllText(iniSettings,
                "[Hotkeys]\nTogglePause = Keyboard/Space\nOpenPauseMenu = Keyboard/Escape\n");
            var conflicts = Pcsx2HotkeyAudit.Scan(steeringData);
            Check(conflicts.Findings.Any(f => f.Description.Contains("Conflict confirmed")) &&
                  conflicts.Findings.Any(f => f.Description.Contains("Escape opens")),
                "local PCSX2 hotkey audit detects a direct Space handbrake/emulator conflict");
            File.WriteAllText(iniSettings,
                "[Hotkeys]\nTogglePause = Keyboard/F12\nOpenPauseMenu = Keyboard/F11\n");
            var corrected = Pcsx2HotkeyAudit.Scan(steeringData);
            Check(corrected.Findings.All(f => !f.Description.Contains("Conflict confirmed")),
                "auditor recognizes customized emulator hotkeys without modifying them");
            // 0.11: profile-to-glyph audit differentiates known-wrong old
            // controller art from freshly generated input-aligned themes.
            var auditData = Path.Combine(temp, "profile-glyph-alignment");
            var auditProfileDirectory = UndercoverKeyboardProfile.GetProfileDirectory(auditData);
            Directory.CreateDirectory(auditProfileDirectory);
            var auditIni = Path.Combine(auditProfileDirectory, UndercoverKeyboardProfile.FileName);
            File.WriteAllText(auditIni, UndercoverKeyboardProfile.RenderIni());
            var noTheme = InputAlignmentAudit.Inspect(auditData, "SLUS-21801");
            Check(noTheme.ExpectedProfileExists && noTheme.ExpectedProfileUnmodified &&
                  noTheme.ThemeKnownMatched is null,
                "alignment check reads exact expected input profile but does not invent an active glyph theme");
            var auditOldPack = Path.Combine(temp, "audit-old-atlas-pack");
            var auditNewPack = Path.Combine(temp, "audit-new-correct-pack");
            foreach (var folder in new[] { auditOldPack, auditNewPack })
            {
                Directory.CreateDirectory(Path.Combine(folder, "replacements"));
                File.WriteAllBytes(Path.Combine(folder, "replacements", "1111111111111111-00001554.png"),
                    [1, 2, 3]);
            }
            File.WriteAllText(Path.Combine(auditOldPack, "glyph-pack.json"),
                """{"schemaVersion":1,"id":"audit-old-atlas","name":"Undercover Lite PC driving-first + keyboard diagram [SLUS-21801]","gameSerial":"SLUS-21801"}""");
            File.WriteAllText(Path.Combine(auditNewPack, "glyph-pack.json"),
                """{"schemaVersion":1,"id":"audit-new-aligned","name":"Undercover Lite PC matched driving keys 0.11 [SLUS-21801]","gameSerial":"SLUS-21801"}""");
            var auditSettings = new LauncherSettings { IsolatePcsx2Data = true };
            ManagedGlyphThemes.Activate(auditOldPack, "SLUS-21801", auditData);
            var oldAudit = InputAlignmentAudit.Inspect(auditData, "SLUS-21801");
            Check(oldAudit.ThemeKnownMatched == false &&
                  oldAudit.Message.Contains("incorrect key labels"),
                "old 0.7 registered atlas theme is clearly flagged as mismatched");
            ManagedGlyphThemes.Activate(auditNewPack, "SLUS-21801", auditData);
            var correctedAudit = InputAlignmentAudit.Inspect(auditData, "SLUS-21801");
            Check(correctedAudit.ThemeKnownMatched == true &&
                  correctedAudit.Message.Contains("Active PCSX2 selection and actual texture pixels are not verified"),
                "new 0.11 declared mapping and intact input profile align without claiming gameplay verification");
            File.WriteAllText(auditIni, "# overridden player profile\n");
            Check(InputAlignmentAudit.Inspect(auditData, "SLUS-21801").ThemeKnownMatched == false,
                "modified input profile invalidates declared image-to-key alignment");
            var auditDoctor = SetupDoctor.Check(new LauncherSettings(), auditData);
            Check(auditDoctor.Findings.Any(f => f.Topic == "Input/icon alignment" &&
                  f.Level == SetupDoctor.Severity.Notice),
                "Setup Doctor reports input alignment even without a known ISO serial");
            Check(!auditDoctor.CopyableReport().Contains(auditData, StringComparison.OrdinalIgnoreCase),
                "safe Setup Doctor report does not expose input-profile or glyph-install paths");
            // UI-1: PC Settings edits are delta-merged into *freshly loaded*
            // launcher.json, never replacing fields owned by other windows.
            var hubBaseline = new LauncherSettings
            {
                Fullscreen = true, FastBoot = true, HideEmulatorWindow = true,
                IsolatePcsx2Data = true, EnableCompanionHotkey = true,
                GameImage = "undercover-original.iso",
                Pcsx2Executable = "pcsx2-original.exe",
                TextureSearchDirectory = "textures-original",
                PcPromptLayout = new PcPromptLayout { Accept = "Enter", Done = "F" }
            };
            var hubOriginal = new LauncherSettings
            {
                Fullscreen = true, FastBoot = true, HideEmulatorWindow = true,
                IsolatePcsx2Data = true, EnableCompanionHotkey = true,
                GameImage = "undercover-original.iso",
                Pcsx2Executable = "pcsx2-original.exe",
                TextureSearchDirectory = "textures-original",
                PcPromptLayout = new PcPromptLayout { Accept = "Enter", Done = "F" }
            };
            Check(!SettingsHubPatch.HasChanges(hubBaseline, hubOriginal),
                "settings hub detects no pending edits before changing controls");
            hubOriginal.Fullscreen = false;
            hubOriginal.FastBoot = false;
            hubOriginal.Pcsx2Executable = "pcsx2-new.exe";
            hubOriginal.GameImage = "different-user-game.iso";
            Check(SettingsHubPatch.HasChanges(hubBaseline, hubOriginal),
                "display mode, fast boot and path changes mark the settings hub as dirty");
            var latestFromOtherTool = new LauncherSettings
            {
                Fullscreen = true, FastBoot = true, HideEmulatorWindow = true,
                IsolatePcsx2Data = true, EnableCompanionHotkey = true,
                Pcsx2Executable = "pcsx2-original.exe", GameImage = "undercover-original.iso",
                TextureSearchDirectory = "new-search-folder-from-glyph-manager",
                PcPromptLayout = new PcPromptLayout { Accept = "W", Back = "Space", Done = "F" }
            };
            var originalLabels = latestFromOtherTool.PcPromptLayout;
            var mergedFromHub = SettingsHubPatch.Merge(latestFromOtherTool, hubBaseline, hubOriginal);
            Check(!mergedFromHub.Fullscreen && !mergedFromHub.FastBoot &&
                  mergedFromHub.HideEmulatorWindow && mergedFromHub.IsolatePcsx2Data,
                "settings hub changes only explicitly edited launcher toggles");
            Check(mergedFromHub.Pcsx2Executable == "pcsx2-new.exe" &&
                  mergedFromHub.GameImage == "different-user-game.iso",
                "settings hub applies both edited path fields");
            Check(mergedFromHub.TextureSearchDirectory == "new-search-folder-from-glyph-manager",
                "settings hub preserves a separately saved texture search directory");
            Check(ReferenceEquals(mergedFromHub.PcPromptLayout, originalLabels) &&
                  mergedFromHub.PcPromptLayout.Accept == "W" &&
                  mergedFromHub.PcPromptLayout.Back == "Space",
                "settings hub does not reset name-entry key labels edited in another window");
            var secondHubEdit = new LauncherSettings
            {
                Fullscreen = true, FastBoot = true,
                HideEmulatorWindow = false, IsolatePcsx2Data = false,
                EnableCompanionHotkey = false,
                Pcsx2Executable = "pcsx2-original.exe", GameImage = "undercover-original.iso",
                TextureSearchDirectory = "new-texture-folder",
            };
            var secondMerged = SettingsHubPatch.Merge(new LauncherSettings
            {
                TextureSearchDirectory = "texture-folder-in-between",
                GameImage = "externally-updated.iso", PcPromptLayout = new PcPromptLayout { Done = "R" }
            }, hubBaseline, secondHubEdit);
            Check(secondMerged.GameImage == "externally-updated.iso" &&
                  secondMerged.TextureSearchDirectory == "new-texture-folder" &&
                  !secondMerged.HideEmulatorWindow &&
                  !secondMerged.IsolatePcsx2Data &&
                  !secondMerged.EnableCompanionHotkey &&
                  secondMerged.PcPromptLayout.Done == "R",
                "settings hub merges edited fields while retaining nonedited external values");
            var settingsHubStore = new LauncherSettingsStore(Path.Combine(temp, "settings-hub-test"));
            settingsHubStore.Save(mergedFromHub);
            var persistedHub = settingsHubStore.Load();
            Check(!persistedHub.Fullscreen && persistedHub.Pcsx2Executable == "pcsx2-new.exe" &&
                  persistedHub.TextureSearchDirectory == "new-search-folder-from-glyph-manager" &&
                  persistedHub.PcPromptLayout.Accept == "W",
                "settings hub delta persists safely through real launcher JSON store");
            ExpectThrows<ArgumentNullException>(() =>
                SettingsHubPatch.Merge(null!, hubBaseline, hubOriginal),
                "settings hub refuses null target instead of destroying configuration");
            // UI-2: edits the verified isolated PCSX2 Qt GS global key only,
            // keeping every unrelated setting, with a full-byte backup.
            var gsData = Path.Combine(temp, "graphics-adapter-test");
            var gsIni = Path.Combine(gsData, "PCSX2", "inis", "PCSX2.ini");
            var gsSettings = new LauncherSettings { IsolatePcsx2Data = true };
            Check(Pcsx2GraphicsAdapter.FindExistingIni(gsData) is null &&
                  !Directory.Exists(gsData),
                "PCSX2 graphics discovery never creates emulator files");
            ExpectThrows<FileNotFoundException>(() =>
                Pcsx2GraphicsAdapter.Inspect(gsSettings, gsData),
                "graphics editor refuses to invent PCSX2.ini before emulator setup");
            Directory.CreateDirectory(Path.GetDirectoryName(gsIni)!);
            string config =
                "# sample settings\r\n[UI]\r\nLanguage = en\r\n\r\n" +
                "[EmuCore/GS]\r\nRenderer = 14\r\nupscale_multiplier = 2.0\r\n" +
                "filter = 2\r\n\r\n[Hotkeys]\r\nTogglePause = Keyboard/F12\r\n";
            File.WriteAllText(gsIni, config);
            var before = Pcsx2GraphicsAdapter.Inspect(gsSettings, gsData);
            Check(before.HasExplicitSetting && before.CurrentScale == 2f &&
                  before.FilePath == gsIni,
                "reads exact PCSX2 2.8.2 EmuCore/GS upscale_multiplier");
            Check(Pcsx2GraphicsAdapter.AllowedMultipliers.SequenceEqual([1, 2, 3, 4, 6]),
                "graphics UI offers only conservative supported scale values");
            string backup = Pcsx2GraphicsAdapter.Apply(gsSettings, gsData, before, 3);
            string afterIni = File.ReadAllText(gsIni);
            Check(File.Exists(backup) && File.ReadAllText(backup) == config,
                "original PCSX2.ini is preserved byte-for-byte in a backup");
            Check(afterIni.Contains("upscale_multiplier = 3.0\r\n") &&
                  afterIni.Contains("[UI]\r\nLanguage = en\r\n") &&
                  afterIni.Contains("Renderer = 14\r\n") &&
                  afterIni.Contains("filter = 2\r\n") &&
                  afterIni.Contains("[Hotkeys]\r\nTogglePause = Keyboard/F12\r\n"),
                "only GS multiplier is updated while preserving other options and CRLF");
            var newSnapshot = Pcsx2GraphicsAdapter.Inspect(gsSettings, gsData);
            Check(newSnapshot.CurrentScale == 3f &&
                  Pcsx2GraphicsAdapter.Apply(gsSettings, gsData, newSnapshot, 3) == "",
                "unchanged graphics setting is a no-op without additional backup");
            ExpectThrows<IOException>(() =>
                Pcsx2GraphicsAdapter.Apply(gsSettings, gsData, before, 4),
                "stale preview is refused to avoid clobbering PCSX2 external edits");
            File.AppendAllText(gsIni, "; user-customized\r\n");
            ExpectThrows<IOException>(() =>
                Pcsx2GraphicsAdapter.Apply(gsSettings, gsData, newSnapshot, 4),
                "concurrent customization makes adapter refuse write");
            Check(File.ReadAllText(gsIni).Contains("; user-customized"),
                "refused graphics write keeps concurrent PCSX2 config changes intact");
            ExpectThrows<ArgumentOutOfRangeException>(() =>
                Pcsx2GraphicsAdapter.Apply(gsSettings, gsData, newSnapshot, 24),
                "unsafe out-of-range resolution multipliers are not applied");
            ExpectThrows<InvalidOperationException>(() =>
                Pcsx2GraphicsAdapter.Inspect(new LauncherSettings { IsolatePcsx2Data = false }, gsData),
                "shared non-isolated PCSX2 configuration is not accessed by automatic adapter");
            var gsMissingSection = Path.Combine(temp, "graphics-missing-section");
            var missingIni = Path.Combine(gsMissingSection, "inis", "PCSX2.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(missingIni)!);
            File.WriteAllText(missingIni, "[UI]\nSetupWizardIncomplete = false\n");
            var defaultSnapshot = Pcsx2GraphicsAdapter.Inspect(gsSettings, gsMissingSection);
            Check(defaultSnapshot.CurrentScale == 1f && !defaultSnapshot.HasExplicitSetting,
                "missing GS multiplier is interpreted as PCSX2 default 1x without editing");
            Pcsx2GraphicsAdapter.Apply(gsSettings, gsMissingSection, defaultSnapshot, 4);
            Check(File.ReadAllText(missingIni).Contains("[EmuCore/GS]\nupscale_multiplier = 4.0\n"),
                "only missing graphics section/key appended to an existing PCSX2 config");
            var gsBomData = Path.Combine(temp, "graphics-utf8-bom");
            var gsBomPath = Path.Combine(gsBomData, "PCSX2", "inis", "PCSX2.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(gsBomPath)!);
            File.WriteAllBytes(gsBomPath, System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes("[EmuCore/GS]\nupscale_multiplier = 2.0\n[UI]\nLanguage = en\n")).ToArray());
            var gsBomBefore = Pcsx2GraphicsAdapter.Inspect(gsSettings, gsBomData);
            Check(gsBomBefore.CurrentScale == 2f, "UTF-8 BOM before first GS section is recognized");
            Pcsx2GraphicsAdapter.Apply(gsSettings, gsBomData, gsBomBefore, 6);
            var bomWritten = File.ReadAllBytes(gsBomPath);
            Check(bomWritten.Take(3).SequenceEqual(new byte[] { 239, 187, 191 }) &&
                  File.ReadAllText(gsBomPath).Contains("upscale_multiplier = 6.0") &&
                  File.ReadAllText(gsBomPath).Split("[EmuCore/GS]").Length == 2,
                "graphics adapter preserves UTF8 BOM and does not duplicate first GS section");
            File.WriteAllText(gsBomPath, "[EmuCore/GS]\nupscale_multiplier = 2.0\nupscale_multiplier = 3.0\n");
            ExpectThrows<InvalidDataException>(() =>
                Pcsx2GraphicsAdapter.Inspect(gsSettings, gsBomData),
                "ambiguous duplicate multiplier keys are rejected rather than guessed");
            File.WriteAllText(gsBomPath, "[EmuCore/GS]\nupscale_multiplier = 2.0\n[EmuCore/GS]\n");
            ExpectThrows<InvalidDataException>(() =>
                Pcsx2GraphicsAdapter.Inspect(gsSettings, gsBomData),
                "duplicate graphics sections are rejected before any modification");
            var dualData = Path.Combine(temp, "graphics-double-config");
            var dualNested = Path.Combine(dualData, "PCSX2", "inis", "PCSX2.ini");
            var dualDirect = Path.Combine(dualData, "inis", "PCSX2.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(dualNested)!);
            Directory.CreateDirectory(Path.GetDirectoryName(dualDirect)!);
            File.WriteAllText(dualNested, "[UI]\n");
            File.WriteAllText(dualDirect, "[UI]\n");
            ExpectThrows<InvalidDataException>(() =>
                Pcsx2GraphicsAdapter.FindExistingIni(dualData),
                "ambiguous direct and nested PCSX2 data locations require manual verification");
            // UI-3: distinguish the saved serial+CRC game profile selection from
            // merely-installed profile files and the *live* emulator state.
            var selectionData = Path.Combine(temp, "game-input-inspection");
            var selectionSettings = new LauncherSettings { IsolatePcsx2Data = true, GameImage = validDisc };
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData).Status ==
                  Pcsx2GameInputInspector.Kind.ConfigurationMissing &&
                  !Directory.Exists(selectionData),
                "saved profile reader does not create emulator files before setup");
            Check(Pcsx2GameInputInspector.Inspect(
                new LauncherSettings { IsolatePcsx2Data = false, GameImage = validDisc },
                selectionData).Status == Pcsx2GameInputInspector.Kind.UnsupportedFolder,
                "external PCSX2 data is never silently inspected");
            Check(Pcsx2GameInputInspector.Inspect(
                new LauncherSettings { IsolatePcsx2Data = true, GameImage = compressed },
                selectionData).Status == Pcsx2GameInputInspector.Kind.UnverifiedDisc,
                "compressed disc serial remains unverified, not guessed from nearby INI names");
            var selectionIni = Path.Combine(selectionData, "PCSX2", "inis", "PCSX2.ini");
            var gameFolder = Path.Combine(selectionData, "PCSX2", "gamesettings");
            var inputFolder = Path.Combine(selectionData, "PCSX2", "inputprofiles");
            Directory.CreateDirectory(Path.GetDirectoryName(selectionIni)!);
            File.WriteAllText(selectionIni,
                "[Folders]\nGameSettings = gamesettings\nInputProfiles = inputprofiles\n" +
                "[EmuCore/GS]\nupscale_multiplier = 3.0\n");
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData).Status ==
                  Pcsx2GameInputInspector.Kind.NoGameFile,
                "no per-game directory is reported as unknown, not automatically Shared");
            Directory.CreateDirectory(gameFolder);
            Directory.CreateDirectory(inputFolder);
            var correctGameIni = Path.Combine(gameFolder, "SLUS-21801_A1B2C3D4.ini");
            File.WriteAllText(Path.Combine(gameFolder, "SLES-55353_ABCDEF01.ini"),
                "[EmuCore]\nInputProfileName = unrelated-profile\n");
            File.WriteAllText(Path.Combine(gameFolder, "1234ABCD.ini"),
                "[EmuCore]\nInputProfileName = legacy-unknown\n");
            File.WriteAllText(correctGameIni,
                "[EmuCore]\nInputProfileName = UndercoverLite-DrivingFirst\n");
            var missingSelection = Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData);
            Check(missingSelection.Status == Pcsx2GameInputInspector.Kind.SelectedProfile &&
                  missingSelection.SelectedProfile == UndercoverKeyboardProfile.Name &&
                  missingSelection.SelectedProfileFileExists == false &&
                  missingSelection.CandidateCount == 1,
                "finds exact Undercover serial+CRC INI, ignores other editions and legacy CRC-only files");
            var selectedFile = Path.Combine(inputFolder, UndercoverKeyboardProfile.FileName);
            File.WriteAllText(selectedFile, UndercoverKeyboardProfile.RenderIni());
            var knownSelection = Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData);
            Check(knownSelection.HasProvenSelection &&
                  knownSelection.RecognizedProfileMatchesSource == true &&
                  knownSelection.SelectedProfileFileExists == true &&
                  knownSelection.Summary.Contains("DISK configuration read"),
                "saved selected DrivingFirst profile matches C# mappings without claiming runtime keys");
            File.WriteAllText(selectedFile, "# changed custom keys\n");
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData)
                    .RecognizedProfileMatchesSource == false,
                "installed but changed driving profile is reported as an input mismatch");
            File.WriteAllText(correctGameIni, "[EmuCore]\nInputProfileName = \n");
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData).Status ==
                  Pcsx2GameInputInspector.Kind.SharedBindings,
                "empty per-game InputProfileName is classified as Shared controls");
            File.WriteAllText(correctGameIni, "[EmuCore]\nLanguage = en\n");
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData).Status ==
                  Pcsx2GameInputInspector.Kind.SharedBindings,
                "missing per-game InputProfileName defaults to Shared controls");
            File.WriteAllText(correctGameIni,
                "[EmuCore]\nInputProfileName = UndercoverLite-Steer85-Menu\n");
            var steerChoice = new SteeringProfiles.Choice(SteeringProfiles.Strength.Balanced, true);
            File.WriteAllText(Path.Combine(inputFolder, steerChoice.FileName),
                SteeringProfiles.RenderIni(steerChoice));
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData)
                    .RecognizedProfileMatchesSource == true,
                "saved steering and menu variant is recognized against actual generated INI source");
            File.WriteAllText(correctGameIni,
                "[EmuCore]\nInputProfileName = custom-controller\n");
            File.WriteAllText(Path.Combine(inputFolder, "custom-controller.ini"), "[Pad1]\nCross = Keyboard/T\n");
            var customSelection = Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData);
            Check(customSelection.Status == Pcsx2GameInputInspector.Kind.SelectedProfile &&
                  customSelection.RecognizedProfileMatchesSource is null,
                "custom input bindings are never incorrectly declared compatible with keyboard icon pack");
            File.WriteAllText(correctGameIni,
                "[EmuCore]\nInputProfileName = ../../other-game\n");
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData).Status ==
                  Pcsx2GameInputInspector.Kind.InvalidFile,
                "malicious or unsafe profile names are rejected without path traversal");
            File.WriteAllText(correctGameIni,
                "[EmuCore]\nInputProfileName = UndercoverLite-DrivingFirst\n");
            var otherCrc = Path.Combine(gameFolder, "SLUS-21801_11112222.ini");
            File.WriteAllText(otherCrc, "[EmuCore]\nInputProfileName = Another-Crc-Profile\n");
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData).Status ==
                  Pcsx2GameInputInspector.Kind.AmbiguousCrc,
                "multiple game CRCs for same disc serial must not be guessed");
            File.Delete(otherCrc);
            File.WriteAllText(selectionIni,
                "[Folders]\nGameSettings = ../outside-config\nInputProfiles = inputprofiles\n");
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData).Status ==
                  Pcsx2GameInputInspector.Kind.UnsupportedFolder,
                "custom game settings folder is surfaced without scanning unrelated files");
            File.WriteAllText(selectionIni,
                "[Folders]\nGameSettings = gamesettings\nInputProfiles = user-profiles\n");
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData).Status ==
                  Pcsx2GameInputInspector.Kind.UnsupportedFolder,
                "custom input profile directory is surfaced without pretending installed profile verification");
            File.WriteAllText(selectionIni,
                "[Folders]\nGameSettings = gamesettings\nInputProfiles = inputprofiles\n");
            File.WriteAllText(correctGameIni,
                "[EmuCore]\nInputProfileName = First\nInputProfileName = Second\n");
            Check(Pcsx2GameInputInspector.Inspect(selectionSettings, selectionData).Status ==
                  Pcsx2GameInputInspector.Kind.InvalidFile,
                "duplicate saved profile keys are rejected");
            File.WriteAllText(correctGameIni, "[EmuCore]\n");
            File.WriteAllText(selectedFile, UndercoverKeyboardProfile.RenderIni());
            var selectionDoctor = SetupDoctor.Check(selectionSettings, selectionData);
            Check(selectionDoctor.Findings.Any(x => x.Topic == "Saved game input profile" &&
                  x.Level == SetupDoctor.Severity.ActionNeeded),
                "Setup Doctor warns when saved Undercover game uses Shared instead of keyboard profile");
            Check(!selectionDoctor.CopyableReport().Contains(selectionData,
                    StringComparison.OrdinalIgnoreCase) &&
                  !selectionDoctor.CopyableReport().Contains(correctGameIni,
                    StringComparison.OrdinalIgnoreCase),
                "saved game profile inspection keeps absolute paths out of safe diagnostic reports");
            Console.WriteLine($"All {_passed} smoke tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { /* cleanup best-effort */ }
        }
    }
}