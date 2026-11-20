namespace EldenBingo.UI;

internal sealed record ExportHealth(bool Active, bool Error, string Detail, DateTimeOffset? LastSuccess = null)
{
    public string Summary => Error ? "Needs attention" : !Active ? "Off" : LastSuccess == null ? "Starting…" : "Live";
    public string Description => Detail + (LastSuccess is { } time ? $"\nLast successful update: {time.ToLocalTime():HH:mm:ss}" : "");
    public static ExportHealth Off => new(false, false, "Off");
}

internal sealed class LobbyCodeCopyButton : Button
{
    private readonly Action<string> _copy;
    private readonly System.Windows.Forms.Timer _feedback = new() { Interval = 2000 };
    private string? _code;
    private bool _copied;
    private readonly ToolTip _tip = new();

    public LobbyCodeCopyButton(Action<string>? copy = null)
    {
        _copy = copy ?? Clipboard.SetText;
        Text = "";
        AccessibleName = "Copy lobby code";
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = true;
        Enabled = false;
        _feedback.Tick += (_, _) => { _feedback.Stop(); _copied = false; updateDescription(); Invalidate(); };
    }

    public void SetLobby(string? code)
    {
        if (_code != code) { _feedback.Stop(); _copied = false; }
        _code = code;
        Enabled = !string.IsNullOrEmpty(code);
        updateDescription();
        Invalidate();
    }

    internal bool CopyLobbyCode()
    {
        if (string.IsNullOrEmpty(_code)) return false;
        try
        {
            _copy(_code);
            _copied = true;
            updateDescription(); Invalidate();
            _feedback.Stop(); _feedback.Start();
            return true;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _copied = false;
            AccessibleDescription = "Could not copy. Click to try again.";
            _tip.SetToolTip(this, AccessibleDescription);
            _feedback.Stop(); _feedback.Start();
            return false;
        }
    }

    private void updateDescription()
    {
        AccessibleDescription = _copied ? "Copied lobby code: " + _code : Enabled ? "Copy lobby code: " + _code : "Join or create a lobby to copy its code.";
        _tip.SetToolTip(this, AccessibleDescription);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        CopyLobbyCode();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(Enabled ? ForeColor : SystemColors.GrayText, 1.5f);
        var x = (Width - 15) / 2;
        var y = (Height - 16) / 2;
        if (_copied) e.Graphics.DrawLines(pen, new[] { new Point(x, y + 8), new Point(x + 5, y + 13), new Point(x + 15, y + 2) });
        else
        {
            e.Graphics.DrawRectangle(pen, x + 4, y + 4, 10, 11);
            e.Graphics.DrawLines(pen, new[] { new Point(x + 2, y + 11), new Point(x, y + 11), new Point(x, y), new Point(x + 10, y), new Point(x + 10, y + 2) });
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _feedback.Dispose(); _tip.Dispose(); }
        base.Dispose(disposing);
    }
}
