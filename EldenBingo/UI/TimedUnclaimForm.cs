namespace EldenBingo.UI;

internal sealed class TimedUnclaimForm : Form
{
    public TimedUnclaimForm(string objective)
    {
        Text = "Unclaim timed square";
        ClientSize = new Size(540, 225);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        Controls.Add(new Label { Text = objective, AutoEllipsis = true, Location = new Point(16, 14), Size = new Size(508, 44), Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold) });
        Controls.Add(new Label { Text = "Resume: remove your claim and continue at the current match time. The requirement may increase immediately.\n\nKeep frozen: remove your claim but leave this objective unchanged. Resuming is allowed only when no other team still claims it.",
            Location = new Point(16, 65), Size = new Size(508, 100) });
        var resume = new Button { Text = "Resume timed square", DialogResult = DialogResult.Yes, Location = new Point(16, 181), Size = new Size(175, 29) };
        var keep = new Button { Text = "Keep frozen", DialogResult = DialogResult.No, Location = new Point(204, 181), Size = new Size(150, 29) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(399, 181), Size = new Size(125, 29) };
        Controls.AddRange(new Control[] { resume, keep, cancel });
        AcceptButton = keep;
        CancelButton = cancel;
        ActiveControl = keep;
    }
}
