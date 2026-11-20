using EldenBingo.Export;

namespace EldenBingo.UI;

internal sealed class LiveShareForm : Form
{
    private readonly CheckBox _enabled;
    private readonly NumericUpDown _slot, _port;
    public bool SharingEnabled => _enabled.Checked;
    public int Slot => (int)_slot.Value;
    public int Port => (int)_port.Value;

    public LiveShareForm(int slot, int port, string? activeUrl)
    {
        Text = "Local JSON URL";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 345);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        var rows = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(14) };
        rows.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(520, 0), Text = "Serve this lobby's match JSON on your computer. Squares are revealed at Preparation (or Running when preparation is skipped). No server setup is required." });
        _enabled = new CheckBox { Text = "Enable local JSON URL for this lobby", Checked = true, AutoSize = true, Margin = new Padding(3, 12, 3, 8) };
        rows.Controls.Add(_enabled);
        _slot = new NumericUpDown { Minimum = 1, Maximum = 99, Value = slot, Width = 75, Enabled = activeUrl == null, AccessibleName = "Local lobby number" };
        _port = new NumericUpDown { Minimum = 1024, Maximum = 65535, Value = port, Width = 90, Enabled = activeUrl == null, AccessibleName = "Local JSON port" };
        var options = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        options.Controls.AddRange(new Control[] { new Label { Text = "Lobby number", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, _slot,
            new Label { Text = "Port", AutoSize = true, Padding = new Padding(12, 5, 0, 0) }, _port });
        rows.Controls.Add(options);
        rows.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(520, 0), Text = "Use Lobby 2 for another open app instance. Each slot has a stable URL. Stop sharing first to change its slot or port.", Margin = new Padding(3, 8, 3, 8) });
        rows.Controls.Add(new Label { Text = "Local JSON URL", AutoSize = true });
        var url = new TextBox { Width = 520, ReadOnly = true, AccessibleName = "Local JSON URL" };
        void preview() => url.Text = activeUrl ?? LocalJsonPublisher.UrlForSlot(Slot, Port).AbsoluteUri;
        _slot.ValueChanged += (_, _) => preview();
        _port.ValueChanged += (_, _) => preview();
        preview();
        rows.Controls.Add(url);
        var copy = new Button { Text = "Copy JSON URL", AutoSize = true, Enabled = activeUrl != null };
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(url.Text); }
            catch (System.Runtime.InteropServices.ExternalException) { MessageBox.Show(this, "The clipboard is busy. Try copying again.", Text); }
        };
        rows.Controls.Add(copy);
        rows.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(520, 0), Text = "Works on this computer only. A hosted page may ask for local-network permission. Leaving the lobby stops the endpoint.", Margin = new Padding(3, 8, 3, 3) });
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(3, 10, 3, 3) };
        var apply = new Button { Text = activeUrl == null ? "Start local URL" : "Keep running", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
        _enabled.CheckedChanged += (_, _) => apply.Text = !_enabled.Checked ? "Stop local URL" : activeUrl == null ? "Start local URL" : "Keep running";
        actions.Controls.AddRange(new Control[] { apply, cancel });
        rows.Controls.Add(actions);
        Controls.Add(rows);
        AcceptButton = apply;
        CancelButton = cancel;
    }
}
