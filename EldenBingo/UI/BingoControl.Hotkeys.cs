using EldenBingo.Settings;

namespace EldenBingo.UI;

internal partial class BingoControl
{
    private bool canUseBoardHotkeys()
    {
        // Raw input also arrives while dialogs or text editors are active.
        if (Application.OpenForms.Cast<Form>().Any(f => f.Visible && f.Modal)) return false;
        if (FindForm() is { Visible: false }) return false;
        var active = Form.ActiveForm;
        if (active == null) return true; // Preserve out-of-focus controls while playing the game.
        var main = MainForm.GetMainForm(this) ?? MainForm.Instance;
        if (active != main && active != FindForm()) return false;
        Control focused = active;
        while (true)
        {
            var child = focused.Controls.Cast<Control>().FirstOrDefault(c => c.ContainsFocus);
            if (child == null) break;
            focused = child;
        }
        return focused is not (TextBoxBase or ListControl or UpDownBase or DataGridView);
    }

    private async void keyPressed(object? sender, KeyEventArgs e)
    {
        if (!canUseBoardHotkeys()) return;
        var main = MainForm.GetMainForm(this) ?? MainForm.Instance;
        var held = main?.RawInput.HeldModifiers ?? BoardHotkeyModifiers.None;
        var action = BoardHotkeysSettings.Current.Resolve(e.KeyCode, held);
        if (action == null) return;
        e.Handled = e.SuppressKeyPress = true;
        switch (action)
        {
            case BoardAction.ToggleFocus: LobbyControl.ToggleFocusFor(Client); return;
            case BoardAction.SearchBoard: OpenBoardSearch(); return;
        }
        if (Squares == null || Squares.Length == 0) return;
        if (BoardHotkeys.IsMovement(action.Value))
        {
            var index = _selectedSquareIndex < 0 ? Math.Clamp(_lastNavigationSelection, 0, Squares.Length - 1) :
                BoardHotkeys.Move(_selectedSquareIndex, _size, action.Value);
            _lastNavigationSelection = index;
            selectKeyboardSquare(index);
            return;
        }
        var square = getSelectedSquare();
        if (square == null) return;
        if (action == BoardAction.DimSquare)
        {
            if (CanSearchBoard) Client?.ToggleLocalDim(square.Index);
            return;
        }
        switch (action)
        {
            case BoardAction.MarkSquare: await clickSquare(square); break;
            case BoardAction.StarSquare: await markSquare(square); break;
            case BoardAction.IncrementCount: await changeSquareCounter(square, 1); break;
            case BoardAction.DecrementCount: await changeSquareCounter(square, -1); break;
        }
    }
}
