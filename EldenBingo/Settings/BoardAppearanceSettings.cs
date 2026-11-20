using EldenBingoCommon;

namespace EldenBingo.Settings
{
    internal static class BoardAppearanceSettings
    {
        private static string? _lastJson;
        private static BoardAppearance _current = new();
        public static BoardAppearance Current
        {
            get
            {
                var json = Properties.Settings.Default.BoardAppearanceJson;
                if (json != _lastJson)
                {
                    _current = BoardAppearance.Parse(json);
                    _lastJson = json;
                }
                return _current;
            }
        }

        public static Color TeamColor(int team) => Current.ColorForTeam(team);
        public static Color TeamColorBright(int team) => TeamColor(team).Brighten(0.2f);
        public static Color UserColor(UserInRoom user) => user.IsAdmin && user.IsSpectator
            ? BingoConstants.AdminSpectatorColor : TeamColorBright(user.Team);
    }
}
