using EldenBingoCommon;

namespace EldenBingo.Net
{
    public class BattleshipConfigEventArgs : EventArgs
    {
        public ShipDefinition[] Ships { get; }
        public int BoardSize { get; }

        public BattleshipConfigEventArgs(ShipDefinition[] ships, int boardSize)
        {
            Ships = ships;
            BoardSize = boardSize;
        }
    }

    public class BattleshipTeamViewEventArgs : EventArgs
    {
        public BattleshipTeamView TeamView { get; }

        public BattleshipTeamViewEventArgs(BattleshipTeamView teamView)
        {
            TeamView = teamView;
        }
    }

    public class AttackResultEventArgs : EventArgs
    {
        public int Index { get; }
        public AttackResult Result { get; }
        public int AttackingTeam { get; }
        public int DefendingTeam { get; }
        public ShipSunkInfo? SunkShip { get; }

        public AttackResultEventArgs(int index, AttackResult result, int attackingTeam, int defendingTeam, ShipSunkInfo? sunkShip)
        {
            Index = index;
            Result = result;
            AttackingTeam = attackingTeam;
            DefendingTeam = defendingTeam;
            SunkShip = sunkShip;
        }
    }

    public class BattleshipGameOverEventArgs : EventArgs
    {
        public int WinningTeam { get; }
        public string WinningTeamName { get; }

        public BattleshipGameOverEventArgs(int winningTeam, string winningTeamName)
        {
            WinningTeam = winningTeam;
            WinningTeamName = winningTeamName;
        }
    }
}
