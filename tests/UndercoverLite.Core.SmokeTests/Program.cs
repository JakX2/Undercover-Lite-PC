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