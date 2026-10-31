namespace UndercoverLite.Core;

/// <summary>Only Windows launcher preferences. Never stores or distributes BIOS/game bytes.</summary>
public sealed class LauncherSettings
{
    public string Pcsx2Executable { get; set; } = "";
    public string GameImage { get; set; } = "";
    public bool Fullscreen { get; set; } = true;
    public bool FastBoot { get; set; } = true;
    public bool HideEmulatorWindow { get; set; } = true;
    public bool IsolatePcsx2Data { get; set; } = true;
    public bool EnableCompanionHotkey { get; set; } = true;
    // Optional exact PCSX2 Graphics > Texture Replacement > Search Directory.
    public string TextureSearchDirectory { get; set; } = "";
    // Suggested on-screen key labels only; not applied to the emulator.
    public PcPromptLayout PcPromptLayout { get; set; } = new();
}