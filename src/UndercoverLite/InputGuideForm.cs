using System.Drawing;
using System.Windows.Forms;

namespace UndercoverLite;

/// <summary>First-run input guidance only. Does not silently modify PCSX2 bindings.</summary>
public sealed class InputGuideForm : Form
{
    public InputGuideForm()
    {
        Text = "Undercover Lite PC | First-run input setup";
        Size = new Size(650, 570);
        MinimumSize = new Size(520, 460);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(15, 19, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var guide = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BackColor = BackColor,
            ForeColor = ForeColor,
            Font = Font,
            DetectUrls = false,
            Margin = new Padding(18),
            Text = "FIRST-RUN INPUT SETUP\n\n" +
                   "1. Open Configure PCSX2 from the main launcher window.\n\n" +
                   "2. Complete the PCSX2 BIOS wizard using your own PS2 BIOS.\n\n" +
                   "3. Open Settings > Controllers in PCSX2. Configure Controller Port 1. " +
                   "Select a controller or keyboard bindings there.\n\n" +
                   "4. Suggested PC keyboard layout:\n" +
                   "    WASD or arrows  : steering/navigation\n" +
                   "    Enter            : confirm / Start\n" +
                   "    Backspace        : back\n" +
                   "    Left Shift       : nitrous (if assigned)\n" +
                   "    Space            : handbrake (if assigned)\n\n" +
                   "Map these keys to the corresponding PS2 controller buttons " +
                   "used by your chosen in-game control scheme. Undercover Lite does not yet write PCSX2 input configuration.\n\n" +
                   "5. Save changes in PCSX2, close its setup window and launch the game.\n\n" +
                   "Tip: USB/XInput controllers can be configured directly in PCSX2. " +
                   "Custom PC button glyphs and a true in-game PC options menu are future milestones."
        };
        var done = new Button
        {
            Text = "Close guide",
            Dock = DockStyle.Bottom,
            Height = 42,
            BackColor = Color.FromArgb(255, 182, 64),
            ForeColor = Color.FromArgb(20, 23, 28),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        Padding = new Padding(18);
        AcceptButton = done;
        Controls.Add(guide);
        Controls.Add(done);
    }
}