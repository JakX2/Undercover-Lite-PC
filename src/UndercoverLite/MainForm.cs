using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using UndercoverLite.Core;

namespace UndercoverLite;

public sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(15, 19, 28);
    private static readonly Color CardColor = Color.FromArgb(25, 31, 42);
    private static readonly Color MutedColor = Color.FromArgb(161, 174, 192);
    private static readonly Color AccentColor = Color.FromArgb(255, 182, 64);

    private readonly LauncherSettingsStore _store = new();
    private readonly TextBox _pcsx2Path = new();
    private readonly TextBox _gamePath = new();
    private readonly CheckBox _fullscreen = new();
    private readonly CheckBox _fastBoot = new();
    private readonly CheckBox _hideEmulator = new();
    private readonly CheckBox _isolateData = new();
    private readonly Label _status = new();
    private readonly Button _launchButton = new();

    public MainForm()
    {
        Text = "Undercover Lite PC | Prototype 0.1";
        MinimumSize = new Size(780, 620);
        Size = new Size(920, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildUi();
        LoadSettings();
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(30, 22, 30, 18),
            BackColor = BackgroundColor,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(NewLabel("UNDERCOVER LITE PC", 22, AccentColor, bold: true), 0, 0);
        header.Controls.Add(NewLabel("An emulation-backed PC launcher for Need for Speed Undercover (PS2)", 10, Color.White), 0, 1);
        header.Controls.Add(NewLabel("Prototype 0.1  •  Bring your own PCSX2, game image and BIOS", 9, MutedColor), 0, 2);
        layout.Controls.Add(header, 0, 0);

        var paths = NewCard();
        var pathsTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14, 8, 14, 8) };
        pathsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        pathsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        pathsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        pathsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        pathsTable.Controls.Add(NewLabel("PCSX2 EXECUTABLE", 9, MutedColor, bold: true), 0, 0);
        pathsTable.Controls.Add(PathPicker(_pcsx2Path, "Browse…", () => BrowsePcsx2()), 0, 1);
        pathsTable.Controls.Add(NewLabel("PS2 GAME IMAGE", 9, MutedColor, bold: true), 0, 2);
        pathsTable.Controls.Add(PathPicker(_gamePath, "Browse…", () => BrowseGame()), 0, 3);
        paths.Controls.Add(pathsTable);
        layout.Controls.Add(paths, 0, 1);

        var options = NewCard();
        var optionTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(18, 12, 18, 8) };
        optionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        optionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        optionTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        optionTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        optionTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        optionTable.Controls.Add(NewLabel("LAUNCH PREFERENCES", 9, MutedColor, bold: true), 0, 0);
        ConfigureCheck(_fullscreen, "Launch fullscreen");
        ConfigureCheck(_fastBoot, "Fast boot (skip PS2 startup)");
        ConfigureCheck(_hideEmulator, "Hide PCSX2 main window");
        ConfigureCheck(_isolateData, "Separate PCSX2 settings and saves");
        optionTable.Controls.Add(_fullscreen, 0, 1);
        optionTable.Controls.Add(_fastBoot, 1, 1);
        optionTable.Controls.Add(_hideEmulator, 0, 2);
        optionTable.Controls.Add(_isolateData, 1, 2);
        options.Controls.Add(optionTable);
        layout.Controls.Add(options, 0, 2);

        var controls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 12, 0, 9) };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        var setupButton = NewButton("Configure PCSX2", secondary: true);
        setupButton.Click += (_, _) => OpenPcsx2Setup();
        var saveButton = NewButton("Save settings", secondary: true);
        saveButton.Click += (_, _) => SaveSettings(showMessage: true);
        _launchButton.Text = "▶  Launch Undercover";
        StyleButton(_launchButton, secondary: false);
        _launchButton.Click += (_, _) => LaunchGame();
        controls.Controls.Add(setupButton, 0, 0);
        controls.Controls.Add(saveButton, 1, 0);
        controls.Controls.Add(_launchButton, 2, 0);
        layout.Controls.Add(controls, 0, 3);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _status.AutoSize = false;
        _status.Dock = DockStyle.Fill;
        _status.ForeColor = MutedColor;
        footer.Controls.Add(_status, 0, 0);
        footer.Controls.Add(NewLabel("Keyboard/controller mapping and graphics settings are configured in PCSX2 for now.", 9, MutedColor), 0, 1);
        footer.Controls.Add(NewLabel("No PS2 game, BIOS or emulator files are included. This is not yet an in-game PC menu.", 9, MutedColor), 0, 2);
        layout.Controls.Add(footer, 0, 4);
    }

    private static Panel NewCard() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = CardColor,
        Padding = new Padding(6),
        Margin = new Padding(0, 0, 0, 9),
    };

    private static Label NewLabel(string text, float fontSize, Color color, bool bold = false) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoSize = false,
        ForeColor = color,
        Font = new Font("Segoe UI", fontSize, bold ? FontStyle.Bold : FontStyle.Regular),
    };

    private static void StyleButton(Button button, bool secondary)
    {
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(5, 0, 5, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = secondary ? 1 : 0;
        button.FlatAppearance.BorderColor = Color.FromArgb(88, 99, 113);
        button.BackColor = secondary ? CardColor : AccentColor;
        button.ForeColor = secondary ? Color.White : Color.FromArgb(20, 23, 28);
        button.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private static Button NewButton(string title, bool secondary)
    {
        var b = new Button { Text = title };
        StyleButton(b, secondary);
        return b;
    }

    private static void ConfigureCheck(CheckBox c, string title)
    {
        c.Text = title;
        c.Dock = DockStyle.Fill;
        c.AutoSize = false;
        c.ForeColor = Color.White;
        c.Padding = new Padding(2, 0, 0, 0);
    }

    private static TableLayoutPanel PathPicker(TextBox box, string buttonText, Action browse)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        box.Dock = DockStyle.Fill;
        box.BackColor = Color.FromArgb(14, 19, 27);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Margin = new Padding(2, 7, 6, 7);
        var button = NewButton(buttonText, secondary: true);
        button.Click += (_, _) => browse();
        row.Controls.Add(box, 0, 0);
        row.Controls.Add(button, 1, 0);
        return row;
    }

    private void BrowsePcsx2()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "PCSX2 executable (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select the PCSX2 Qt executable (pcsx2-qt.exe)"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _pcsx2Path.Text = dialog.FileName;
    }

    private void BrowseGame()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "PS2 disc images (*.iso;*.chd;*.cso;*.zso;*.gz)|*.iso;*.chd;*.cso;*.zso;*.gz|All files (*.*)|*.*",
            Title = "Select your own Need for Speed Undercover PS2 image"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _gamePath.Text = dialog.FileName;
    }

    private LauncherSettings ReadUi() => new()
    {
        Pcsx2Executable = _pcsx2Path.Text.Trim().Trim('"'),
        GameImage = _gamePath.Text.Trim().Trim('"'),
        Fullscreen = _fullscreen.Checked,
        FastBoot = _fastBoot.Checked,
        HideEmulatorWindow = _hideEmulator.Checked,
        IsolatePcsx2Data = _isolateData.Checked,
    };

    private void LoadSettings()
    {
        try
        {
            var s = _store.Load();
            // On first use, detect the official Qt executable if PCSX2 is
            // separately extracted next to Undercover Lite. User choice wins.
            var detected = string.IsNullOrWhiteSpace(s.Pcsx2Executable)
                ? Pcsx2InstallLocator.FindNextToLauncher(AppContext.BaseDirectory)
                : null;
            _pcsx2Path.Text = detected ?? s.Pcsx2Executable;
            _gamePath.Text = s.GameImage;
            _fullscreen.Checked = s.Fullscreen;
            _fastBoot.Checked = s.FastBoot;
            _hideEmulator.Checked = s.HideEmulatorWindow;
            _isolateData.Checked = s.IsolatePcsx2Data;
            _status.Text = detected is null
                ? "Ready. Select PCSX2 and your PS2 image, then configure your controller."
                : "Found PCSX2 beside the launcher. Configure BIOS and input before launching.";
        }
        catch (Exception ex)
        {
            _status.Text = "Saved settings could not be loaded; defaults are active.";
            ShowError("Settings could not be loaded. Your existing file was not overwritten.", ex);
            _fullscreen.Checked = true;
            _fastBoot.Checked = true;
            _hideEmulator.Checked = true;
            _isolateData.Checked = true;
        }
    }

    private bool SaveSettings(bool showMessage)
    {
        try
        {
            _store.Save(ReadUi());
            _status.Text = "Settings saved locally: " + _store.FilePath;
            if (showMessage) MessageBox.Show(this, "Launcher settings saved.", "Undercover Lite PC", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            ShowError("Could not save launcher settings.", ex);
            return false;
        }
    }

    private void OpenPcsx2Setup()
    {
        try
        {
            var config = ReadUi();
            Pcsx2Launcher.Validate(config, launchingGame: false);
            if (!SaveSettings(showMessage: false)) return;
            using var process = Pcsx2Launcher.Start(config, _store.Pcsx2DataDirectory, launchingGame: false);
            _status.Text = "PCSX2 opened. Configure BIOS, keyboard/gamepad and graphics in its own settings.";
        }
        catch (Exception ex)
        {
            ShowError("PCSX2 could not be opened.", ex);
        }
    }

    private void LaunchGame()
    {
        try
        {
            var config = ReadUi();
            Pcsx2Launcher.Validate(config, launchingGame: true);
            if (!SaveSettings(showMessage: false)) return;
            using var process = Pcsx2Launcher.Start(config, _store.Pcsx2DataDirectory, launchingGame: true);
            _status.Text = "Undercover launched through PCSX2. Close the game normally when finished.";
        }
        catch (Exception ex)
        {
            ShowError("Could not start Undercover. Check your PCSX2, BIOS and game image setup.", ex);
        }
    }

    private void ShowError(string message, Exception ex)
    {
        MessageBox.Show(this, message + Environment.NewLine + Environment.NewLine + ex.Message,
            "Undercover Lite PC", MessageBoxButtons.OK, MessageBoxIcon.Error);
        _status.Text = message;
    }
}