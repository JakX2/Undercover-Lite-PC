using System.Drawing;
using System.Windows.Forms;
using UndercoverLite.Core;

namespace UndercoverLite;

/// <summary>
/// External configuration/research aid. Never injects keyboard input into PCSX2,
/// alters PS2 game memory, or claims to replace texture hashes without dumps.
/// </summary>
public sealed class PcInputLabForm : Form
{
    private readonly LauncherSettingsStore _store;
    private readonly Dictionary<string, ComboBox> _choices = new();
    private readonly TextBox _desiredName = new() { Text = "DAVID", MaxLength = 24 };
    private readonly TextBox _plan = new();
    private readonly Label _message = new();

    public PcInputLabForm(LauncherSettingsStore store)
    {
        _store = store;
        Text = "Undercover Lite PC | PC input lab";
        Size = new Size(785, 800);
        MinimumSize = new Size(650, 640);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(15, 19, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        BuildUi();
        LoadProfile();
    }

    private void BuildUi()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7,
            Padding = new Padding(22), AutoScroll = true
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 350));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(main);

        main.Controls.Add(new Label
        {
            Text = "PC INPUT LAB", ForeColor = Color.FromArgb(255, 182, 64),
            Dock = DockStyle.Fill, Font = new Font("Segoe UI", 20, FontStyle.Bold)
        }, 0, 0);
        main.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(195, 204, 218),
            Text = "Proposed keyboard labels for Undercover's original PS2 prompts. " +
                   "Saving these labels does NOT change PCSX2 controller mappings or install textures. " +
                   "Map the corresponding buttons manually in PCSX2 2.8.2."
        }, 0, 1);

        var mapping = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10 };
        mapping.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        mapping.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        var labels = new[]
        {
            "Cross / Accept", "Triangle / Back", "Square / Delete", "Start / Done",
            "L1 / Previous tab", "R1 / Next tab", "D-pad Up", "D-pad Down", "D-pad Left", "D-pad Right"
        };
        foreach (var (label, index) in labels.Select((value, index) => (value, index)))
        {
            mapping.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            mapping.Controls.Add(new Label
            {
                Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White
            }, 0, index);
            var picker = new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(32, 39, 51), ForeColor = Color.White
            };
            picker.Items.AddRange(PcPromptLayout.SupportedKeys.Cast<object>().ToArray());
            mapping.Controls.Add(picker, 1, index);
            _choices[label] = picker;
        }
        main.Controls.Add(mapping, 0, 2);
        main.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, Text = "NAME-ENTRY GRID PLANNER (EXPERIMENTAL)",
            Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(255, 182, 64)
        }, 0, 3);
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        _desiredName.Dock = DockStyle.Fill;
        _desiredName.BackColor = Color.FromArgb(32, 39, 51);
        _desiredName.ForeColor = Color.White;
        editor.Controls.Add(_desiredName, 0, 0);
        var calculate = NewButton("Preview steps");
        calculate.Click += (_, _) => PreviewPlan();
        editor.Controls.Add(calculate, 1, 0);
        main.Controls.Add(editor, 0, 4);
        _plan.Multiline = true;
        _plan.ReadOnly = true;
        _plan.ScrollBars = ScrollBars.Vertical;
        _plan.Dock = DockStyle.Fill;
        _plan.BackColor = Color.FromArgb(25, 31, 42);
        _plan.ForeColor = Color.FromArgb(195, 204, 218);
        _plan.Text = "Place the selection on A first. This previews only a POSSIBLE D-pad sequence. " +
                     "It will not type in the game; grid navigation and cursor behavior need testing.";
        main.Controls.Add(_plan, 0, 5);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        _message.Dock = DockStyle.Fill;
        _message.ForeColor = Color.FromArgb(255, 182, 64);
        _message.TextAlign = ContentAlignment.MiddleLeft;
        actions.Controls.Add(_message, 0, 0);
        var save = NewButton("Save key-label suggestions");
        save.Click += (_, _) => Save();
        actions.Controls.Add(save, 1, 0);
        main.Controls.Add(actions, 0, 6);
    }

    private static Button NewButton(string text) => new()
    {
        Text = text, Dock = DockStyle.Fill, BackColor = Color.FromArgb(40, 48, 63),
        ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(4)
    };

    private void LoadProfile()
    {
        try
        {
            var layout = _store.Load().PcPromptLayout ?? new PcPromptLayout();
            foreach (var (action, key) in layout.Actions())
            {
                _choices[action].SelectedItem = key;
                if (_choices[action].SelectedIndex < 0)
                    _choices[action].SelectedItem = new PcPromptLayout().Actions().First(x => x.Action == action).Key;
            }
            _message.Text = "Reference layout only. PCSX2 bindings must match.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Cannot load key suggestions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private PcPromptLayout ReadProfile() => new()
    {
        Accept = Selected("Cross / Accept"),
        Back = Selected("Triangle / Back"),
        Delete = Selected("Square / Delete"),
        Done = Selected("Start / Done"),
        PreviousTab = Selected("L1 / Previous tab"),
        NextTab = Selected("R1 / Next tab"),
        Up = Selected("D-pad Up"),
        Down = Selected("D-pad Down"),
        Left = Selected("D-pad Left"),
        Right = Selected("D-pad Right")
    };

    private string Selected(string action) => _choices[action].SelectedItem?.ToString() ?? "";

    private void Save()
    {
        try
        {
            var profile = ReadProfile();
            profile.Validate();
            var settings = _store.Load();
            settings.PcPromptLayout = profile;
            _store.Save(settings);
            _message.Text = "PC key-label suggestions saved; not applied to PCSX2.";
        }
        catch (Exception ex)
        {
            _message.Text = "Not saved. Review key assignments.";
            MessageBox.Show(this, ex.Message, "Invalid key layout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PreviewPlan()
    {
        try
        {
            var plan = NameEntryGridPlanner.Plan(_desiredName.Text);
            _plan.Text = "Assumes starting cursor at A; no automatic input!\r\n" +
                         "The game may move differently: validate manually.\r\n\r\n" +
                         string.Join("  ", plan.Select(step => step switch
                         {
                             GridAction.Up => "↑", GridAction.Down => "↓", GridAction.Left => "←",
                             GridAction.Right => "→", GridAction.Accept => "[Cross]", _ => "[Start]"
                         }));
        }
        catch (Exception ex)
        {
            _plan.Text = ex.Message;
        }
    }
}