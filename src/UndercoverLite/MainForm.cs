using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
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
    private readonly CheckBox _hotkeyEnabled = new();
    private readonly Button _sessionButton = new();
    private readonly GameSessionGate _sessionGate = new();
    private readonly System.Windows.Forms.Timer _sessionTimer = new() { Interval = 850 };
    private Process? _gameProcess;
    private CompanionMenuForm? _companionMenu;
    private bool _hotkeyRegistered;
    private const int HotkeyId = 0x4C50;
    private const int WmHotkey = 0x0312;
    private const uint CtrlShiftNoRepeat = 0x0002 | 0x0004 | 0x4000;
    private const uint VkF10 = 0x79;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    private readonly Label _status = new();
    private readonly Button _launchButton = new();

    public MainForm()
    {
        Text = "Undercover Lite PC | Prototype 0.5";
        MinimumSize = new Size(800, 730);
        Size = new Size(980, 765);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildUi();
        _sessionTimer.Tick += (_, _) => PollSession();
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 206));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(NewLabel("UNDERCOVER LITE PC", 22, AccentColor, bold: true), 0, 0);
        header.Controls.Add(NewLabel("An emulation-backed PC launcher for Need for Speed Undercover (PS2)", 10, Color.White), 0, 1);
        header.Controls.Add(NewLabel("Prototype 0.5  •  Bring your own PCSX2, game image and BIOS", 9, MutedColor), 0, 2);
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
        var optionTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(18, 12, 18, 8) };
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
        optionTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        ConfigureCheck(_hotkeyEnabled, "Session companion hotkey (Ctrl+Shift+F10)");
        optionTable.Controls.Add(_hotkeyEnabled, 0, 3);
        optionTable.SetColumnSpan(_hotkeyEnabled, 2);
        options.Controls.Add(optionTable);
        layout.Controls.Add(options, 0, 2);

        var controls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Padding = new Padding(0, 12, 0, 9) };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        var setupButton = NewButton("Configure PCSX2", secondary: true);
        setupButton.Click += (_, _) => OpenPcsx2Setup();
        var verifyButton = NewButton("Check image", secondary: true);
        verifyButton.Click += (_, _) => CheckImage();
        var saveButton = NewButton("Save", secondary: true);
        saveButton.Click += (_, _) => SaveSettings(showMessage: true);
        _launchButton.Text = "▶  Launch Undercover";
        StyleButton(_launchButton, secondary: false);
        _launchButton.Click += (_, _) => LaunchGame();
        controls.Controls.Add(setupButton, 0, 0);
        controls.Controls.Add(verifyButton, 1, 0);
        controls.Controls.Add(saveButton, 2, 0);
        _sessionButton.Text = "Session menu";
        StyleButton(_sessionButton, secondary: true);
        _sessionButton.Enabled = false;
        _sessionButton.Click += (_, _) => ToggleCompanionMenu();
        controls.Controls.Add(_sessionButton, 3, 0);
        controls.Controls.Add(_launchButton, 4, 0);
        layout.Controls.Add(controls, 0, 3);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _status.AutoSize = false;
        _status.Dock = DockStyle.Fill;
        _status.ForeColor = MutedColor;
        footer.Controls.Add(_status, 0, 0);
        var inputHelp = new LinkLabel
        {
            Text = "First run? Click here for PCSX2 keyboard and controller setup instructions.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            LinkColor = AccentColor,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = AccentColor,
            Font = new Font("Segoe UI", 9F),
        };
        inputHelp.Click += (_, _) => { using var guide = new InputGuideForm(); guide.ShowDialog(this); };
        var glyphHelp = new LinkLabel
        {
            Text = "Button glyphs / texture packs...",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            LinkColor = AccentColor,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = AccentColor,
            Font = new Font("Segoe UI", 9F),
        };
        glyphHelp.Click += (_, _) =>
        {
            using var glyphs = new GlyphPackForm(_store, ReadUi(), _sessionGate.IsRunning);
            glyphs.ShowDialog(this);
        };
        var pcInput = new LinkLabel
        {
            Text = "PC input / name-entry lab...",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            LinkColor = AccentColor,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = AccentColor,
            Font = new Font("Segoe UI", 9F),
        };
        pcInput.Click += (_, _) => { using var lab = new PcInputLabForm(_store); lab.ShowDialog(this); };
        var helpLinks = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        helpLinks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        helpLinks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        helpLinks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        helpLinks.Controls.Add(inputHelp, 0, 0);
        helpLinks.Controls.Add(glyphHelp, 1, 0);
        helpLinks.Controls.Add(pcInput, 2, 0);
        footer.Controls.Add(helpLinks, 0, 1);
        footer.Controls.Add(NewLabel("Session menu: Ctrl+Shift+F10 when running (if enabled). External Windows overlay, not a PS2 patch.", 9, MutedColor), 0, 2);
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

    private LauncherSettings ReadUi()
    {
        // Retain preferences managed by other windows (PC Input Lab, texture search path).
        // A fresh settings object used to erase those values on every main-window Save.
        var settings = _store.Load();
        settings.Pcsx2Executable = _pcsx2Path.Text.Trim().Trim('"');
        settings.GameImage = _gamePath.Text.Trim().Trim('"');
        settings.Fullscreen = _fullscreen.Checked;
        settings.FastBoot = _fastBoot.Checked;
        settings.HideEmulatorWindow = _hideEmulator.Checked;
        settings.IsolatePcsx2Data = _isolateData.Checked;
        settings.EnableCompanionHotkey = _hotkeyEnabled.Checked;
        return settings;
    }

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
            _hotkeyEnabled.Checked = s.EnableCompanionHotkey;
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
            _hotkeyEnabled.Checked = true;
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

    private DiscInspection InspectSelectedGame()
    {
        var inspected = Ps2DiscIdentifier.Inspect(ReadUi().GameImage);
        _status.Text = inspected.Message;
        return inspected;
    }

    private void CheckImage()
    {
        var result = InspectSelectedGame();
        MessageBox.Show(this, result.Message, "Disc identification", MessageBoxButtons.OK,
            result.Recognition == DiscRecognition.Undercover ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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
        if (_sessionGate.IsRunning)
        {
            _status.Text = "A game is already running; a second launch was blocked.";
            return;
        }
        try
        {
            var config = ReadUi();
            Pcsx2Launcher.Validate(config, launchingGame: true);
            var inspection = InspectSelectedGame();
            if (inspection.Recognition != DiscRecognition.Undercover)
            {
                var choice = MessageBox.Show(this,
                    inspection.Message + Environment.NewLine + Environment.NewLine +
                    "The launcher cannot confirm this is Need for Speed: Undercover. Launch anyway?",
                    "Verify your game image", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (choice != DialogResult.Yes) return;
            }
            if (!SaveSettings(showMessage: false)) return;
            if (_sessionGate.IsRunning)
            {
                _status.Text = "A game session is already running. Use Session menu to manage it.";
                return;
            }
            _gameProcess = Pcsx2Launcher.Start(config, _store.Pcsx2DataDirectory, launchingGame: true);
            if (!_sessionGate.TryEnter())
                throw new InvalidOperationException("A session was started twice unexpectedly.");
            _launchButton.Enabled = false;
            _sessionButton.Enabled = true;
            _sessionTimer.Start();
            var hotkey = config.EnableCompanionHotkey ? TryRegisterCompanionHotkey() : false;
            _status.Text = hotkey
                ? "Undercover running. Ctrl+Shift+F10 opens the companion menu."
                : "Undercover running. Use Session menu here (global hotkey disabled or unavailable).";
            WindowState = FormWindowState.Minimized;
        }
        catch (Exception ex)
        {
            ShowError("Could not start Undercover. Check your PCSX2, BIOS and game image setup.", ex);
        }
    }


    private bool TryRegisterCompanionHotkey()
    {
        if (_hotkeyRegistered) return true;
        _hotkeyRegistered = RegisterHotKey(Handle, HotkeyId, CtrlShiftNoRepeat, VkF10);
        return _hotkeyRegistered;
    }

    private void StopCompanionHotkey()
    {
        if (!_hotkeyRegistered) return;
        UnregisterHotKey(Handle, HotkeyId);
        _hotkeyRegistered = false;
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey && message.WParam.ToInt32() == HotkeyId)
        {
            ToggleCompanionMenu();
            return;
        }
        base.WndProc(ref message);
    }

    private void ToggleCompanionMenu()
    {
        PollSession();
        if (!_sessionGate.IsRunning) return;
        if (_companionMenu is { IsDisposed: false })
        {
            _companionMenu.Close();
            return;
        }
        _companionMenu = new CompanionMenuForm(ReadUi(), SaveNextLaunchOptions, RequestGracefulExit);
        _companionMenu.FormClosed += (_, _) => _companionMenu = null;
        _companionMenu.Show();
        _companionMenu.Activate();
    }

    private void SaveNextLaunchOptions(bool fullscreen, bool fastBoot, bool hideEmulator)
    {
        _fullscreen.Checked = fullscreen;
        _fastBoot.Checked = fastBoot;
        _hideEmulator.Checked = hideEmulator;
        if (SaveSettings(showMessage: false))
            _status.Text = "Companion preferences saved. They take effect on the NEXT launch.";
    }

    private void RequestGracefulExit()
    {
        if (!_sessionGate.IsRunning || _gameProcess is null) return;
        try
        {
            if (_gameProcess.HasExited)
            {
                PollSession();
                return;
            }
            if (_gameProcess.CloseMainWindow())
                _status.Text = "Asked PCSX2 to close. It may ask you to confirm in its own window.";
            else
                _status.Text = "PCSX2 has no closable main window. Exit through its normal game/window controls.";
        }
        catch (InvalidOperationException)
        {
            PollSession();
        }
    }

    private void PollSession()
    {
        if (!_sessionGate.IsRunning || _gameProcess is null) return;
        bool exited;
        try { exited = _gameProcess.HasExited; }
        catch (InvalidOperationException) { exited = true; }
        if (!exited) return;
        _sessionTimer.Stop();
        StopCompanionHotkey();
        _companionMenu?.Close();
        _companionMenu = null;
        _gameProcess.Dispose();
        _gameProcess = null;
        _sessionGate.Release();
        _launchButton.Enabled = true;
        _sessionButton.Enabled = false;
        _status.Text = "PCSX2 session ended. Ready for another launch.";
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
            Activate();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_sessionGate.IsRunning && e.CloseReason == CloseReason.UserClosing)
        {
            var answer = MessageBox.Show(this,
                "Closing Undercover Lite disables the companion hotkey, but leaves PCSX2 running. Close the launcher?",
                "Game still running", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _sessionTimer.Stop();
        _sessionTimer.Dispose();
        StopCompanionHotkey();
        _companionMenu?.Close();
        _gameProcess?.Dispose(); // Never terminates PCSX2.
        base.OnFormClosed(e);
    }

    private void ShowError(string message, Exception ex)
    {
        MessageBox.Show(this, message + Environment.NewLine + Environment.NewLine + ex.Message,
            "Undercover Lite PC", MessageBoxButtons.OK, MessageBoxIcon.Error);
        _status.Text = message;
    }
}