using System.Drawing;
using System.Windows.Forms;
using UndercoverLite.Core;

namespace UndercoverLite;

/// <summary>Opt-in, out-of-game steering profile selection. Never changes the running emulator.</summary>
public sealed class SteeringLabForm : Form
{
    private readonly LauncherSettingsStore _store;
    private readonly bool _running;
    private readonly ComboBox _strength = new();
    private readonly CheckBox _menu = new();
    private readonly TextBox _preview = new();

    public SteeringLabForm(LauncherSettingsStore store, bool gameRunning)
    {
        _store = store;
        _running = gameRunning;
        Text = "Undercover Lite PC | Steering and menu controls 0.10";
        Size = new Size(755, 660);
        MinimumSize = new Size(595, 520);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(15, 19, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(20)
        };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        Controls.Add(grid);

        grid.Controls.Add(new Label
        {
            Text = "STEERING LAB", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 182, 64)
        }, 0, 0);

        var selection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        selection.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        selection.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        selection.Controls.Add(new Label
        {
            Text = "Keyboard steering strength", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _strength.Dock = DockStyle.Fill;
        _strength.DropDownStyle = ComboBoxStyle.DropDownList;
        _strength.Items.AddRange(["Gentle (65%)", "Balanced (85%)", "Full (100%)"]);
        _strength.SelectedIndex = 1;
        _strength.SelectedIndexChanged += (_, _) => RefreshPreview();
        selection.Controls.Add(_strength, 1, 0);
        _menu.Text = "Also bind Enter = Cross and Backspace = Triangle for easier menus";
        _menu.Dock = DockStyle.Fill;
        _menu.CheckedChanged += (_, _) => RefreshPreview();
        selection.Controls.Add(_menu, 0, 1);
        selection.SetColumnSpan(_menu, 2);
        grid.Controls.Add(selection, 0, 1);

        grid.Controls.Add(new Label
        {
            Text = "This affects both menus and driving. No live switching, keyboard smoothing or game patches.",
            ForeColor = Color.FromArgb(206, 214, 226), Dock = DockStyle.Fill
        }, 0, 2);
        _preview.Multiline = true;
        _preview.ScrollBars = ScrollBars.Vertical;
        _preview.ReadOnly = true;
        _preview.WordWrap = true;
        _preview.Dock = DockStyle.Fill;
        _preview.BackColor = Color.FromArgb(25, 31, 42);
        _preview.ForeColor = Color.White;
        _preview.Font = new Font("Consolas", 9.5F);
        grid.Controls.Add(_preview, 0, 3);

        var buttons = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        var install = NewButton("Install selected profile...");
        install.Click += (_, _) => Install();
        buttons.Controls.Add(install, 0, 0);
        var close = NewButton("Close");
        close.Click += (_, _) => Close();
        buttons.Controls.Add(close, 1, 0);
        grid.Controls.Add(buttons, 0, 4);
        RefreshPreview();
    }

    private static Button NewButton(string text) => new()
    {
        Text = text, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(40, 48, 63), ForeColor = Color.White,
        Margin = new Padding(4)
    };

    private SteeringProfiles.Choice Choice()
    {
        var strength = _strength.SelectedIndex switch
        {
            0 => SteeringProfiles.Strength.Gentle,
            2 => SteeringProfiles.Strength.Full,
            _ => SteeringProfiles.Strength.Balanced
        };
        return new(strength, _menu.Checked);
    }

    private void RefreshPreview()
    {
        try
        {
            var choice = Choice();
            var inspection = SteeringProfiles.Inspect(_store.Pcsx2DataDirectory, choice);
            var audit = Pcsx2HotkeyAudit.Scan(_store.Pcsx2DataDirectory);
            _preview.Text =
                $"Profile: {choice.Name}\r\n" +
                $"Install status: {inspection.Status}\r\n" +
                $"Steering: {SteeringProfiles.Percent(choice.Steering)}% left/right stick output.\r\n" +
                "Note: scales the maximum digital-key stick magnitude. It DOES NOT gradually ramp steering.\r\n\r\n" +
                "Keyboard: W/S accelerate/brake, A/D steer, Space handbrake, Shift nitrous,\r\n" +
                "Q/E gears, X speedbreaker, F pause, arrows D-pad navigation.\r\n" +
                (_menu.Checked
                    ? "Extra keys: Enter confirms (also accelerates), Backspace goes back (also handbrakes).\r\n"
                    : "Extra menu keys disabled; W = Accept and Space = Back.\r\n") +
                "\r\nPCSX2 HOTKEY CHECK:\r\n" +
                string.Join("\r\n", audit.Findings.Select(f => "- " + f.Description)) +
                "\r\n\r\nThis tool WILL NOT modify PCSX2 hotkeys or activate any profile automatically.\r\n" +
                "If Space toggles PCSX2 pause, rebind that emulator hotkey manually before driving.";
            _preview.SelectionStart = 0;
            _preview.SelectionLength = 0;
        }
        catch (Exception ex)
        {
            _preview.Text = "Cannot preview profile: " + ex.GetType().Name +
                "\r\nVerify that the isolated PCSX2 data directory is accessible.";
        }
    }

    private void Install()
    {
        if (_running)
        {
            MessageBox.Show(this, "Close the game and PCSX2 before changing input profiles.",
                "Steering profiles", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            var choice = Choice();
            var settings = _store.Load();
            var preview = SteeringProfiles.Inspect(_store.Pcsx2DataDirectory, choice);
            if (preview.Status == UndercoverKeyboardProfile.InstallStatus.Conflict)
                throw new IOException("A different input profile already occupies this name. No files were changed.");
            if (!settings.IsolatePcsx2Data)
                throw new InvalidOperationException("Turn on isolated PCSX2 settings in the launcher first.");

            if (MessageBox.Show(this,
                $"Install {choice.Name}?\n\n" +
                "This creates an additional, separate PCSX2 profile without overwriting shared bindings.\n" +
                "It will NOT activate itself automatically or modify hotkeys.\n\n" +
                "Remember: Space may still be bound to PCSX2 TogglePause. Verify it in PCSX2 Hotkeys.\n" +
                "After installation, select the new profile in Undercover Properties > Summary > Input Profile.",
                "Confirm steering profile installation", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;
            var installed = SteeringProfiles.Install(settings, _store.Pcsx2DataDirectory, choice);
            MessageBox.Show(this,
                $"{choice.Name} is available.\n\n" +
                "Choose it under Undercover > Properties > Summary > Input Profile in PCSX2, " +
                "and review the Space hotkey conflict before racing.",
                "Steering profile installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Steering profile not installed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
