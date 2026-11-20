using System.Configuration;

namespace EldenBingo.Properties;

internal sealed partial class Settings
{
    [UserScopedSetting, DefaultSettingValue("[]")]
    public string MatchPresetsJson { get => (string)this[nameof(MatchPresetsJson)]; set => this[nameof(MatchPresetsJson)] = value; }
    [UserScopedSetting, DefaultSettingValue("{}")]
    public string EventSoundsJson { get => (string)this[nameof(EventSoundsJson)]; set => this[nameof(EventSoundsJson)] = value; }
    [UserScopedSetting, DefaultSettingValue("0")]
    public int FocusLayout { get => (int)this[nameof(FocusLayout)]; set => this[nameof(FocusLayout)] = value; }
}
