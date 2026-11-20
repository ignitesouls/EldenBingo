using EldenBingo.Settings;
using System.Text.Json;

namespace EldenBingo.UI;

public partial class SettingsDialog
{
    private BoardHotkeys _boardHotkeys = new();
    private readonly Dictionary<BoardAction, TextBox> _hotkeyFields = new();
    private BoardAction? _capturingAction;
    private Label _hotkeyFeedback = null!;
    private CheckBox _scrollKeyboardSquare = null!;
    private CheckBox _middleMouseDims = null!;

    private sealed class HotkeyCaptureBox : TextBox
    {
        protected override bool IsInputKey(Keys keyData) => true;
    }

    private TabPage createKeyboardPage()
    {
        var settings = Properties.Settings.Default;
        _boardHotkeys = BoardHotkeys.Parse(settings.BoardHotkeysJson, BoardHotkeys.FromLegacy(settings.ClickHotkey, settings.NumpadNavigation, settings.ArrowNavigation));
        var page = new TabPage { Padding = new Padding(8) };
        var tabs = new TabControl { Dock = DockStyle.Fill, AccessibleName = "Input settings tabs" };
        var keysPage = new TabPage("Key bindings") { Padding = new Padding(8), AutoScroll = true };
        var mousePage = new TabPage("Mouse controls") { Padding = new Padding(12), AutoScroll = true };
        tabs.TabPages.Add(keysPage); tabs.TabPages.Add(mousePage);

        GroupBox bindingsGroup(string title, IEnumerable<(BoardAction Action, string Label)> actions)
        {
            var entries = actions.ToArray();
            var group = new GroupBox { Text = title, Width = 316, Height = 31 + entries.Length * 29, Padding = new Padding(8), Margin = new Padding(0, 0, 12, 12) };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = entries.Length };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
            for (var row = 0; row < entries.Length; row++)
            {
                var (action, label) = entries[row];
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
                var field = new HotkeyCaptureBox { ReadOnly = true, Dock = DockStyle.Fill, AccessibleName = label + " hotkey", Margin = new Padding(0, 2, 4, 4) };
                _hotkeyFields[action] = field;
                field.Enter += (_, _) => beginHotkeyCapture(action);
                field.Click += (_, _) => beginHotkeyCapture(action);
                field.Leave += (_, _) => endHotkeyCapture();
                var clear = new Button { Text = "\u00d7", Dock = DockStyle.Fill, AccessibleName = "Clear " + label + " hotkey", Margin = new Padding(0, 2, 0, 4) };
                clear.Click += (_, _) => { endHotkeyCapture(); _boardHotkeys.Bind(action, Keys.None); refreshHotkeyFields(); _hotkeyFeedback.Text = label + " cleared. Save settings to keep changes."; };
                table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, row);
                table.Controls.Add(field, 1, row); table.Controls.Add(clear, 2, row);
            }
            group.Controls.Add(table);
            return group;
        }
        var groups = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
        groups.Controls.Add(bindingsGroup("Movement", BoardHotkeys.Actions.Where(a => BoardHotkeys.IsMovement(a.Action))));
        var actionGroups = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = Padding.Empty, Width = 316 };
        actionGroups.Controls.Add(bindingsGroup("Square actions", BoardHotkeys.Actions.Where(a => a.Action is BoardAction.MarkSquare or BoardAction.StarSquare or BoardAction.IncrementCount or BoardAction.DecrementCount or BoardAction.DimSquare)));
        actionGroups.Controls.Add(bindingsGroup("Board and window", BoardHotkeys.Actions.Where(a => a.Action is BoardAction.ToggleFocus or BoardAction.SearchBoard)));
        groups.Controls.Add(actionGroups);

        var modifiers = new GroupBox { Text = "Required modifiers", Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10) };
        var modifierRows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2 };
        modifierRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        modifierRows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modifierRows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var modifierChecks = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0, 6, 0, 2), AccessibleName = "Required modifier keys" };
        foreach (var (flag, text) in new[] { (BoardHotkeyModifiers.Shift, "Shift"), (BoardHotkeyModifiers.Control, "Control"), (BoardHotkeyModifiers.LeftAlt, "Left Alt") })
        {
            var check = new CheckBox { Text = text, AutoSize = true, Checked = (_boardHotkeys.RequiredModifiers & flag) != 0, Margin = new Padding(0, 3, 20, 3) };
            check.CheckedChanged += (_, _) => _boardHotkeys.RequiredModifiers = check.Checked ? _boardHotkeys.RequiredModifiers | flag : _boardHotkeys.RequiredModifiers & ~flag;
            modifierChecks.Controls.Add(check);
        }
        var modifierHint = new Label { Text = "Hold every checked modifier to use keyboard shortcuts. Leave all unchecked for direct keys.", AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        modifierRows.Controls.Add(modifierHint, 0, 0);
        modifierRows.Controls.Add(modifierChecks, 0, 1);
        modifiers.Controls.Add(modifierRows);

        var presets = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 66, WrapContents = true, Padding = new Padding(0, 4, 0, 4), AccessibleName = "Binding presets" };
        void preset(string text, bool numpad, bool arrows)
        {
            var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 0, 8, 4) };
            button.Click += (_, _) =>
            {
                endHotkeyCapture();
                var held = _boardHotkeys.RequiredModifiers;
                _boardHotkeys = numpad || arrows ? BoardHotkeys.FromLegacy((int)Keys.Space, numpad, arrows) : new BoardHotkeys { Version = 3 };
                _middleMouseDims.Checked = numpad || arrows;
                _boardHotkeys.RequiredModifiers = held;
                refreshHotkeyFields();
                _hotkeyFeedback.Text = text + " loaded. Save settings to apply.";
            };
            presets.Controls.Add(button);
        }
        preset("Restore defaults", true, true);
        preset("Arrow defaults", false, true);
        preset("Numpad defaults", true, false);
        preset("Clear all bindings", false, false);
        var bindingHint = new Label { Dock = DockStyle.Top, Height = 38, Text = "Select a binding and press a key. Left/Right Ctrl supported. Escape cancels; \u00d7 disables that action." };
        keysPage.Controls.Add(groups); keysPage.Controls.Add(modifiers); keysPage.Controls.Add(presets); keysPage.Controls.Add(bindingHint);
        groups.BringToFront();

        GroupBox mouseGroup(string title, CheckBox toggle, string explanation)
        {
            var group = new GroupBox { Text = title, Dock = DockStyle.Top, Height = 118, Padding = new Padding(10) };
            toggle.Dock = DockStyle.Top; toggle.Height = 36;
            group.Controls.Add(new Label { Text = explanation, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) });
            group.Controls.Add(toggle);
            return group;
        }
        _scrollKeyboardSquare = new CheckBox { Text = "Allow scrolling the keyboard-selected square", AutoSize = false, Checked = settings.ScrollKeyboardSelectedSquare };
        _middleMouseDims = new CheckBox { Text = "Middle-click to dim or restore a square", AutoSize = false, Checked = settings.MiddleMouseDimsSquares };
        var wheel = mouseGroup("Scroll wheel counters", _scrollKeyboardSquare, "Off by default. Normally the pointer must hover over a square. When enabled, navigation-key selection can also target a counter. Mouse hover takes priority.");
        var dimming = mouseGroup("Private square dimming", _middleMouseDims, "On by default. Dimming is private and syncs with your pop-out. It changes no claims or scores. The keyboard binding is set separately under Key bindings.");
        var mouseHint = new Label { Dock = DockStyle.Top, Height = 52, Padding = new Padding(0, 12, 0, 0), Text = "Mouse actions do not require the keyboard modifiers above. Input pauses while typing in the app or using a dialog." };
        var guide = new Button { Text = "Open board controls guide", AutoSize = true, Dock = DockStyle.Top, Height = 30 };
        guide.Click += (_, _) =>
        {
            SelectHelpGuides();
            var topics = tabControl1.SelectedTab!.Controls.OfType<TabControl>().Single();
            topics.SelectedTab = topics.TabPages.Cast<TabPage>().Single(p => p.Text == "Board controls");
        };
        mousePage.Controls.Add(guide); mousePage.Controls.Add(mouseHint); mousePage.Controls.Add(dimming); mousePage.Controls.Add(wheel);
        _hotkeyFeedback = new Label { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(0, 8, 0, 0), Text = "Save settings applies changes. Cancel discards them. Cleared bindings stay disabled after restarting.", AccessibleName = "Input settings feedback" };
        page.Controls.Add(tabs); page.Controls.Add(_hotkeyFeedback);
        page.Controls.Add(new Label { Text = "Keyboard and mouse", Dock = DockStyle.Top, Height = 40, Padding = new Padding(0, 8, 0, 0), Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12, FontStyle.Bold) });
        tabs.BringToFront();
        refreshHotkeyFields();
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (_capturingAction == null) return;
            e.Handled = e.SuppressKeyPress = true;
            captureHotkey(e.KeyCode);
        };
        return page;
    }

    private void beginHotkeyCapture(BoardAction action)
    {
        _capturingAction = action;
        AcceptButton = CancelButton = null;
        refreshHotkeyFields();
        _hotkeyFields[action].Text = "Press a key…";
    }

    protected override bool ProcessKeyPreview(ref Message m)
    {
        // WinForms KeyDown reports both Ctrl keys as ControlKey. Preserve the
        // native extended-key bit so capture matches the raw-input action key.
        if (_capturingAction != null && m.Msg == 0x0100 &&
            (Keys)m.WParam.ToInt32() is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey)
        {
            var key = (Keys)m.WParam.ToInt32();
            if (key == Keys.ControlKey)
                key = (m.LParam.ToInt64() & (1L << 24)) != 0 ? Keys.RControlKey : Keys.LControlKey;
            captureHotkey(key);
            return true;
        }
        return base.ProcessKeyPreview(ref m);
    }

    private void endHotkeyCapture()
    {
        _capturingAction = null;
        AcceptButton = _okButton; CancelButton = _cancelButton;
        refreshHotkeyFields();
    }

    private void captureHotkey(Keys key)
    {
        if (_capturingAction is not { } action) return;
        if (key == Keys.Escape) { endHotkeyCapture(); return; }
        if (!BoardHotkeys.IsBindable(key)) return;
        if (_boardHotkeys.Conflict(key, action) is { } conflict)
        {
            _hotkeyFeedback.Text = key + " is already assigned to " + BoardHotkeys.Actions.First(p => p.Action == conflict).Label + ". Clear that binding first.";
            return;
        }
        _boardHotkeys.Bind(action, key);
        endHotkeyCapture();
        _hotkeyFeedback.Text = "Binding updated. Save settings to apply.";
    }

    private void refreshHotkeyFields()
    {
        foreach (var pair in _hotkeyFields) pair.Value.Text = _boardHotkeys.BindingText(pair.Key);
    }
}
