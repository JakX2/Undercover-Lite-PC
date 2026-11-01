using System.Drawing;
using System.Windows.Forms;
using UndercoverLite.Core;

namespace UndercoverLite;

/// <summary>
/// Unified external PC settings hub (UI-1).
/// Only enabled options with a working launcher backend are editable.
/// PCSX2/game-engine settings lacking tested adapters are displayed read-only.
/// This form is NOT an injected PS2 game menu or a runtime graphics overlay.
/// </summary>
public sealed class PcSettingsForm : Form
{
    private static readonly Color Background = Color.FromArgb(11, 13, 17);
    private static readonly Color Surface = Color.FromArgb(24, 29, 39);
    private static readonly Color Field = Color.FromArgb(32, 39, 51);
    private static readonly Color Muted = Color.FromArgb(174, 184, 200);
    private static readonly Color Accent = Color.FromArgb(255, 182, 64);

    private readonly LauncherSettingsStore _store;
    private readonly bool _gameRunning;
    private LauncherSettings _baseline;
    private readonly LauncherSettings _initial;
    private readonly ComboBox _displayMode = new();
    private readonly ComboBox _internalScale = new();
    private readonly Label _gameInputState = new();
    private readonly Label _gameGraphicsStatus = new();
    private readonly Button _scaleApply = new();
    private readonly Label _scaleState = new();
    private Pcsx2GraphicsAdapter.Snapshot? _scaleSnapshot;
    private readonly CheckBox _fastBoot = new();
    private readonly CheckBox _hideEmulator = new();
    private readonly CheckBox _isolated = new();
    private readonly CheckBox _companionHotkey = new();
    private readonly TextBox _pcsx2Path = new();
    private readonly TextBox _gameImagePath = new();
    private readonly TextBox _texturePath = new();
    private readonly Button _apply = new();
    private readonly Label _status = new();
    private readonly TabControl _tabs = new();
    private bool _loading = true;

    public bool SavedChanges { get; private set; }

