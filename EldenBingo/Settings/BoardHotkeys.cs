using System.Text.Json;

namespace EldenBingo.Settings;

// TogglePin is retained only to retire older saved bindings without renumbering actions.
internal enum BoardAction { Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight, MarkSquare, StarSquare, IncrementCount, DecrementCount, ToggleFocus, TogglePin, SearchBoard, DimSquare }
[Flags]
internal enum BoardHotkeyModifiers { None = 0, Shift = 1, Control = 2, LeftAlt = 4 }

internal sealed class BoardHotkeys
{
    public Dictionary<BoardAction, List<int>> Bindings { get; set; } = new();
    public int Version { get; set; } = 1;
    public BoardHotkeyModifiers RequiredModifiers { get; set; }
    public static readonly (BoardAction Action, string Label)[] Actions =
    {
        (BoardAction.Up, "↑ Up"), (BoardAction.Down, "↓ Down"), (BoardAction.Left, "← Left"), (BoardAction.Right, "→ Right"),
        (BoardAction.UpLeft, "↖ Up left"), (BoardAction.UpRight, "↗ Up right"), (BoardAction.DownLeft, "↙ Down left"), (BoardAction.DownRight, "↘ Down right"),
        (BoardAction.MarkSquare, "✓ Mark / unmark square"), (BoardAction.StarSquare, "★ Star / unstar square"),
        (BoardAction.IncrementCount, "+ Increment count"), (BoardAction.DecrementCount, "− Decrement count"),
        (BoardAction.ToggleFocus, "Toggle focus mode"), (BoardAction.SearchBoard, "Search board"), (BoardAction.DimSquare, "Dim / restore square")
    };

    public static bool IsBindable(Keys key) => (int)key is >= 8 and < 255 && key is not (Keys.Escape or Keys.ShiftKey or Keys.ControlKey or Keys.Menu or
        Keys.LShiftKey or Keys.RShiftKey or Keys.LMenu or Keys.RMenu or Keys.LWin or Keys.RWin);

    public bool IsValid => Version is >= 1 and <= 3 && Bindings != null && ((int)RequiredModifiers & ~7) == 0 && Bindings.All(p => Enum.IsDefined(typeof(BoardAction), p.Key) &&
        p.Value != null && p.Value.Count <= 3 && p.Value.All(k => IsBindable((Keys)k)) && p.Value.Distinct().Count() == p.Value.Count) &&
        Bindings.SelectMany(p => p.Value).Distinct().Count() == Bindings.Sum(p => p.Value.Count);

    public BoardHotkeys Clone() => new() { Version = Version, RequiredModifiers = RequiredModifiers, Bindings = Bindings.ToDictionary(p => p.Key, p => p.Value.ToList()) };
    public string BindingText(BoardAction action) => Bindings.TryGetValue(action, out var keys) && keys.Count > 0 ? string.Join(" / ", keys.Select(k => (Keys)k switch
    {
        Keys.LControlKey => "Left Ctrl", Keys.RControlKey => "Right Ctrl", _ => ((Keys)k).ToString()
    })) : "NONE";
    public BoardAction? Resolve(Keys key, BoardHotkeyModifiers held)
    {
        if ((held & RequiredModifiers) != RequiredModifiers) return null;
        foreach (var pair in Bindings) if (pair.Key != BoardAction.TogglePin && pair.Value.Contains((int)key)) return pair.Key;
        return null;
    }
    public BoardAction? Conflict(Keys key, BoardAction except)
    {
        foreach (var pair in Bindings) if (pair.Key != except && pair.Value.Contains((int)key)) return pair.Key;
        return null;
    }
    public void Bind(BoardAction action, Keys key) => Bindings[action] = key == Keys.None ? new() : new() { (int)key };

