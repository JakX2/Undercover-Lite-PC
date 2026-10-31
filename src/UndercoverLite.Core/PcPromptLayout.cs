namespace UndercoverLite.Core;

/// <summary>
/// A reference for the artwork we WANT the PS2 UI to display. It does not
/// change or inspect PCSX2 bindings and must not be presented as an applied profile.
/// </summary>
public sealed class PcPromptLayout
{
    public string Accept { get; set; } = "Enter";   // Cross
    public string Back { get; set; } = "Escape";    // Triangle
    public string Delete { get; set; } = "Backspace"; // Square
    public string Done { get; set; } = "F";        // Start, separate from Cross
    public string PreviousTab { get; set; } = "Q"; // L1
    public string NextTab { get; set; } = "E";     // R1
    public string Up { get; set; } = "W";
    public string Down { get; set; } = "S";
    public string Left { get; set; } = "A";
    public string Right { get; set; } = "D";

    public static readonly string[] SupportedKeys =
    [
        "Enter", "Escape", "Backspace", "Space", "Tab", "Shift", "Ctrl",
        "Up", "Down", "Left", "Right", "W", "A", "S", "D", "Q", "E",
        "F", "R", "C", "V", "X", "Z", "I", "J", "K", "L", "U", "O",
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"
    ];

    public IReadOnlyList<(string Action, string Key)> Actions() =>
    [
        ("Cross / Accept", Accept),
        ("Triangle / Back", Back),
        ("Square / Delete", Delete),
        ("Start / Done", Done),
        ("L1 / Previous tab", PreviousTab),
        ("R1 / Next tab", NextTab),
        ("D-pad Up", Up),
        ("D-pad Down", Down),
        ("D-pad Left", Left),
        ("D-pad Right", Right)
    ];

    /// <summary>
    /// Keys must be distinct so a suggested one-to-one PS2 mapping remains
    /// meaningful. This is a preview validator, NOT a PCSX2 config writer.
    /// </summary>
    public void Validate()
    {
        var actions = Actions();
        foreach (var (action, key) in actions)
        {
            if (!SupportedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"Unsupported PC key for {action}: {key}");
        }
        var collision = actions.GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (collision is not null)
            throw new ArgumentException($"'{collision.Key}' is assigned to multiple PS2 actions. Choose distinct keys.");
    }
}