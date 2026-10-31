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
    private readonly TextBox _textureSearchPath = new() { ReadOnly = true };
    private readonly CheckBox _adoptMatching = new();

    public GlyphPackForm(LauncherSettingsStore store, LauncherSettings settings, bool gameRunning)
    {
        _store = store;
        _settings = settings;
        _gameRunning = gameRunning;
        Text = "Undercover Lite PC | Button glyph themes (0.6)";
        Size = new Size(850, 690);
        MinimumSize = new Size(720, 550);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(15, 19, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(18) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
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
                   "5. ACTIVATE / SWITCH tracks exactly what Undercover Lite installs. " +
                   "DISABLE removes only unchanged tracked icons. Restart PCSX2 with LOAD TEXTURES enabled.\\n\\n" +
                   "To adopt an old installation, first select the SAME theme and check the explicit " +
                   "adoption box for byte-identical existing textures. Unknown mods are protected. " +
                   "This does not change keyboard bindings."
        };
        layout.Controls.Add(guide, 0, 0);
        var search = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        search.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        search.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        search.Controls.Add(new Label
        {
            Text = "PCSX2 Search Directory (match the path shown in Graphics Settings):",
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(255, 182, 64),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _textureSearchPath.Dock = DockStyle.Fill;
        _textureSearchPath.BackColor = Color.FromArgb(25, 31, 42);
        _textureSearchPath.ForeColor = Color.White;
        _textureSearchPath.Margin = new Padding(0, 8, 6, 8);
        _textureSearchPath.Text = GlyphPackManager.GetTextureSearchDirectory(
            _store.Pcsx2DataDirectory, _settings.TextureSearchDirectory);
        search.Controls.Add(_textureSearchPath, 0, 1);
        var selectRoot = MakeButton("Choose folder...");
        selectRoot.Click += (_, _) => ChooseSearchDirectory();
        search.Controls.Add(selectRoot, 1, 1);
        layout.Controls.Add(search, 0, 1);
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        for (int i = 0; i < 4; i++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        var dump = MakeButton("Open texture dumps");
        dump.Click += (_, _) => OpenGameTextureFolder("dumps");
        var replacement = MakeButton("Open replacements");
        replacement.Click += (_, _) => OpenGameTextureFolder("replacements");
        var install = MakeButton("Activate / Switch...");
        install.Click += (_, _) => ActivateTheme();
        var disable = MakeButton("Disable managed glyphs");
        disable.Click += (_, _) => DisableTheme();
        buttons.Controls.Add(dump, 0, 0);
        buttons.Controls.Add(replacement, 1, 0);
        buttons.Controls.Add(install, 2, 0);
        buttons.Controls.Add(disable, 3, 0);
        layout.Controls.Add(buttons, 0, 2);
        _adoptMatching.Text = "I own matching glyphs from the old installer; allow adopting those exact files";
        _adoptMatching.Dock = DockStyle.Fill;
        _adoptMatching.ForeColor = Color.White;
        _adoptMatching.Checked = false;
        layout.Controls.Add(_adoptMatching, 0, 3);
        _feedback.Dock = DockStyle.Fill;
        _feedback.ForeColor = Color.FromArgb(255, 182, 64);
        _feedback.TextAlign = ContentAlignment.MiddleLeft;
        _feedback.Text = _gameRunning ? "Close your current PCSX2 game session before installing a pack." :
            "No original game artwork is bundled. See docs/GLYPH_PACKS.md for the pack format.";
        layout.Controls.Add(_feedback, 0, 4);
        Controls.Add(layout);
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text, Dock = DockStyle.Fill, Margin = new Padding(5, 10, 5, 9),
        BackColor = Color.FromArgb(39, 48, 61), ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat
    };

    private void ChooseSearchDirectory()
    {
        if (_gameRunning)
        {
            _feedback.Text = "Close the running PCSX2 session before changing the texture location.";
            return;
        }
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the exact Search Directory displayed in PCSX2 Graphics > Texture Replacement",
            SelectedPath = Directory.Exists(_textureSearchPath.Text)
                ? _textureSearchPath.Text : _store.Pcsx2DataDirectory
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var selected = Path.GetFullPath(dialog.SelectedPath);
            // Update only this setting; preserve changes from PC Input Lab and main launcher.
            var settings = _store.Load();
            settings.TextureSearchDirectory = selected;
            _store.Save(settings);
            _settings.TextureSearchDirectory = selected;
            _textureSearchPath.Text = selected;
            _feedback.Text = "Saved exact PCSX2 Search Directory. Reinstall your glyph pack here.";
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

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
            var directory = Path.Combine(GlyphPackManager.GetGameTextureDirectory(
                _store.Pcsx2DataDirectory, serial, _textureSearchPath.Text), child);
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
            var plan = GlyphPackManager.Inspect(dialog.SelectedPath, serial,
                _store.Pcsx2DataDirectory, _textureSearchPath.Text);
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
            var result = GlyphPackManager.Install(dialog.SelectedPath, serial,
                _store.Pcsx2DataDirectory, _textureSearchPath.Text);
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