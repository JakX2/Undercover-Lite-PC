using System.Drawing;
using System.Windows.Forms;
using UndercoverLite.Core;

namespace UndercoverLite;

/// <summary>
/// Non-injected, external Windows companion panel. These preferences affect
/// NEXT launch, never live PCSX2 graphics or the emulated PS2 pause menu.
/// </summary>
public sealed class CompanionMenuForm : Form
{
    public CompanionMenuForm(
        LauncherSettings settings,
        Action<bool, bool, bool> saveNextLaunch,
        Action requestGracefulExit)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(saveNextLaunch);
        ArgumentNullException.ThrowIfNull(requestGracefulExit);

        Text = "Undercover Lite PC | Companion menu";
        Size = new Size(650, 430);
        MinimumSize = new Size(540, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        BackColor = Color.FromArgb(15, 19, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1,
            Padding = new Padding(24, 20, 24, 18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "SESSION COMPANION", ForeColor = Color.FromArgb(255, 182, 64),
            Font = new Font("Segoe UI", 18, FontStyle.Bold), Dock = DockStyle.Fill
        }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Not the PS2 pause menu. These options are saved for the NEXT game launch.\n" +
                   "Live renderer, input and audio settings remain controlled by PCSX2.",
            ForeColor = Color.FromArgb(180, 190, 208), Dock = DockStyle.Fill
        }, 0, 1);

        var nextFullscreen = NewOption("Start fullscreen next time", settings.Fullscreen);
        var nextFastBoot = NewOption("Fast boot next time", settings.FastBoot);
        var nextHideUi = NewOption("Hide PCSX2 main window next time", settings.HideEmulatorWindow);
        root.Controls.Add(nextFullscreen, 0, 2);
        root.Controls.Add(nextFastBoot, 0, 3);
        root.Controls.Add(nextHideUi, 0, 4);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        var save = NewButton("Save for next launch");
        save.Click += (_, _) =>
        {
            saveNextLaunch(nextFullscreen.Checked, nextFastBoot.Checked, nextHideUi.Checked);
            Close();
        };
        var close = NewButton("Return to game");
        close.Click += (_, _) => Close();
        var exit = NewButton("Request game exit");
        exit.Click += (_, _) =>
        {
            requestGracefulExit();
            Close();
        };
        actions.Controls.Add(save, 0, 0);
        actions.Controls.Add(close, 1, 0);
        actions.Controls.Add(exit, 2, 0);
        root.Controls.Add(actions, 0, 5);
        CancelButton = close;
    }

    private static CheckBox NewOption(string label, bool value) => new()
    {
        Text = label, Checked = value, Dock = DockStyle.Fill,
        ForeColor = Color.White, AutoSize = false
    };

    private static Button NewButton(string label) => new()
    {
        Text = label, Dock = DockStyle.Fill,
        Margin = new Padding(4, 18, 4, 6),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(40, 48, 63), ForeColor = Color.White
    };
}