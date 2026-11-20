namespace EldenBingo.UI;

internal partial class BingoControl
{
    private Panel _searchPanel = null!;
    private TextBox _boardSearch = null!;
    private Label _searchCount = null!;
    private TableLayoutPanel _searchRow = null!;
    internal bool CanSearchBoard => Squares != null && Squares.Length > 0 && _boardStatus == BoardStatusEnum.BoardRevealed;
    internal event EventHandler? SearchAvailabilityChanged;

    private void initializeBoardSearch()
    {
        _searchPanel = new Panel { Dock = DockStyle.Top, Height = 34, Visible = false, BackColor = Color.Transparent };
        _searchRow = new TableLayoutPanel { Height = 30, RowCount = 1, ColumnCount = 3, BackColor = SystemColors.Control, Padding = new Padding(2), Margin = Padding.Empty };
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));
        _boardSearch = new TextBox { Dock = DockStyle.Fill, AutoSize = false, Margin = Padding.Empty, PlaceholderText = "Find an objective…", AccessibleName = "Search revealed board", Font = SystemFonts.MessageBoxFont };
        _searchCount = new Label { Dock = DockStyle.Fill, Margin = Padding.Empty, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleCenter, ForeColor = SystemColors.ControlText, Font = SystemFonts.MessageBoxFont };
        var close = new Button { Text = "×", Dock = DockStyle.Fill, Margin = Padding.Empty, AccessibleName = "Close board search" };
        close.Click += (_, _) => closeBoardSearch();
        _boardSearch.TextChanged += (_, _) => updateSearchMatches();
        _boardSearch.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; closeBoardSearch(); } };
        _searchRow.Controls.Add(_boardSearch, 0, 0); _searchRow.Controls.Add(_searchCount, 1, 0); _searchRow.Controls.Add(close, 2, 0);
        _searchPanel.Controls.Add(_searchRow);
        Controls.Add(_searchPanel); _searchPanel.SendToBack();
        _gridControl.SizeChanged += (_, _) => alignBoardSearch();
        _gridControl.LocationChanged += (_, _) => alignBoardSearch();
        alignBoardSearch();
    }

    private void alignBoardSearch()
    {
        if (_searchRow == null) return;
        _searchRow.SetBounds(_gridControl.Left, 0, _gridControl.Width, 30);
        _searchRow.ColumnStyles[1].Width = Math.Min(82, Math.Max(40, _gridControl.Width / 4));
    }

    internal void OpenBoardSearch()
    {
        if (!CanSearchBoard) return;
        _searchPanel.Visible = true;
        alignBoardSearch();
        _boardSearch.Focus(); _boardSearch.SelectAll();
    }

    private void closeBoardSearch()
    {
        _boardSearch.Clear(); _searchPanel.Visible = false; Focus();
    }

    private void refreshSearchAvailability()
    {
        if (!CanSearchBoard) { _boardSearch.Clear(); _searchPanel.Visible = false; }
        updateSearchMatches();
        SearchAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void updateSearchMatches()
    {
        if (Squares == null) return;
        var query = CanSearchBoard ? _boardSearch.Text.Trim() : "";
        var count = 0;
        foreach (var square in Squares)
        {
            var match = query.Length > 0 && square.Text.Replace("&&", "&").Contains(query, StringComparison.InvariantCultureIgnoreCase);
            if (match) count++;
            if (square.SearchMatched != match) { square.SearchMatched = match; square.Invalidate(); }
        }
        _searchCount.Text = query.Length == 0 ? "" : count == 1 ? "1 match" : $"{count} matches";
        _searchCount.AccessibleDescription = _searchCount.Text;
    }
}
