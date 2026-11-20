using EldenBingoCommon;
using EldenBingoServer;
using System.Text;
using System.Data;
using System.ComponentModel;

namespace EldenBingo.UI;

internal sealed class BoardDistributionForm : Form
{
    private readonly TextBox _path = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly GameSettingsControl _options = new() { Dock = DockStyle.Top };
    private readonly NumericUpDown _attempts = new() { Minimum = 1, Maximum = 100000, Value = 1000, Width = 95 };
    private readonly Button _run = new() { Text = "Run test", AutoSize = true };
    private readonly Button _cancel = new() { Text = "Cancel test", AutoSize = true, Enabled = false };
    private readonly Button _preview = new() { Text = "View sample board", AutoSize = true, Enabled = false };
    private readonly Button _export = new() { Text = "Export shown results…", AutoSize = true, Enabled = false };
    private readonly Label _summary = new() { Dock = DockStyle.Top, Height = 68, Text = "Choose a local JSON squareset and bingo game settings. Boards are generated on this computer: no server requests, uploads, or lobby changes.\nFrequencies use successful boards only. Failed generations are reported separately." };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Height = 14 };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly DataGridView[] _grids;
    private readonly DataTable?[] _tables = new DataTable?[6];
    private readonly TextBox _resultSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "Search results…", AccessibleName = "Search distribution results" };
    private readonly Label _resultCount = new() { Dock = DockStyle.Right, Width = 130, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _diagnosticNote = new() { Dock = DockStyle.Bottom, Height = 48, Visible = false };
    private sealed record CategoryChoice(string? Name) { public override string ToString() => Name ?? "All categories"; }
    private readonly ComboBox _categoryFilter = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false, AccessibleName = "Filter objectives by category" };
    private readonly Button _browse;
    private CancellationTokenSource? _cancellation;
    private BoardDistributionReport? _report;
    private bool _closeWhenStopped;

    public BoardDistributionForm(BingoGameSettings settings)
    {
        Text = "Test squareset randomization";
        Icon = Properties.Resources.icon;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1120, 750);
        MinimumSize = new Size(920, 620);
        var split = new SplitContainer { Size = new Size(1080, 600), Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterDistance = 315, Panel1MinSize = 305 };
        _options.Settings = settings;
        if (File.Exists(Properties.Settings.Default.LastBingoFile)) _path.Text = Properties.Settings.Default.LastBingoFile;
        split.Panel1.AutoScroll = true;
        split.Panel1.Padding = new Padding(8);
        split.Panel1.Controls.Add(_options);
        _grids = new[] { "Resolved objectives", "Source objectives", "Categories", "Regions", "Generation failures", "Selection diagnostics" }.Select(title =>
        {
            var page = new TabPage(title);
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                RowHeadersVisible = false, BackgroundColor = SystemColors.Window, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            page.Controls.Add(grid); _tabs.TabPages.Add(page); return grid;
        }).ToArray();
        var searchRow = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(0, 3, 0, 3) };
        var searchLabel = new Label { Text = "Search:", Dock = DockStyle.Left, Width = 54, TextAlign = ContentAlignment.MiddleLeft };
        var clearSearch = new Button { Text = "×", Dock = DockStyle.Right, Width = 28, AccessibleName = "Clear result search" };
        clearSearch.Click += (_, _) => _resultSearch.Clear();
        searchRow.Controls.Add(_resultSearch); searchRow.Controls.Add(_resultCount); searchRow.Controls.Add(clearSearch); searchRow.Controls.Add(searchLabel);
        _resultSearch.TextChanged += (_, _) => filterResults();
        var categoryRow = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 3, 0, 3) };
        var clearCategory = new Button { Text = "Show all", Dock = DockStyle.Right, Width = 75, AccessibleName = "Clear category filter" };
        clearCategory.Click += (_, _) => _categoryFilter.SelectedIndex = 0;
        _categoryFilter.Items.Add(new CategoryChoice(null)); _categoryFilter.SelectedIndex = 0;
        _categoryFilter.SelectedIndexChanged += (_, _) =>
        {
            filterResults();
            if (_categoryFilter.SelectedIndex > 0 && _tabs.SelectedIndex is 2 or 3 or 4) _tabs.SelectedIndex = 1;
        };
        categoryRow.Controls.Add(_categoryFilter); categoryRow.Controls.Add(clearCategory);
        categoryRow.Controls.Add(new Label { Text = "Category:", Dock = DockStyle.Left, Width = 70, TextAlign = ContentAlignment.MiddleLeft });
        var categoryHint = new Label { Text = "Category filters objective and selection statistics. Board-wide totals stay unchanged.", Dock = DockStyle.Top, Height = 32 };
        var showCategory = new Button { Text = "Show selected category's objectives", Dock = DockStyle.Bottom, Height = 30, Enabled = false };
        _grids[2].SelectionChanged += (_, _) => showCategory.Enabled = _grids[2].CurrentRow?.DataBoundItem is DataRowView;
        showCategory.Click += (_, _) => showCategoryObjectives();
        _grids[2].CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) showCategoryObjectives(); };
        _tabs.TabPages[2].Controls.Add(showCategory); _grids[2].BringToFront();
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            updateResultCount();
            _diagnosticNote.Visible = _tabs.SelectedIndex >= 4;
            _diagnosticNote.Text = _tabs.SelectedIndex == 4
                ? "A failed attempt can have several reasons. Percentages use all attempts; reasons can overlap. Blockers describe decisions observed during generation."
                : "Counts cover all attempts, once per objective per reason; rejection reasons may overlap. Selected counts successful boards only. Picked on failed boards shows discarded picks; never reached means selection did not examine that objective.";
        };
        var results = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        results.Controls.Add(_tabs); results.Controls.Add(_diagnosticNote); results.Controls.Add(searchRow); results.Controls.Add(categoryHint); results.Controls.Add(categoryRow); results.Controls.Add(_progress); results.Controls.Add(_summary);
        split.Panel2.Controls.Add(results);
        var fileRow = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(8, 5, 8, 3) };
        _browse = new Button { Text = "Choose JSON…", Dock = DockStyle.Right, Width = 125 };
        _browse.Click += (_, _) =>
        {
            using var file = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", Title = "Choose a squareset to test" };
            if (file.ShowDialog(this) == DialogResult.OK) { _path.Text = file.FileName; clearResults(); }
        };
        fileRow.Controls.Add(_path); fileRow.Controls.Add(_browse);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 43, Padding = new Padding(8), WrapContents = false };
        buttons.Controls.Add(new Label { Text = "Boards to attempt:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
        buttons.Controls.Add(_attempts); buttons.Controls.Add(_run); buttons.Controls.Add(_cancel); buttons.Controls.Add(_preview); buttons.Controls.Add(_export);
        Controls.Add(split); Controls.Add(fileRow); Controls.Add(buttons);
        _run.Click += async (_, _) => await runTest();
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        _preview.Click += (_, _) => showPreview();
        _export.Click += (_, _) => exportResults();
        FormClosing += (_, e) => { if (_cancellation != null) { e.Cancel = true; _closeWhenStopped = true; _cancellation.Cancel(); } };
    }

    private void clearResults()
    {
        _report = null; _preview.Enabled = _export.Enabled = false;
        for (var i = 0; i < _grids.Length; i++) { _grids[i].DataSource = null; _tables[i] = null; }
        _categoryFilter.Items.Clear(); _categoryFilter.Items.Add(new CategoryChoice(null)); _categoryFilter.SelectedIndex = 0; _categoryFilter.Enabled = false;
        updateResultCount();
    }
    private async Task runTest()
    {
        if (string.IsNullOrEmpty(_path.Text)) { _summary.Text = "Choose a JSON squareset first."; return; }
        clearResults();
        _progress.Value = 0;
        _cancellation = new CancellationTokenSource();
        _run.Enabled = _browse.Enabled = _options.Enabled = _attempts.Enabled = false;
        _cancel.Enabled = true;
        _summary.Text = "Generating boards on this computer — no server requests…";
        try
        {
            var json = await File.ReadAllTextAsync(_path.Text);
            var settings = _options.Settings;
            var count = (int)_attempts.Value;
            var token = _cancellation.Token;
            var progress = new Progress<int>(value => { if (!IsDisposed) _progress.Value = value; });
            var report = await Task.Run(() => BoardDistributionTester.Run(json, settings, count, token, progress), token);
            PresentReport(report, settings);
        }
        catch (OperationCanceledException) { _summary.Text = "Test canceled. Run again to generate a complete report."; }
        catch (Exception ex) { _summary.Text = "Could not test this squareset: " + ex.Message; }
        finally
        {
            _cancellation.Dispose(); _cancellation = null;
            _run.Enabled = _browse.Enabled = _options.Enabled = _attempts.Enabled = true;
            _cancel.Enabled = false;
            if (_closeWhenStopped) Close();
        }
    }
    internal void PresentReport(BoardDistributionReport report, BingoGameSettings settings)
    {
        _report = report;
        _progress.Value = 100;
        _summary.Text = $"Local test: {report.Successful:N0} successful / {report.Attempts:N0} attempts · {report.Failed:N0} failed ({100d * report.Failed / report.Attempts:0.##}%)\n" +
            $"Average board points: {report.AveragePoints:0.##}. Frequencies below are per successful board.\nSeed: {(settings.RandomSeed == 0 ? "Random" : settings.RandomSeed)}. Resolved rows show observed variants. Click a column heading to sort.";
        if (report.Objectives.Any(o => o.Timing.Length > 0)) _summary.Text += " Timed values are measured at 00:00.";
        bindRows(0, report.ResolvedObjectives, "Selected DESC, Index ASC, Objective ASC");
        bindRows(1, report.Objectives, "Selected DESC, Index ASC");
        bindRows(2, report.Categories, "Occurrences DESC, Group ASC");
        bindRows(3, report.Regions, "Occurrences DESC, Group ASC");
        bindRows(4, report.GenerationFailures, "Attempts DESC, Reason ASC");
        bindRows(5, report.SelectionDiagnostics, "Selected ASC, Index ASC");
        foreach (var grid in _grids)
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column.ValueType == typeof(double)) column.DefaultCellStyle.Format = "0.##";
                if (column.Name == "BoardPercent") column.HeaderText = "Boards (%)";
                if (column.Name == "CenterSelected") column.HeaderText = "In center";
                if (column.Name == "AveragePerBoard") column.HeaderText = "Average / board";
                if (column.Name == "AttemptPercent") column.HeaderText = "Attempts (%)";
                if (column.Name == "SelectedOnFailedBoards") column.HeaderText = "Picked on failed boards";
                if (column.Name == "CategoryRejected") column.HeaderText = "Category rejected";
                if (column.Name == "RegionRejected") column.HeaderText = "Region rejected";
                if (column.Name == "ForcedCenterExcluded") column.HeaderText = "Unchosen forced center";
                if (column.Name == "CenterRejected") column.HeaderText = "Center placement rejected";
                if (column.Name == "NotReached") column.HeaderText = "Never reached";
                if (column.Name == "PassedOver") column.HeaderText = "Considered, not picked";
                if (column.Name == "Timing") column.HeaderText = "Timing (match seconds)";
            }
        foreach (var grid in _grids.Take(2))
        {
            grid.Columns["Objective"].MinimumWidth = 220;
            grid.Columns["Objective"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            var columnOrder = new[] { "Index", "Objective", "Selected", "BoardPercent", "CenterSelected", "Weight", "Points", "Center", "Categories", "Regions", "Template", "Timing" };
            // Assign from the end so moving one column does not shift a column
            // whose final display position has already been assigned.
            for (var i = columnOrder.Length - 1; i >= 0; i--) grid.Columns[columnOrder[i]].DisplayIndex = 0;
        }
        _grids[1].Columns["Template"].Visible = false;
        _grids[4].Columns["Detail"].MinimumWidth = 360;
        _grids[4].Columns["Detail"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _grids[4].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grids[4].AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
        _grids[5].Columns["Objective"].MinimumWidth = 220;
        _grids[5].Columns["Objective"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        foreach (DataGridViewColumn column in _grids[5].Columns)
            column.ToolTipText = "Counts cover all attempts, once per objective per reason. Rejection reasons may overlap. Selected counts successful boards only.";
        var previousCategory = (_categoryFilter.SelectedItem as CategoryChoice)?.Name;
        _categoryFilter.Items.Clear(); _categoryFilter.Items.Add(new CategoryChoice(null));
        foreach (var name in report.ObjectiveCategories.Values.SelectMany(names => names).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            _categoryFilter.Items.Add(new CategoryChoice(name));
        _categoryFilter.SelectedIndex = Math.Max(0, _categoryFilter.Items.Cast<CategoryChoice>().ToList().FindIndex(c => string.Equals(c.Name, previousCategory, StringComparison.OrdinalIgnoreCase)));
        _categoryFilter.Enabled = _categoryFilter.Items.Count > 1;
        filterResults();
        _preview.Enabled = report.Sample != null;
        _export.Enabled = true;
    }
    private void bindRows<T>(int index, IEnumerable<T> rows, string defaultSort)
    {
        var table = new DataTable { Locale = System.Globalization.CultureInfo.InvariantCulture, CaseSensitive = false };
        var properties = TypeDescriptor.GetProperties(typeof(T)).Cast<PropertyDescriptor>().ToArray();
        foreach (var property in properties) table.Columns.Add(property.Name, property.PropertyType);
        foreach (var row in rows) table.Rows.Add(properties.Select(p => p.GetValue(row)).ToArray());
        table.DefaultView.Sort = defaultSort;
        _tables[index] = table;
        _grids[index].DataSource = table.DefaultView;
        foreach (DataGridViewColumn column in _grids[index].Columns) column.SortMode = DataGridViewColumnSortMode.Automatic;
    }
    private void filterResults()
    {
        var query = _resultSearch.Text.Trim();
        var escaped = string.Concat(query.Select(c => c switch { '\'' => "''", '[' => "[[]", ']' => "[]]", '%' => "[%]", '*' => "[*]", _ => c.ToString() }));
        var category = (_categoryFilter.SelectedItem as CategoryChoice)?.Name;
        var sourceIndexes = category == null || _report == null ? Array.Empty<int>() : _report.ObjectiveCategories
            .Where(p => p.Value.Contains(category, StringComparer.OrdinalIgnoreCase)).Select(p => p.Key).ToArray();
        for (var index = 0; index < _tables.Length; index++)
        {
            var table = _tables[index];
            if (table == null) continue;
            var expressions = new List<string>();
            if (query.Length > 0) expressions.Add("(" + string.Join(" OR ", table.Columns.Cast<DataColumn>()
                .Select(c => $"Convert([{c.ColumnName}], 'System.String') LIKE '%{escaped}%'")) + ")");
            if (category != null && index is 0 or 1 or 5)
                expressions.Add(sourceIndexes.Length == 0 ? "1 = 0" : $"[Index] IN ({string.Join(",", sourceIndexes)})");
            table.DefaultView.RowFilter = string.Join(" AND ", expressions);
        }
        updateResultCount();
    }
    private void showCategoryObjectives()
    {
        if (_grids[2].CurrentRow?.DataBoundItem is not DataRowView row) return;
        var category = (string)row["Group"];
        var index = _categoryFilter.Items.Cast<CategoryChoice>().ToList().FindIndex(c => string.Equals(c.Name, category, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;
        _resultSearch.Clear();
        _categoryFilter.SelectedIndex = index;
        _tabs.SelectedIndex = 1; // Includes objectives selected zero times.
    }
    private void updateResultCount()
    {
        var table = _tables[Math.Max(0, _tabs.SelectedIndex)];
        _resultCount.Text = table == null ? "" : $"{table.DefaultView.Count:N0} of {table.Rows.Count:N0} rows";
    }
    private void showPreview()
    {
        if (_report?.Sample == null) return;
        using var preview = new Form { Text = "Sample generated board", StartPosition = FormStartPosition.CenterParent, Size = new Size(820, 780), MinimumSize = new Size(320, 320) };
        var board = new BingoControl { Dock = DockStyle.Fill };
        preview.Controls.Add(board);
        board.PreviewBoard(_report.Sample);
        preview.ShowDialog(this);
    }
    private void exportResults()
    {
        if (_report == null) return;
        using var file = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "squareset-distribution.csv" };
        if (file.ShowDialog(this) != DialogResult.OK) return;
        string quote(object? value) => "\"" + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Replace("\"", "\"\"") + "\"";
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine($"Attempts,{_report.Attempts},Successful,{_report.Successful},Failed,{_report.Failed}");
            csv.AppendLine("Search," + quote(_resultSearch.Text.Trim()));
            csv.AppendLine("Objective category," + quote((_categoryFilter.SelectedItem as CategoryChoice)?.Name ?? "All categories"));
            for (var i = 0; i < _grids.Length; i++)
            {
                csv.AppendLine(); csv.AppendLine(_tabs.TabPages[i].Text);
                var grid = _grids[i];
                var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToArray();
                csv.AppendLine(string.Join(",", columns.Select(c => quote(c.HeaderText))));
                foreach (DataGridViewRow row in grid.Rows) csv.AppendLine(string.Join(",", columns.Select(c => quote(row.Cells[c.Index].Value))));
            }
            File.WriteAllText(file.FileName, csv.ToString(), new UTF8Encoding(true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, "Could not export results", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