    public PcSettingsForm(LauncherSettingsStore store, LauncherSettings originalSaved,
        LauncherSettings displayedFromLauncher, bool gameRunning)
    {
        _store = store;
        _baseline = originalSaved;
        _initial = displayedFromLauncher;
        _gameRunning = gameRunning;
        Text = "Undercover Lite PC | PC Settings (UI-3 + graphics audit)";
        BackColor = Background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1050, 760);
        MinimumSize = new Size(830, 650);
        BuildUi();
        Populate(_initial);
        _loading = false;
        RefreshDirty();
        RefreshGraphicsScale();
        RefreshGameInputSelection();
        RefreshGameGraphicsStatus();
    }

    private void BuildUi()
    {
        var frame = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(24, 16, 24, 14)
        };
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        Controls.Add(frame);

        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        heading.Controls.Add(new Label
        {
            Text = "UNDERCOVER | PC SETTINGS",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Color.White, Dock = DockStyle.Fill
        }, 0, 0);
        heading.Controls.Add(new Label
        {
            Text = "EXIENT PS2 EDITION  •  PCSX2-POWERED  •  SETTINGS FOR NEXT LAUNCH",
            Font = new Font("Segoe UI", 9), ForeColor = Muted, Dock = DockStyle.Fill
        }, 0, 1);
        frame.Controls.Add(heading, 0, 0);

        _tabs.Dock = DockStyle.Fill;
        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.ItemSize = new Size(122, 38);
        _tabs.Appearance = TabAppearance.Normal;
        _tabs.DrawItem += DrawTab;
        _tabs.Padding = new Point(6, 6);
        foreach (var name in new[] { "Graphics", "Controls", "Audio", "Interface", "Advanced", "Troubleshooting" })
        {
            var tab = new TabPage(name) { BackColor = Background, ForeColor = Color.White, Padding = new Padding(8) };
            tab.AutoScroll = true;
            _tabs.TabPages.Add(tab);
        }
        BuildGraphics(_tabs.TabPages[0]);
        BuildControls(_tabs.TabPages[1]);
        BuildAudio(_tabs.TabPages[2]);
        BuildInterface(_tabs.TabPages[3]);
        BuildAdvanced(_tabs.TabPages[4]);
        BuildTroubleshooting(_tabs.TabPages[5]);
        frame.Controls.Add(_tabs, 0, 1);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Muted;
        _status.AutoEllipsis = true;
        footer.Controls.Add(_status, 0, 0);
        var close = Button("Back");
        close.Click += (_, _) => Close();
        footer.Controls.Add(close, 1, 0);
        _apply.Text = "Apply changes";
        StyleButton(_apply, primary: true);
        _apply.Click += (_, _) => Apply();
        footer.Controls.Add(_apply, 2, 0);
        frame.Controls.Add(footer, 0, 2);
    }

    private void DrawTab(object? sender, DrawItemEventArgs e)
    {
        var selected = e.Index == _tabs.SelectedIndex;
        var bounds = e.Bounds;
        using var bg = new SolidBrush(selected ? Surface : Background);
        using var text = new SolidBrush(selected ? Color.White : Muted);
        using var tabFont = new Font("Segoe UI", 10, selected ? FontStyle.Bold : FontStyle.Regular);
        e.Graphics.FillRectangle(bg, bounds);
        TextRenderer.DrawText(e.Graphics, _tabs.TabPages[e.Index].Text,
            tabFont, bounds, text.Color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if (selected)
        {
            using var line = new SolidBrush(Accent);
            e.Graphics.FillRectangle(line, bounds.Left + 8, bounds.Bottom - 3, bounds.Width - 16, 3);
        }
    }

    private static Button Button(string title) { var b = new Button { Text = title }; StyleButton(b); return b; }
    private static void StyleButton(Button button, bool primary = false)
    {
        button.Dock = DockStyle.Fill;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(79, 92, 105);
        button.BackColor = primary ? Accent : Field;
        button.ForeColor = primary ? Background : Color.White;
        button.Margin = new Padding(4, 8, 4, 8);
        button.Font = new Font("Segoe UI", 10, primary ? FontStyle.Bold : FontStyle.Regular);
        button.Cursor = Cursors.Hand;
    }

    private static TableLayoutPanel Page(TabPage tab)
    {
        var list = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1 };
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tab.Controls.Add(list);
        return list;
    }

    private static TableLayoutPanel Group(TableLayoutPanel page, string title)
    {
        var group = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1,
            BackColor = Surface, Margin = new Padding(1, 8, 6, 10),
            Padding = new Padding(16, 10, 16, 10)
        };
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        group.RowCount = 1;
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        group.Controls.Add(new Label
        {
            Text = title.ToUpperInvariant(), Dock = DockStyle.Fill,
            ForeColor = Accent, Font = new Font("Segoe UI", 11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        page.RowCount++;
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.Controls.Add(group, 0, page.RowCount - 1);
        return group;
    }

    private static void Row(TableLayoutPanel group, string title, string help, Control right, string state)
    {
        var layout = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Height = 81 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(new Label
        {
            Text = title, Dock = DockStyle.Fill, ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        left.Controls.Add(new Label
        {
            Text = help, Dock = DockStyle.Fill, ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.TopLeft
        }, 0, 1);
        layout.Controls.Add(left, 0, 0);
        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Dock = DockStyle.Fill;
        right.Margin = new Padding(8, 5, 5, 3);
        stack.Controls.Add(right, 0, 0);
        stack.Controls.Add(new Label
        {
            Text = state, Dock = DockStyle.Fill, ForeColor = Muted,
            Font = new Font("Segoe UI", 8),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(9, 0, 0, 0)
        }, 0, 1);
        layout.Controls.Add(stack, 1, 0);
        group.RowCount++;
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 83));
        group.Controls.Add(layout, 0, group.RowCount - 1);
    }

    private static Control Unavailable(string label = "Managed by PCSX2") => new TextBox
    {
        Text = label, ReadOnly = true, Enabled = false, BackColor = Field,
        ForeColor = Muted, BorderStyle = BorderStyle.FixedSingle
    };

    private static Control Pending(string label = "Coming later") => Unavailable(label);

    private static Control Action(string title, Action action)
    {
        var btn = Button(title);
        btn.Click += (_, _) => action();
        return btn;
    }

    private void Connect(CheckBox check)
    {
        check.CheckedChanged += (_, _) => RefreshDirty();
        check.ForeColor = Color.White;
        check.Padding = new Padding(6, 0, 0, 0);
    }

    private static void PrepareText(TextBox box)
    {
        box.BackColor = Field;
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
    }

    private void BuildGraphics(TabPage tab)
    {
        var list = Page(tab);
        var display = Group(list, "Display");
        _displayMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _displayMode.BackColor = Field;
        _displayMode.ForeColor = Color.White;
        _displayMode.Items.AddRange(new object[] { "Fullscreen", "Windowed" });
        _displayMode.SelectedIndexChanged += (_, _) => RefreshDirty();
        Row(display, "Display mode", "Choose how Undercover opens.", _displayMode, "Available now · Next launch");
        Row(display, "Resolution", "Desktop resolution and internal PS2 rendering are separate.",
            Unavailable(), "Requires PCSX2 integration");
        Row(display, "V-Sync", "May reduce tearing and add input latency.",
            Unavailable("PCSX2 managed"), "Requires PCSX2 integration");
        Row(display, "Aspect ratio", "A widescreen output mode does not guarantee correctly scaled HUD.",
            Unavailable(), "Requires verified game/PCSX2 setup");
        var render = Group(list, "Rendering");
        Row(render, "Internal resolution", "Adjusts the isolated PCSX2 global renderer scale. A per-game override may take priority.",
            ScaleSelector(), "Real PCSX2 setting · Separate Apply + backup · Next boot");
        _scaleState.ForeColor = Muted;
        _scaleState.TextAlign = ContentAlignment.MiddleLeft;
        _scaleState.AutoEllipsis = true;
        Row(render, "Graphics configuration", "The game must be stopped. Backups are kept alongside PCSX2.ini.",
            _scaleState, "Read-only state · Never writes game-specific overrides");
        Row(render, "Undercover graphics override",
            "Read the game's saved serial+CRC graphics override, without modifying any file.",
            Action("Inspect game graphics...", ShowGameGraphicsAudit),
            "Available now · Read-only config inspection");
        _gameGraphicsStatus.ForeColor = Muted;
        _gameGraphicsStatus.TextAlign = ContentAlignment.MiddleLeft;
        _gameGraphicsStatus.AutoEllipsis = true;
        Row(render, "Saved graphics status",
            "Compares the global scale to a unique saved game config; active CRC is not verified.",
            _gameGraphicsStatus, "Read-only · Game Properties can override global rendering");
        Row(render, "Anisotropic filtering", "Sharper textures on oblique surfaces.",
            Unavailable(), "Requires PCSX2 integration");
        Row(render, "Texture replacements", "Manage installed UI button glyphs and controller artwork.",
            Action("Manage glyph themes...", OpenGlyphs), "Available now · Relaunch game to take effect");
        Row(render, "Graphics renderer", "Vulkan, Direct3D and OpenGL support varies.",
            Unavailable(), "Requires PCSX2 integration");
        Row(render, "Advanced image effects", "Motion blur, bloom and FPS changes need game-specific work.",
            Pending(), "Not implemented");
    }

    private Control ScaleSelector()
    {
        var panel = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 59));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 41));
        _internalScale.DropDownStyle = ComboBoxStyle.DropDownList;
        _internalScale.BackColor = Field;
        _internalScale.ForeColor = Color.White;
        _internalScale.Dock = DockStyle.Fill;
        _internalScale.Margin = new Padding(1, 8, 3, 8);
        _internalScale.Items.AddRange(new object[]
        {
            "Native (1×)", "2× (~720p)", "3× (~1080p)", "4× (~1440p)", "6× (~2160p)"
        });
        _internalScale.SelectedIndexChanged += (_, _) => UpdateScaleButton();
        panel.Controls.Add(_internalScale, 0, 0);
        _scaleApply.Text = "Apply scale...";
        StyleButton(_scaleApply);
        _scaleApply.Margin = new Padding(2, 4, 1, 4);
        _scaleApply.Click += (_, _) => SaveGraphicsScale();
        panel.Controls.Add(_scaleApply, 1, 0);
        _scaleApply.Enabled = false;
        return panel;
    }

    private int SelectedScale() => _internalScale.SelectedIndex switch
    {
        0 => 1, 1 => 2, 2 => 3, 3 => 4, 4 => 6, _ => 0
    };

    private void RefreshGraphicsScale()
    {
        try
        {
            var persisted = _store.Load();
            if (!persisted.IsolatePcsx2Data)
                throw new InvalidOperationException("Isolated PCSX2 data is disabled.");
            _scaleSnapshot = Pcsx2GraphicsAdapter.Inspect(persisted, _store.Pcsx2DataDirectory);
            int index = Array.IndexOf(Pcsx2GraphicsAdapter.AllowedMultipliers,
                (int)_scaleSnapshot.CurrentScale);
            _internalScale.Enabled = !_gameRunning;
            _internalScale.SelectedIndex =
                index >= 0 && _scaleSnapshot.CurrentScale == Pcsx2GraphicsAdapter.AllowedMultipliers[index]
                    ? index : -1;
            _scaleState.Text = $"{_scaleSnapshot.CurrentScale:0.##}× global scale" +
                (_scaleSnapshot.HasExplicitSetting ? "" : " (PCSX2 default)");
            if (_gameRunning) _scaleState.Text += " · Exit game before editing";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   InvalidDataException or InvalidOperationException or ArgumentException)
        {
            _scaleSnapshot = null;
            _internalScale.SelectedIndex = -1;
            _internalScale.Enabled = false;
            _scaleState.Text = ex is FileNotFoundException
                ? "Run PCSX2 setup first to create its configuration."
                : "Graphics config unavailable: " + ex.Message;
        }
        UpdateScaleButton();
    }

    private void UpdateScaleButton()
    {
        _scaleApply.Enabled = !_gameRunning && _scaleSnapshot is not null &&
            SelectedScale() != 0 && SelectedScale() != _scaleSnapshot.CurrentScale &&
            _isolated.Checked;
    }

    private void SaveGraphicsScale()
    {
        if (_scaleSnapshot is null || _gameRunning) return;
        try
        {
            var persisted = _store.Load();
            if (!persisted.IsolatePcsx2Data || !_isolated.Checked)
                throw new InvalidOperationException(
                    "Save/enable isolated PCSX2 data first; global/shared PCSX2 settings will never be edited.");
            int multiplier = SelectedScale();
            if (multiplier == 0) return;
            var confirmed = MessageBox.Show(this,
                $"Set the isolated PCSX2 internal rendering resolution to {multiplier}×?\n\n" +
                "Please CLOSE every PCSX2 window first. This modifies the existing " +
                "isolated PCSX2.ini [EmuCore/GS] upscale_multiplier setting only.\n" +
                "A timestamped backup of the original file will be kept next to it.\n\n" +
                "This is the GLOBAL setting for this isolated emulator profile. " +
                "Undercover-specific graphics overrides may still take precedence.\n\n" +
                "Proceed?",
                "Apply PCSX2 graphics scale",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmed != DialogResult.Yes) return;
            var backup = Pcsx2GraphicsAdapter.Apply(
                persisted, _store.Pcsx2DataDirectory, _scaleSnapshot, multiplier);
            RefreshGraphicsScale();
            RefreshGameGraphicsStatus();
            _status.Text = string.IsNullOrEmpty(backup)
                ? "PCSX2 graphics scale was already set."
                : $"{multiplier}× saved in isolated PCSX2.ini with an original-file backup. Restart PCSX2.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message + "\n\nNo automatic retry was attempted.",
                "Could not apply graphics change", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshGraphicsScale();
        }
    }

    private void BuildControls(TabPage tab)
    {
        var list = Page(tab);
        var input = Group(list, "Input profiles");
        Row(input, "Controller and keyboard configuration",
            "Open the existing PC Input Lab to install real PCSX2 keyboard profiles.",
            Action("PC Input Lab...", OpenInputLab), "Available now · PCSX2 activation is manual");
        Row(input, "Steering strength", "Gentle 65%, Balanced 85%, Full 100%. No gradual smoothing.",
            Action("Steering presets...", OpenSteeringLab), "Available now · Select profile in PCSX2");
        Row(input, "Saved game input selection",
            "Reads the matching serial+CRC Undercover Game Properties. Not live controller state.",
            Action("Inspect game profile...", ShowSavedGameInput),
            "Read-only · Detects saved per-game choice, not runtime input");
        _gameInputState.ForeColor = Muted;
        _gameInputState.TextAlign = ContentAlignment.MiddleLeft;
        _gameInputState.AutoEllipsis = true;
        Row(input, "Saved selection status",
            "Warns about missing CRC matches, Shared profiles and missing profile files.",
            _gameInputState, "Auto-read · Close PCSX2 before making changes");
        Row(input, "Hotkey conflict check", "Space may pause PCSX2 instead of acting solely as handbrake.",
            Action("Inspect hotkeys...", InspectHotkeys), "Available now · Read-only");
        Row(input, "Individual key rebinding", "A native PC binding editor requires further input integration.",
            Pending(), "Not implemented");
        var reference = Group(list, "Driving-first reference");
        Row(reference, "Keyboard bindings", "Only valid after assigning UndercoverLite-DrivingFirst in PCSX2.",
            Action("Show keys...", ShowBindings), "Reference · Does not inspect live controls");
        Row(reference, "Menu keys", "Cross = W/Accept, Triangle = Space/Back across all contexts.",
            Unavailable("PS2 buttons are reused"), "Not context-sensitive");
    }

    private void BuildAudio(TabPage tab)
    {
        var list = Page(tab);
        var audio = Group(list, "Audio");
        Row(audio, "Master volume", "Currently controlled through PCSX2, not this launcher.",
            Pending("PCSX2 managed"), "Not wired yet");
        Row(audio, "Music volume", "Separate music mixing needs a verified game setting or patch.",
            Pending(), "Not implemented");
        Row(audio, "Sound effects", "Independent SFX mixing requires game support.",
            Pending(), "Not implemented");
        Row(audio, "Speech volume", "Requires Exient dialogue volume support.",
            Pending(), "Not implemented");
        Row(audio, "Output device", "Choose headphones or speakers in the emulator or Windows.",
            Pending("System default"), "Not wired yet");
        Row(audio, "Audio settings", "Use the working PCSX2 configuration UI for now.",
            Action("Configure PCSX2...", ConfigurePcsx2), "Opens emulator settings, not a direct audio page");
    }

    private void BuildInterface(TabPage tab)
    {
        var list = Page(tab);
        var visuals = Group(list, "In-game UI");
        Row(visuals, "Keyboard button glyphs", "Manage tracked replacements, including the matched 0.11 theme.",
            Action("Manage glyph themes...", OpenGlyphs), "Available now · Static textures");
        Row(visuals, "Input/icon alignment", "Check the declared input INI against registered glyph artwork.",
            Action("Run Setup Doctor...", OpenDoctor), "Read-only · Active PCSX2 profile unverified");
        Row(visuals, "Controller diagram", "The 0.11 illustration is experimental until tested in game.",
            Unavailable("Preview not built yet"), "Coming later");
        Row(visuals, "HUD size and safe area", "Requires game-specific screen-layout changes.",
            Pending(), "Not implemented");
        Row(visuals, "Subtitles and language", "Depend on which features the Exient game actually supports.",
            Pending(), "Not implemented");
    }

    private void BuildAdvanced(TabPage tab)
    {
        var list = Page(tab);
        var startup = Group(list, "Launcher behavior");
        Connect(_fastBoot);
        Connect(_hideEmulator);
        Connect(_isolated);
        _isolated.CheckedChanged += (_, _) => UpdateScaleButton();
        Connect(_companionHotkey);
        Row(startup, "Fast boot", "Skip the PS2 startup animation.", _fastBoot,
            "Available now · Next launch");
        Row(startup, "Hide PCSX2 main window", "Open directly into the game view where supported.",
            _hideEmulator, "Available now · Next launch");
        Row(startup, "Isolated PCSX2 data", "Separate Undercover saves and settings. No files are migrated.",
            _isolated, "Available now · Next launch");
        Row(startup, "Session companion hotkey", "Ctrl+Shift+F10 opens a Windows companion window.",
            _companionHotkey, "Available now · Next session");
        var files = Group(list, "Game and emulator paths");
        PrepareText(_pcsx2Path);
        PrepareText(_gameImagePath);
        PrepareText(_texturePath);
        _texturePath.ReadOnly = true;
        _pcsx2Path.TextChanged += (_, _) => RefreshDirty();
        _gameImagePath.TextChanged += (_, _) =>
        {
            RefreshDirty();
            if (!_loading)
            {
                RefreshGameInputSelection();
                RefreshGameGraphicsStatus();
            }
        };
        _texturePath.TextChanged += (_, _) => RefreshDirty();
        Row(files, "PCSX2 Qt executable", "Local emulator program path. Nothing is bundled.",
            Picker(_pcsx2Path, SelectPcsx2), "Saved only to launcher preferences");
        Row(files, "Undercover PS2 game image", "Your own ISO or compatible compressed game image.",
            Picker(_gameImagePath, SelectGameImage), "Saved only to launcher preferences");
        Row(files, "Texture Search Directory", "Must match the path in PCSX2 Graphics > Texture Replacement.",
            Picker(_texturePath, SelectTextureDirectory), "Saved to launcher; does not move texture files");
        Row(files, "Emulator settings", "Open PCSX2 configuration without writing global settings.",
            Action("Configure PCSX2...", ConfigurePcsx2), "Existing launcher action");
    }

    private static Control Picker(TextBox box, Action select)
    {
        var pair = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill };
        pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(0, 5, 4, 6);
        pair.Controls.Add(box, 0, 0);
        var choose = Button("Browse...");
        choose.Margin = new Padding(2, 1, 2, 3);
        choose.Click += (_, _) => select();
        pair.Controls.Add(choose, 1, 0);
        return pair;
    }

    private void BuildTroubleshooting(TabPage tab)
    {
        var list = Page(tab);
        var diagnosis = Group(list, "Setup Doctor");
        Row(diagnosis, "Run Setup Doctor", "Read-only checks of paths, PS2 serial and registered UI input theme.",
            Action("Run diagnostics...", OpenDoctor), "Available now · Does not modify files");
        Row(diagnosis, "Copy safe report", "Copy findings without private file paths or game/BIOS contents.",
            Action("Copy report...", CopyReport), "Available now · Read-only");
        Row(diagnosis, "Verify game image", "Inspect bounded ISO metadata for Undercover PS2 boot serial.",
            Action("Check image...", CheckGameImage), "Available now · Read-only");
        Row(diagnosis, "Inspect input/hotkey conflicts", "Look for Space-to-pause and other potential key collisions.",
            Action("Check hotkeys...", InspectHotkeys), "Available now · Read-only");
        var tools = Group(list, "Repair tools");
        Row(tools, "Input profiles", "Install the DrivingFirst or steering profiles; do not auto-activate.",
            Action("PC Input Lab...", OpenInputLab), "Available now · PCSX2 selection remains manual");
        Row(tools, "Glyph themes", "Switch or disable unchanged, tracked texture files safely.",
            Action("Manage glyphs...", OpenGlyphs), "Available now · Close game before switching");
        Row(tools, "Reset launcher and emulator", "Bulk reset would risk settings or saves; no automatic reset exists.",
            Pending("Not available"), "Research · No files modified");
    }

    private void Populate(LauncherSettings settings)
    {
        _displayMode.SelectedIndex = settings.Fullscreen ? 0 : 1;
        _fastBoot.Checked = settings.FastBoot;
        _hideEmulator.Checked = settings.HideEmulatorWindow;
        _isolated.Checked = settings.IsolatePcsx2Data;
        _companionHotkey.Checked = settings.EnableCompanionHotkey;
        _pcsx2Path.Text = settings.Pcsx2Executable;
        _gameImagePath.Text = settings.GameImage;
        _texturePath.Text = settings.TextureSearchDirectory;
    }

    private LauncherSettings Edited()
    {
        // Read the latest copy to preserve preferences and fields owned by other forms.
        var s = _store.Load();
        s.Fullscreen = _displayMode.SelectedIndex != 1;
        s.FastBoot = _fastBoot.Checked;
        s.HideEmulatorWindow = _hideEmulator.Checked;
        s.IsolatePcsx2Data = _isolated.Checked;
        s.EnableCompanionHotkey = _companionHotkey.Checked;
        s.Pcsx2Executable = _pcsx2Path.Text.Trim().Trim('"');
        s.GameImage = _gameImagePath.Text.Trim().Trim('"');
        s.TextureSearchDirectory = _texturePath.Text.Trim().Trim('"');
        return s;
    }

    private void RefreshDirty()
    {
        if (_loading) return;
        try
        {
            bool dirty = SettingsHubPatch.HasChanges(_baseline, Edited());
            _apply.Enabled = dirty;
            _status.Text = dirty
                ? "Unsaved launcher preferences. Apply to use them on the next launch."
                : "All displayed launcher preferences saved. PCSX2-managed options are read-only.";
        }
        catch (Exception)
        {
            _apply.Enabled = false;
            _status.Text = "Cannot inspect saved settings; correct the launcher configuration first.";
        }
    }

    private bool Apply()
    {
        try
        {
            var edited = Edited();
            if (!SettingsHubPatch.HasChanges(_baseline, edited)) return true;
            if (_baseline.IsolatePcsx2Data && !edited.IsolatePcsx2Data &&
                MessageBox.Show(this,
                    "Disabling isolated PCSX2 data will change which emulator settings and saves are used. " +
                    "No existing data will be moved. Continue?",
                    "Change PCSX2 profile location", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return false;
            // Important: reload again immediately before saving, then apply only the
            // fields the user actually changed. Other forms may have saved meanwhile.
            var latest = _store.Load();
            _store.Save(SettingsHubPatch.Merge(latest, _baseline, edited));
            _baseline = edited;
            SavedChanges = true;
            RefreshDirty();
            RefreshGraphicsScale();
            RefreshGameInputSelection();
            RefreshGameGraphicsStatus();
            _status.Text = "Launcher settings saved. Changes take effect on the next launch.";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save settings",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_loading)
        {
            try
            {
                if (SettingsHubPatch.HasChanges(_baseline, Edited()))
                {
                    var choice = MessageBox.Show(this,
                        "Save your pending launcher changes before closing?",
                        "Unsaved PC settings", MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);
                    if (choice == DialogResult.Cancel ||
                        (choice == DialogResult.Yes && !Apply()))
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // Avoid losing edits on unexpected read errors.
                var discard = MessageBox.Show(this,
                    "Saved preferences cannot be read. Leave the settings window without saving pending edits?",
                    "Cannot inspect settings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (discard != DialogResult.Yes) { e.Cancel = true; return; }
            }
        }
        base.OnFormClosing(e);
    }

    private void SelectPcsx2()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Select PCSX2 Qt executable", Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*"
        };
        if (picker.ShowDialog(this) == DialogResult.OK) _pcsx2Path.Text = picker.FileName;
    }

    private void SelectGameImage()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Choose your own Undercover PS2 game image",
            Filter = "PS2 images (*.iso;*.chd;*.cso;*.zso;*.gz)|*.iso;*.chd;*.cso;*.zso;*.gz|All files (*.*)|*.*"
        };
        if (picker.ShowDialog(this) == DialogResult.OK) _gameImagePath.Text = picker.FileName;
    }

    private void SelectTextureDirectory()
    {
        using var picker = new FolderBrowserDialog { Description = "Select PCSX2's exact Texture Search Directory" };
        if (Directory.Exists(_texturePath.Text)) picker.SelectedPath = _texturePath.Text;
        if (picker.ShowDialog(this) == DialogResult.OK) _texturePath.Text = picker.SelectedPath;
    }

    private void OpenDoctor()
    {
        try
        {
            using var doctor = new SetupDoctorForm(Edited(), _store.Pcsx2DataDirectory);
            doctor.ShowDialog(this);
        }
        catch (Exception ex) { Error("Setup Doctor", ex); }
    }

    private void OpenGlyphs()
    {
        try
        {
            var before = _texturePath.Text;
            using var glyphs = new GlyphPackForm(_store, Edited(), _gameRunning);
            glyphs.ShowDialog(this);
            // Glyphs can save the texture Search Directory independently.
            // Refresh it only if this form did not have an unsaved edit of that field.
            if (string.Equals(before, _baseline.TextureSearchDirectory, StringComparison.Ordinal))
            {
                var latest = _store.Load().TextureSearchDirectory;
                _texturePath.Text = latest;
                _baseline.TextureSearchDirectory = latest;
            }
            RefreshDirty();
        }
        catch (Exception ex) { Error("Glyph manager", ex); }
    }

    private void OpenInputLab()
    {
        try
        {
            using var lab = new PcInputLabForm(_store, _gameRunning);
            lab.ShowDialog(this);
        }
        catch (Exception ex) { Error("PC Input Lab", ex); }
    }

    private void OpenSteeringLab()
    {
        try
        {
            using var steering = new SteeringLabForm(_store, _gameRunning);
            steering.ShowDialog(this);
        }
        catch (Exception ex) { Error("Steering presets", ex); }
    }

    private Pcsx2GameInputInspector.Result SavedSelection() =>
        Pcsx2GameInputInspector.Inspect(Edited(), _store.Pcsx2DataDirectory);

    private void RefreshGameInputSelection()
    {
        try
        {
            var result = SavedSelection();
            _gameInputState.Text = result.Status switch
            {
                Pcsx2GameInputInspector.Kind.SelectedProfile =>
                    result.SelectedProfile + (result.RecognizedProfileMatchesSource == true ? " (verified file)" :
                    result.SelectedProfileFileExists == false ? " (missing file)" : " (custom/modified)"),
                Pcsx2GameInputInspector.Kind.SharedBindings => "Shared controller bindings",
                Pcsx2GameInputInspector.Kind.AmbiguousCrc => "Multiple game CRC profiles",
                Pcsx2GameInputInspector.Kind.UnverifiedDisc => "Disc serial unverified",
                _ => "Not verified: " + result.Status
            };
        }
        catch (Exception)
        {
            _gameInputState.Text = "Could not read saved PCSX2 input selection.";
        }
    }

    private void ShowSavedGameInput()
    {
        try
        {
            var info = SavedSelection();
            RefreshGameInputSelection();
            MessageBox.Show(this,
                "READ-ONLY PCSX2 SAVED GAME INPUT INSPECTION\n\n" +
                info.Summary + "\n\n" + info.NextStep + "\n\n" +
                "This reads a unique per-game INI only if its serial matches your verified ISO. " +
                "It does NOT inspect which keys are active inside the currently running emulator.",
                "Undercover saved input profile", MessageBoxButtons.OK,
                info.Status is Pcsx2GameInputInspector.Kind.SharedBindings or
                               Pcsx2GameInputInspector.Kind.AmbiguousCrc or
                               Pcsx2GameInputInspector.Kind.InvalidFile
                    ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (Exception ex) { Error("Saved input profile inspection", ex); }
    }

    private void RefreshGameGraphicsStatus()
    {
        try
        {
            var report = Pcsx2GameGraphicsAudit.Inspect(Edited(), _store.Pcsx2DataDirectory);
            _gameGraphicsStatus.Text = report.Status switch
            {
                Pcsx2GameGraphicsAudit.State.OverridePresent =>
                    $"Global {report.GlobalScale:0.##}×; saved game {report.GameScale:0.##}×" +
                    (report.HasPossibleDifferentOverride ? " (different)" : " (same)"),
                Pcsx2GameGraphicsAudit.State.UniqueGameIni =>
                    $"{report.GlobalScale:0.##}× global; no game scale override",
                Pcsx2GameGraphicsAudit.State.NoMatchingGameIni =>
                    $"{report.GlobalScale:0.##}× global; game config unknown",
                Pcsx2GameGraphicsAudit.State.MultipleCrcs =>
                    "Multiple game CRC configs; cannot choose",
                _ => "Not verified: " + report.Status
            };
        }
        catch (Exception)
        {
            _gameGraphicsStatus.Text = "Could not inspect saved game graphics settings.";
        }
    }

    private void ShowGameGraphicsAudit()
    {
        try
        {
            var report = Pcsx2GameGraphicsAudit.Inspect(Edited(), _store.Pcsx2DataDirectory);
            RefreshGameGraphicsStatus();
            MessageBox.Show(this,
                "SAVED UNDERCOVER GRAPHICS CONFIGURATION\n\n" +
                report.Summary + "\n\n" + report.NextStep + "\n\n" +
                "This check cannot establish the current running game's executable CRC " +
                "or prove that the saved configuration is active. It never changes game overrides.",
                "Undercover graphics overrides (read-only)", MessageBoxButtons.OK,
                report.HasPossibleDifferentOverride ||
                report.Status == Pcsx2GameGraphicsAudit.State.MultipleCrcs ||
                report.Status == Pcsx2GameGraphicsAudit.State.InvalidConfiguration
                    ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (Exception ex) { Error("Undercover graphics override check", ex); }
    }

    private void ShowBindings()
    {
        MessageBox.Show(this,
            "Reference for UndercoverLite-DrivingFirst (not automatically verified):\n\n" +
            "W / S: Accelerate / Brake\nA / D: Steer\nSpace: Handbrake / Back\n" +
            "Shift: Nitrous\nQ / E: Shift down / up\nX: Speedbreaker / Delete\n" +
            "F: Pause / Done\nR: Reset car\nV: Change view\n" +
            "Arrow keys: D-pad menus\nIJKL: Right stick camera\n\n" +
            "IMPORTANT: Select the matching Input Profile in PCSX2 Game Properties. " +
            "W also accepts menus; Space also goes back.",
            "Driving-first keyboard reference", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void InspectHotkeys()
    {
        try
        {
            var audit = Pcsx2HotkeyAudit.Scan(_store.Pcsx2DataDirectory);
            MessageBox.Show(this, audit.Source + "\n\n" +
                string.Join("\n\n", audit.Findings.Select(f => f.Action + ": " + f.Description)),
                "PCSX2 hotkeys (read-only)", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { Error("Hotkey audit", ex); }
    }

    private void CheckGameImage()
    {
        try
        {
            var disc = Ps2DiscIdentifier.Inspect(Edited().GameImage);
            MessageBox.Show(this, disc.Message, "Game image inspection",
                MessageBoxButtons.OK, disc.Recognition == DiscRecognition.Undercover
                    ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex) { Error("Game image inspection", ex); }
    }

    private void CopyReport()
    {
        try
        {
            var report = SetupDoctor.Check(Edited(), _store.Pcsx2DataDirectory).CopyableReport();
            Clipboard.SetText(report);
            _status.Text = "Safe diagnostic report copied. Local file paths were omitted.";
        }
        catch (Exception ex) { Error("Copy safe report", ex); }
    }

    private void ConfigurePcsx2()
    {
        if (_gameRunning)
        {
            MessageBox.Show(this, "Exit the running game before opening a separate PCSX2 setup process.",
                "PCSX2 is running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var config = Edited();
            Pcsx2Launcher.Validate(config, launchingGame: false);
            using var emulator = Pcsx2Launcher.Start(config, _store.Pcsx2DataDirectory, launchingGame: false);
            _status.Text = "PCSX2 setup opened. Its graphics and audio options remain managed by PCSX2.";
        }
        catch (Exception ex) { Error("PCSX2 setup", ex); }
    }

    private void Error(string title, Exception ex) =>
        MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
