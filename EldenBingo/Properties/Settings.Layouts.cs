using System.Configuration;

namespace EldenBingo.Properties
{
    internal sealed partial class Settings
    {
        [UserScopedSetting, DefaultSettingValue("layouts")]
        public string LastSettingsCategory
        {
            get => (string)this[nameof(LastSettingsCategory)];
            set => this[nameof(LastSettingsCategory)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("{}")]
        public string BoardAppearanceJson
        {
            get => (string)this[nameof(BoardAppearanceJson)];
            set => this[nameof(BoardAppearanceJson)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("[]")]
        public string SavedColorProfilesJson
        {
            get => (string)this[nameof(SavedColorProfilesJson)];
            set => this[nameof(SavedColorProfilesJson)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("[]")]
        public string SavedKeywordProfilesJson
        {
            get => (string)this[nameof(SavedKeywordProfilesJson)];
            set => this[nameof(SavedKeywordProfilesJson)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("[]")]
        public string SavedLayoutsJson
        {
            get => (string)this[nameof(SavedLayoutsJson)];
            set => this[nameof(SavedLayoutsJson)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("200")]
        public int LobbySidebarWidth
        {
            get => (int)this[nameof(LobbySidebarWidth)];
            set => this[nameof(LobbySidebarWidth)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("")]
        public string PopoutLayoutJson
        {
            get => (string)this[nameof(PopoutLayoutJson)];
            set => this[nameof(PopoutLayoutJson)] = value;
        }
    }
}
