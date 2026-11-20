namespace EldenBingo;

public partial class MainForm
{
    private ToolStripSplitButton _focusButton = null!;
    private ToolStripButton _findSquareButton = null!;
    private void initializeQualityOfLifeButtons()
    {
        var focus = _focusButton = new ToolStripSplitButton("Focus") { ToolTipText = "Toggle focus (F10), or choose a focus layout using the arrow", DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            Image = UI.ToolbarIcons.Focus, ImageScaling = ToolStripItemImageScaling.None, TextImageRelation = TextImageRelation.ImageAboveText, TextAlign = ContentAlignment.BottomCenter, DropDownButtonWidth = 20 };
        focus.ButtonClick += (_, _) => { if (_client?.Room != null) _lobbyControl.SetFocusMode(!_lobbyControl.FocusMode); };
        foreach (var (label, layout) in new[] { ("Board only", UI.FocusLayout.BoardOnly), ("Board + score & timer", UI.FocusLayout.ScoreAndTimer), ("Board + score, timer & chat", UI.FocusLayout.ScoreTimerAndChat) })
        {
            var item = new ToolStripMenuItem(label) { Tag = layout };
            item.Click += (_, _) => { if (_client?.Room != null) _lobbyControl.SelectFocusLayout(layout); };
            focus.DropDownItems.Add(item);
        }
        void refreshFocus()
        {
            focus.Text = _lobbyControl.FocusMode ? "Exit focus" : "Focus";
            foreach (ToolStripMenuItem item in focus.DropDownItems) item.Checked = (UI.FocusLayout)item.Tag! == _lobbyControl.SelectedFocusLayout;
        }
        focus.DropDownOpening += (_, _) => refreshFocus();
        _lobbyControl.FocusModeChanged += (_, _) => refreshFocus();
        var search = _findSquareButton = new ToolStripButton("Find square") { ToolTipText = "Search the revealed board (default F3)", DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            Image = UI.ToolbarIcons.Search, ImageScaling = ToolStripItemImageScaling.None, TextImageRelation = TextImageRelation.ImageAboveText, TextAlign = ContentAlignment.BottomCenter };
        search.Click += (_, _) => _lobbyControl.SearchBoard();
        toolStrip1.Items.Add(new ToolStripSeparator());
        toolStrip1.Items.Add(focus); toolStrip1.Items.Add(search);
        _lobbyControl.SearchAvailabilityChanged += (_, _) => refreshQualityOfLifeButtons();
        Load += (_, _) => refreshQualityOfLifeButtons();
    }

    private void refreshQualityOfLifeButtons()
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired) { BeginInvoke((Action)refreshQualityOfLifeButtons); return; }
        _focusButton.Enabled = _client.Room != null;
        _findSquareButton.Enabled = _lobbyControl.CanSearchBoard;
    }
}
