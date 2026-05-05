namespace EldenBingoCommon
{
    /// <summary>
    /// Defines a ship type with a name and size (number of cells it occupies).
    /// </summary>
    public record struct ShipDefinition(string Name, int Size);

    /// <summary>
    /// Represents a placed ship on the grid: starting position and orientation.
    /// </summary>
    public record struct ShipPlacement(int ShipIndex, int StartRow, int StartCol, bool IsHorizontal);

    /// <summary>
    /// Result of attacking a cell on the opponent's grid.
    /// </summary>
    public enum AttackResult
    {
        Miss,
        Hit,
        Sunk
    }

    /// <summary>
    /// State of a single cell on a battleship grid.
    /// </summary>
    public enum CellState
    {
        Unknown,
        Miss,
        Hit
    }

    /// <summary>
    /// Holds the battleship state for one team: their ship placements and the attacks received.
    /// Sent to clients with visibility rules applied.
    /// </summary>
    public record struct BattleshipTeamView(
        int Team,
        CellState[] DefenseGrid,      // What attacks have been received on this team's grid (visible to this team)
        CellState[] AttackGrid,       // What this team has attacked on opponent's grid
        bool[] ShipCells,             // Which cells have ships (only visible to own team)
        bool PlacementConfirmed,
        ShipSunkInfo[] SunkShips,     // Ships that have been sunk (visible to both teams)
        int[] AttackedBy               // Per-cell attacker team id (-1 = not attacked)
    );

    /// <summary>
    /// Information about a sunk ship, visible to both teams.
    /// </summary>
    public record struct ShipSunkInfo(string ShipName, int Size, int StartRow, int StartCol, bool IsHorizontal);

    public static class BattleshipConstants
    {
        public static readonly ShipDefinition[] ClassicShips = new[]
        {
            new ShipDefinition("Carrier", 5),
            new ShipDefinition("Battleship", 4),
            new ShipDefinition("Cruiser", 3),
            new ShipDefinition("Submarine", 3),
            new ShipDefinition("Destroyer", 2),
        };
    }
}
