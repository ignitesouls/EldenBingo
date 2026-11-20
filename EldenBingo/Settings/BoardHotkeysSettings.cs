namespace EldenBingo.Settings;

internal static class BoardHotkeysSettings
{
    private static string? _last;
    private static BoardHotkeys _current = new();
    public static BoardHotkeys Current
    {
        get
        {
            var settings = Properties.Settings.Default;
            var json = settings.BoardHotkeysJson;
            if (_last != json)
            {
                _current = BoardHotkeys.Parse(json, BoardHotkeys.FromLegacy(settings.ClickHotkey, settings.NumpadNavigation, settings.ArrowNavigation));
                _last = json;
            }
            return _current;
        }
    }
}