    public static BoardHotkeys FromLegacy(int markKey, bool numpad, bool arrows)
    {
        if (markKey == 0 && !numpad && !arrows) return DefaultBindings();
        var result = new BoardHotkeys();
        void add(BoardAction action, Keys key)
        {
            if (!IsBindable(key)) return;
            if (!result.Bindings.TryGetValue(action, out var keys)) result.Bindings[action] = keys = new();
            if (!keys.Contains((int)key)) keys.Add((int)key);
        }
        if (arrows)
        {
            add(BoardAction.Up, Keys.Up); add(BoardAction.Down, Keys.Down);
            add(BoardAction.Left, Keys.Left); add(BoardAction.Right, Keys.Right);
        }
        if (numpad)
        {
            add(BoardAction.Up, Keys.NumPad8); add(BoardAction.Down, Keys.NumPad2); add(BoardAction.Left, Keys.NumPad4); add(BoardAction.Right, Keys.NumPad6);
            add(BoardAction.UpLeft, Keys.NumPad7); add(BoardAction.UpRight, Keys.NumPad9); add(BoardAction.DownLeft, Keys.NumPad1); add(BoardAction.DownRight, Keys.NumPad3);
            add(BoardAction.StarSquare, Keys.Multiply); add(BoardAction.IncrementCount, Keys.Add); add(BoardAction.DecrementCount, Keys.Subtract);
        }
        if (IsBindable((Keys)markKey))
        {
            foreach (var keys in result.Bindings.Values) keys.Remove(markKey);
            result.Bind(BoardAction.MarkSquare, (Keys)markKey);
        }
        result.AddAppDefaults();
        return result;
    }

    public static BoardHotkeys DefaultBindings() => FromLegacy((int)Keys.Space, true, true);

    public static BoardHotkeys Parse(string json, BoardHotkeys fallback)
    {
        if (!string.IsNullOrWhiteSpace(json) && json.Length <= 16384)
        {
            try
            {
                var value = JsonSerializer.Deserialize<BoardHotkeys>(json);
                if (value?.IsValid == true)
                {
                    value.Bindings.Remove(BoardAction.TogglePin);
                    if (value.Version < 3)
                    {
                        // Empty saved maps are deliberate. Never re-enable cleared keys.
                        if (value.Bindings.Values.Any(keys => keys.Count > 0))
                        {
                            if (value.Version < 2) value.AddAppDefaults();
                            else value.AddDimDefault();
                        }
                        value.Version = 3;
                    }
                    return value;
                }
            }
            catch (JsonException) { }
        }
        return fallback.Clone();
    }

    private void AddAppDefaults()
    {
        foreach (var (action, key) in new[] { (BoardAction.ToggleFocus, Keys.F10), (BoardAction.SearchBoard, Keys.F3) })
            if (!Bindings.ContainsKey(action) && !Bindings.Values.Any(keys => keys.Contains((int)key))) Bind(action, key);
        AddDimDefault();
        Version = 3;
    }

    private void AddDimDefault()
    {
        if (!Bindings.ContainsKey(BoardAction.DimSquare) && !Bindings.Values.Any(keys => keys.Contains((int)Keys.F4)))
            Bind(BoardAction.DimSquare, Keys.F4);
    }

    public static bool IsMovement(BoardAction action) => action is >= BoardAction.Up and <= BoardAction.DownRight;
    public static int Move(int index, int size, BoardAction action)
    {
        if (size <= 0) return -1;
        index = Math.Clamp(index, 0, size * size - 1);
        var dx = action is BoardAction.Left or BoardAction.UpLeft or BoardAction.DownLeft ? -1 : action is BoardAction.Right or BoardAction.UpRight or BoardAction.DownRight ? 1 : 0;
        var dy = action is BoardAction.Up or BoardAction.UpLeft or BoardAction.UpRight ? -1 : action is BoardAction.Down or BoardAction.DownLeft or BoardAction.DownRight ? 1 : 0;
        return Math.Clamp(index / size + dy, 0, size - 1) * size + Math.Clamp(index % size + dx, 0, size - 1);
    }
}
