using System.Configuration;

namespace EldenBingo.Properties
{
    internal sealed partial class Settings
    {
        [UserScopedSetting, DefaultSettingValue("1")]
        public int LocalJsonSlot
        {
            get => (int)this[nameof(LocalJsonSlot)];
            set => this[nameof(LocalJsonSlot)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("5168")]
        public int LocalJsonPort
        {
            get => (int)this[nameof(LocalJsonPort)];
            set => this[nameof(LocalJsonPort)] = value;
        }


    }
}
