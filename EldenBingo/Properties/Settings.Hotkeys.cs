using System.Configuration;

namespace EldenBingo.Properties;

internal sealed partial class Settings
{
    [UserScopedSetting, DefaultSettingValue("True")]
    public bool MiddleMouseDimsSquares
    {
        get => (bool)this[nameof(MiddleMouseDimsSquares)];
        set => this[nameof(MiddleMouseDimsSquares)] = value;
    }

    [UserScopedSetting, DefaultSettingValue("False")]
    public bool ScrollKeyboardSelectedSquare
    {
        get => (bool)this[nameof(ScrollKeyboardSelectedSquare)];
        set => this[nameof(ScrollKeyboardSelectedSquare)] = value;
    }

    [UserScopedSetting, DefaultSettingValue("")]
    public string BoardHotkeysJson
    {
        get => (string)this[nameof(BoardHotkeysJson)];
        set => this[nameof(BoardHotkeysJson)] = value;
    }
}
