using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using UndercoverLite.Core;

namespace UndercoverLite;

/// <summary>Safe, opt-in installer for user-authored texture packs. Not a game overlay.</summary>
public sealed class GlyphPackForm : Form
{
    private readonly LauncherSettingsStore _store;
    private readonly LauncherSettings _settings;
    private readonly bool _gameRunning;
    private readonly Label _feedback = new();

    public GlyphPackForm(LauncherSettingsStore store, LauncherSettings settings, bool gameRunning)
    {
        _store = store;
        _settings = settings;
        _gameRunning = gameRunning;
        Text = "Undercover Lite PC | Button glyph packs (0.4)";
        Size = new Size(720, 530);
        MinimumSize = new Size(590, 460);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(15, 19, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        var guide = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
            BackColor = BackColor, ForeColor = ForeColor, DetectUrls = false,
            Text = "INPUT GLYPH REPLACEMENTS · EXPERIMENTAL\n\n" +
                   "1. Choose Configure PCSX2 in the launcher. Under Settings > Graphics > Texture Replacement, enable DUMP TEXTURES.\n\n" +
                   "2. Launch Undercover and visit its menus, pause screen, and tutorial prompts. Then close the game and switch DUMP TEXTURES back OFF.\n\n" +
                   "3. Inspect the captured PNG files under textures/<game serial>/dumps. Identify prompt sheets; create your own edited images preserving the exact PCSX2 hash filenames.\n\n" +
                   "4. Put edited PNG/DDS textures under your custom pack's replacements folder, alongside a glyph-pack.json manifest in its parent folder.\n\n" +
                   "5. Install the pack below, then enable LOAD TEXTURES in PCSX2's graphics settings and restart the emulation session.\n\n" +
                   "Safety: only verified uncompressed Undercover ISO images and isolated PCSX2 data are supported by the automatic installer. Compressed images can still be handled manually. Original images and BIOS stay on your own machine.\n\n" +
                   "PC key artwork must match your actual PCSX2 bindings. The prototype does not apply keyboard mappings or swap packs in real time."
        };
        layout.Controls.Add(guide, 0, 0);
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        for (int i = 0; i < 3; i++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        var dump = MakeButton("Open texture dumps");
        dump.Click += (_, _) => OpenGameTextureFolder("dumps");
        var replacement = MakeButton("Open replacements");
        replacement.Click += (_, _) => OpenGameTextureFolder("replacements");
        var install = MakeButton("Install a glyph pack...");
        install.Click += (_, _) => InstallPack();
        buttons.Controls.Add(dump, 0, 0);
        buttons.Controls.Add(replacement, 1, 0);
        buttons.Controls.Add(install, 2, 0);
        layout.Controls.Add(buttons, 0, 1);
        _feedback.Dock = DockStyle.Fill;
        _feedback.ForeColor = Color.FromArgb(255, 182, 64);
        _feedback.TextAlign = ContentAlignment.MiddleLeft;
        _feedback.Text = _gameRunning ? "Close your current PCSX2 game session before installing a pack." :
            "No original game artwork is bundled. See docs/GLYPH_PACKS.md for the pack format.";
        layout.Controls.Add(_feedback, 0, 2);
        Controls.Add(layout);
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text, Dock = DockStyle.Fill, Margin = new Padding(5, 10, 5, 9),
        BackColor = Color.FromArgb(39, 48, 61), ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat
    };

    private string? VerifiedSerial()
    {
        if (!_settings.IsolatePcsx2Data)
        {
            _feedback.Text = "Enable 'Separate PCSX2 settings and saves' first; external PCSX2 texture paths can differ.";
            return null;
        }
        var inspected = Ps2DiscIdentifier.Inspect(_settings.GameImage);
        if (!inspected.IsUndercover || inspected.Serial is null)
        {
            _feedback.Text = "Use a verified Undercover .iso for automatic paths; compressed image IDs cannot yet be read.";
            return null;
        }
        return inspected.Serial;
    }

    private void OpenGameTextureFolder(string child)
    {
        try
        {
            var serial = VerifiedSerial();
            if (serial is null) return;
            var directory = Path.Combine(GlyphPackManager.GetGameTextureDirectory(_store.Pcsx2DataDirectory, serial), child);
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
            _feedback.Text = $"Opened {serial}/{child}.";
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void InstallPack()
    {
        if (_gameRunning) { _feedback.Text = "Close the game first, then reopen this screen."; return; }
        var serial = VerifiedSerial();
        if (serial is null) return;
        using var dialog = new FolderBrowserDialog { Description = "Select a folder containing glyph-pack.json and replacements/" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var plan = GlyphPackManager.Inspect(dialog.SelectedPath, serial, _store.Pcsx2DataDirectory);
            if (plan.FileNames.Count == 0)
            {
                _feedback.Text = "The selected pack is an empty template. Add edited textures first.";
                return;
            }
            if (plan.ConflictingFiles != 0)
            {
                _feedback.Text = $"Blocked: {plan.ConflictingFiles} existing replacement files differ. No files were overwritten.";
                return;
            }
            var approval = MessageBox.Show(this,
                $"Pack: {plan.Name}\nRegion: {plan.Serial}\nNew textures: {plan.NewFiles}\nAlready installed: {plan.IdenticalFiles}\n\n" +
                $"Destination:\n{plan.DestinationDirectory}\n\n" +
                "Only user-supplied textures will be copied. PCSX2 Load Textures must be enabled manually. Proceed?",
                "Install glyph pack", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (approval != DialogResult.Yes) return;
            var result = GlyphPackManager.Install(dialog.SelectedPath, serial, _store.Pcsx2DataDirectory);
            _feedback.Text = $"Installed {result.NewFiles} file(s), {result.IdenticalFiles} already present. Restart PCSX2 with Load Textures enabled.";
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string message)
    {
        _feedback.Text = "Glyph pack operation failed. No existing replacements were intentionally overwritten.";
        MessageBox.Show(this, message, "Glyph pack manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}