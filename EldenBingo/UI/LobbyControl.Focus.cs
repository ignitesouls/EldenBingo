namespace EldenBingo.UI;

internal enum FocusLayout { BoardOnly, ScoreAndTimer, ScoreTimerAndChat }

internal partial class LobbyControl
{
    internal FocusLayout SelectedFocusLayout => (FocusLayout)Math.Clamp(Properties.Settings.Default.FocusLayout, 0, 2);
    internal void SelectFocusLayout(FocusLayout layout)
    {
        if (!Enum.IsDefined(typeof(FocusLayout), layout)) return;
        Properties.Settings.Default.FocusLayout = (int)layout;
        Properties.Settings.Default.Save();
        if (!_focusMode) SetFocusMode(true);
        else { applyFocusLayout(); updateBingoSize(); FocusModeChanged?.Invoke(this, EventArgs.Empty); }
    }

    private void applyFocusLayout()
    {
        var showStatus = SelectedFocusLayout != FocusLayout.BoardOnly;
        _lobbyStatusPanel.Visible = showStatus;
        _logBoxBorderPanel.Visible = SelectedFocusLayout == FocusLayout.ScoreTimerAndChat;
        var railWidth = showStatus ? Math.Min(Convert.ToInt32(280 * this.DefaultScaleFactors().Width), splitContainer1.Panel1.Width / 2) : 0;
        _lobbyStatusPanel.Bounds = new Rectangle(splitContainer1.Panel1.Width - railWidth, 0, railWidth, splitContainer1.Panel1.Height);
        _bingoBoardPanel.Bounds = new Rectangle(0, 0, Math.Max(1, splitContainer1.Panel1.Width - railWidth), splitContainer1.Panel1.Height);
        updateScoreboardControlLocationAndSize();
    }
    private bool _focusMode;
    private int _focusSidebarWidth;
    internal bool FocusMode => _focusMode;
    internal event EventHandler? FocusModeChanged;
    internal bool CanSearchBoard => _bingoControl.CanSearchBoard;
    internal event EventHandler? SearchAvailabilityChanged;

    internal static void ToggleFocusFor(Client? client)
    {
        if (_instance?.Client == client && client?.Room != null) _instance.SetFocusMode(!_instance._focusMode);
    }
    internal void SearchBoard() => _bingoControl.OpenBoardSearch();
    internal void SetFocusMode(bool enabled)
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired) { BeginInvoke(() => SetFocusMode(enabled)); return; }
        if (enabled == _focusMode) return;
        SuspendLayout();
        if (enabled) _focusSidebarWidth = LayoutSidebarWidth;
        _focusMode = enabled;
        splitContainer1.Panel2Collapsed = enabled;
        _lobbyStatusPanel.Dock = enabled ? DockStyle.None : DockStyle.Fill;
        _bingoBoardPanel.Dock = enabled ? DockStyle.None : DockStyle.Left;
        _lobbyStatusPanel.Visible = !enabled;
        _logBoxBorderPanel.Visible = true;
        if (!enabled && _focusSidebarWidth > 0) setSidebarWidth(_focusSidebarWidth);
        showHideAdminControls();
        ResumeLayout(true);
        updateBingoPanelSize();
        FocusModeChanged?.Invoke(this, EventArgs.Empty);
    }
}
