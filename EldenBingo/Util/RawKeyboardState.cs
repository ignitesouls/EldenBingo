using EldenBingo.Settings;

namespace EldenBingo.Util;

internal sealed class RawKeyboardState
{
    private readonly HashSet<Keys> _held = new();
    public BoardHotkeyModifiers Modifiers =>
        (_held.Contains(Keys.LShiftKey) || _held.Contains(Keys.RShiftKey) ? BoardHotkeyModifiers.Shift : 0) |
        (_held.Contains(Keys.LControlKey) || _held.Contains(Keys.RControlKey) ? BoardHotkeyModifiers.Control : 0) |
        (_held.Contains(Keys.LMenu) ? BoardHotkeyModifiers.LeftAlt : 0);

    public Keys? Process(ushort virtualKey, ushort scan, ushort flags)
    {
        if (virtualKey is 0 or >= 255 || scan == 255) return null;
        var extended = (flags & 2) != 0;
        var key = (Keys)virtualKey switch
        {
            Keys.ShiftKey => scan == 0x36 ? Keys.RShiftKey : Keys.LShiftKey,
            Keys.ControlKey => extended ? Keys.RControlKey : Keys.LControlKey,
            Keys.Menu => extended ? Keys.RMenu : Keys.LMenu,
            _ => (Keys)virtualKey
        };
        if ((flags & 1) != 0) { _held.Remove(key); return null; }
        return _held.Add(key) ? key : null;
    }
}
