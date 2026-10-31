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