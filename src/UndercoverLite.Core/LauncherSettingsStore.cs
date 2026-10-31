using System.Text.Json;

namespace UndercoverLite.Core;

public sealed class LauncherSettingsStore
{
    public string FilePath { get; }
    public string DataDirectory { get; }
    public string Pcsx2DataDirectory => Path.Combine(DataDirectory, "pcsx2-data");

    public LauncherSettingsStore(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UndercoverLitePC");
        FilePath = Path.Combine(DataDirectory, "launcher.json");
    }

    public LauncherSettings Load()
    {
        if (!File.Exists(FilePath)) return new LauncherSettings();
        // Invalid JSON is surfaced to the UI; don't destroy the user's configuration.
        var data = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<LauncherSettings>(data) ??
            throw new InvalidDataException("Settings file contains a JSON null value.");
    }

    public void Save(LauncherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(DataDirectory);
        string temp = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            File.Move(temp, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}