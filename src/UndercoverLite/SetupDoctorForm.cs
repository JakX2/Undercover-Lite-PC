using System.Drawing;
using System.Windows.Forms;
using UndercoverLite.Core;

namespace UndercoverLite;

/// <summary>A read-only diagnostic screen; it never changes emulator or game settings.</summary>
public sealed class SetupDoctorForm : Form
{
    private readonly LauncherSettings _settings;
    private readonly string _pcsx2DataDirectory;
    private readonly TextBox _summary = new();
    private readonly Label _headline = new();
    private SetupDoctor.Result? _last;

    public SetupDoctorForm(LauncherSettings settings, string pcsx2DataDirectory)
    {
        _settings = settings;
        _pcsx2DataDirectory = pcsx2DataDirectory;
        Text = "Undercover Lite PC | Setup Doctor 0.8";
        Size = new Size(810, 650);
        MinimumSize = new Size(610, 460);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(15, 19, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 57));
        Controls.Add(layout);
        layout.Controls.Add(new Label
        {
            Text = "SETUP DOCTOR", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 182, 64)
        }, 0, 0);
        _headline.Dock = DockStyle.Fill;
        _headline.ForeColor = Color.FromArgb(205, 212, 224);
        layout.Controls.Add(_headline, 0, 1);
        _summary.Dock = DockStyle.Fill;
        _summary.Multiline = true;
        _summary.ReadOnly = true;
        _summary.WordWrap = true;
        _summary.ScrollBars = ScrollBars.Vertical;
        _summary.Font = new Font("Consolas", 10);
        _summary.BackColor = Color.FromArgb(24, 30, 41);
        _summary.ForeColor = Color.White;
        layout.Controls.Add(_summary, 0, 2);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        var refresh = Button("Refresh checks");
        refresh.Click += (_, _) => RunChecks();
        var copy = Button("Copy safe report");
        copy.Click += (_, _) =>
        {
            if (_last is not null)
            {
                try { Clipboard.SetText(_last.CopyableReport()); }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    MessageBox.Show(this, "Clipboard was unavailable. Try copying again.",
                        "Copy report", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        };
        var close = Button("Close");
        close.Click += (_, _) => Close();
        actions.Controls.Add(refresh, 0, 0);
        actions.Controls.Add(copy, 1, 0);
        actions.Controls.Add(close, 2, 0);
        layout.Controls.Add(actions, 0, 3);

        RunChecks();
    }

    private static Button Button(string label) => new()
    {
        Text = label, Dock = DockStyle.Fill, Margin = new Padding(5),
        BackColor = Color.FromArgb(40, 48, 63), ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat
    };

    private void RunChecks()
    {
        // A copied diagnostic never reveals the user's game or BIOS paths.
        try
        {
            _last = SetupDoctor.Check(_settings, _pcsx2DataDirectory);
            _headline.Text = _last.Blockers > 0
                ? $"{_last.Blockers} blocking issue(s), {_last.ActionItems - _last.Blockers} attention item(s)."
                : _last.ActionItems > 0
                    ? $"{_last.ActionItems} item(s) to review. A launch may still work."
                    : "No blocking issues found. BIOS, controls and gameplay still need PCSX2 testing.";
            _summary.Text = _last.CopyableReport();
            _summary.SelectionStart = 0;
            _summary.SelectionLength = 0;
        }
        catch (Exception ex)
        {
            _last = null;
            _headline.Text = "Diagnostic could not finish.";
            _summary.Text = $"The read-only check stopped ({ex.GetType().Name}).\r\n" +
                "Please check your settings and file permissions. Paths were not displayed.";
        }
    }
}
