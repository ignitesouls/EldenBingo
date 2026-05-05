using EldenBingoCommon;
using Newtonsoft.Json;

namespace EldenBingoServer
{
    /// <summary>
    /// Server-side battleship state for one team.
    /// Tracks ship placements and received attacks.
    /// </summary>
    public class ServerBattleshipTeamBoard
    {
        private readonly int _boardSize;
        private readonly ShipDefinition[] _shipDefs;

        [JsonProperty]
        public int Team { get; init; }

        /// <summary>
        /// True for cells that contain a ship segment.
        /// </summary>
        [JsonProperty]
        public bool[] ShipGrid { get; init; }

        /// <summary>
        /// Stores the team id that attacked this cell, or -1 if not attacked.
        /// </summary>
        [JsonProperty]
        public int[] AttackedGrid { get; init; }

        /// <summary>
        /// The ship placements for this team.
        /// </summary>
        [JsonProperty]
        public ShipPlacement[]? Placements { get; set; }

        /// <summary>
        /// Whether this team has confirmed their placement.
        /// </summary>
        [JsonProperty]
        public bool PlacementConfirmed { get; set; }

        /// <summary>
        /// Tracks which ship index each cell belongs to (-1 = no ship).
        /// </summary>
        [JsonProperty]
        public int[] ShipIndexGrid { get; init; }

        /// <summary>
        /// Hits remaining per ship. When it reaches 0 the ship is sunk.
        /// </summary>
        [JsonProperty]
        public int[] ShipHitsRemaining { get; init; }

        /// <summary>
        /// Whether each ship has been sunk.
        /// </summary>
        [JsonProperty]
        public bool[] ShipSunk { get; init; }

        public ServerBattleshipTeamBoard(int team, int boardSize, ShipDefinition[] shipDefs)
        {
            Team = team;
            _boardSize = boardSize;
            _shipDefs = shipDefs;

            var totalCells = boardSize * boardSize;
            ShipGrid = new bool[totalCells];
            AttackedGrid = new int[totalCells];
            ShipIndexGrid = new int[totalCells];
            ShipHitsRemaining = new int[shipDefs.Length];
            ShipSunk = new bool[shipDefs.Length];

            for (int i = 0; i < totalCells; i++)
            {
                ShipIndexGrid[i] = -1;
                AttackedGrid[i] = -1;
            }

            for (int i = 0; i < shipDefs.Length; i++)
                ShipHitsRemaining[i] = shipDefs[i].Size;
        }

        /// <summary>
        /// Validate and apply ship placements from a player.
        /// Returns true if placements are valid.
        /// </summary>
        public bool TryPlaceShips(ShipPlacement[] placements)
        {
            if (placements.Length != _shipDefs.Length)
                return false;

            // Reset
            var totalCells = _boardSize * _boardSize;
            var tempGrid = new bool[totalCells];
            var tempShipIndex = new int[totalCells];
            for (int i = 0; i < totalCells; i++)
                tempShipIndex[i] = -1;

            for (int s = 0; s < placements.Length; s++)
            {
                var p = placements[s];
                if (p.ShipIndex != s)
                    return false; // Must be in order

                var shipSize = _shipDefs[s].Size;
                int dr = p.IsHorizontal ? 0 : 1;
                int dc = p.IsHorizontal ? 1 : 0;

                for (int i = 0; i < shipSize; i++)
                {
                    int r = p.StartRow + dr * i;
                    int c = p.StartCol + dc * i;

                    if (r < 0 || r >= _boardSize || c < 0 || c >= _boardSize)
                        return false; // Out of bounds

                    int idx = r * _boardSize + c;
                    if (tempGrid[idx])
                        return false; // Overlap

                    tempGrid[idx] = true;
                    tempShipIndex[idx] = s;
                }
            }

            // Apply
            Array.Copy(tempGrid, ShipGrid, totalCells);
            Array.Copy(tempShipIndex, ShipIndexGrid, totalCells);
            Placements = placements;

            for (int i = 0; i < _shipDefs.Length; i++)
                ShipHitsRemaining[i] = _shipDefs[i].Size;

            return true;
        }

        /// <summary>
        /// Process an attack on this team's grid at the given cell index.
        /// Returns the result and optionally the sunk ship info.
        /// </summary>
        public (AttackResult result, ShipSunkInfo? sunkShip) ReceiveAttack(int cellIndex, int attackerTeam)
        {
            if (cellIndex < 0 || cellIndex >= _boardSize * _boardSize)
                return (AttackResult.Miss, null);

            if (AttackedGrid[cellIndex] != -1)
                return (AttackResult.Miss, null); // Already attacked

            AttackedGrid[cellIndex] = attackerTeam;

            if (!ShipGrid[cellIndex])
                return (AttackResult.Miss, null);

            // It's a hit
            int shipIdx = ShipIndexGrid[cellIndex];
            ShipHitsRemaining[shipIdx]--;

            if (ShipHitsRemaining[shipIdx] <= 0)
            {
                ShipSunk[shipIdx] = true;
                var placement = Placements![shipIdx];
                var sunkInfo = new ShipSunkInfo(
                    _shipDefs[shipIdx].Name,
                    _shipDefs[shipIdx].Size,
                    placement.StartRow,
                    placement.StartCol,
                    placement.IsHorizontal
                );
                return (AttackResult.Sunk, sunkInfo);
            }

            return (AttackResult.Hit, null);
        }

        /// <summary>
        /// Whether all ships have been sunk.
        /// </summary>
        public bool AllShipsSunk()
        {
            return ShipSunk.All(s => s);
        }

        /// <summary>
        /// Get the list of already-sunk ships for display.
        /// </summary>
        public List<ShipSunkInfo> GetSunkShips()
        {
            var result = new List<ShipSunkInfo>();
            for (int i = 0; i < _shipDefs.Length; i++)
            {
                if (ShipSunk[i] && Placements != null)
                {
                    var p = Placements[i];
                    result.Add(new ShipSunkInfo(_shipDefs[i].Name, _shipDefs[i].Size, p.StartRow, p.StartCol, p.IsHorizontal));
                }
            }
            return result;
        }

        /// <summary>
        /// Build the view for a specific viewer team.
        /// </summary>
        public BattleshipTeamView GetViewForTeam(int viewerTeam)
        {
            var totalCells = _boardSize * _boardSize;
            var defenseGrid = new CellState[totalCells];
            var attackGrid = new CellState[totalCells]; // Not used here — filled by caller
            var shipCells = new bool[totalCells];
            bool isOwnTeam = viewerTeam == Team;

            for (int i = 0; i < totalCells; i++)
            {
                if (AttackedGrid[i] != -1)
                {
                    defenseGrid[i] = ShipGrid[i] ? CellState.Hit : CellState.Miss;
                }
                else
                {
                    defenseGrid[i] = CellState.Unknown;
                }

                // Only show ship cells to own team
                shipCells[i] = isOwnTeam && ShipGrid[i];
            }

            // Build per-cell attacker array
            var attackedBy = new int[totalCells];
            for (int i = 0; i < totalCells; i++)
                attackedBy[i] = AttackedGrid[i];

            return new BattleshipTeamView(
                Team,
                defenseGrid,
                attackGrid,
                shipCells,
                PlacementConfirmed,
                GetSunkShips().ToArray(),
                attackedBy
            );
        }
    }

    /// <summary>
    /// Manages the full battleship game state for a room: two team boards.
    /// </summary>
    public class ServerBattleshipGame
    {
        [JsonProperty]
        public int BoardSize { get; init; }

        [JsonProperty]
        public ShipDefinition[] ShipDefs { get; init; }

        [JsonProperty]
        public Dictionary<int, ServerBattleshipTeamBoard> TeamBoards { get; init; }

        public ServerBattleshipGame(int boardSize, ShipDefinition[] shipDefs, IEnumerable<int> teams)
        {
            BoardSize = boardSize;
            ShipDefs = shipDefs;
            TeamBoards = new Dictionary<int, ServerBattleshipTeamBoard>();
            foreach (var t in teams)
            {
                TeamBoards[t] = new ServerBattleshipTeamBoard(t, boardSize, shipDefs);
            }
        }

        /// <summary>
        /// Get the opponent team index.
        /// </summary>
        public int GetOpponentTeam(int team)
        {
            // Backwards-compatible helper: return any other team if exists
            foreach (var t in TeamBoards.Keys)
            {
                if (t != team)
                    return t;
            }
            return -1;
        }

        /// <summary>
        /// Returns true if both teams have confirmed their ship placements.
        /// </summary>
        public bool AllPlacementsConfirmed()
        {
            return TeamBoards.Values.All(b => b.PlacementConfirmed);
        }

        /// <summary>
        /// Process an attack: the attacking team completes a challenge at cellIndex,
        /// which attacks that cell on the opponent's grid.
        /// </summary>
        public (AttackResult result, ShipSunkInfo? sunkShip)? ProcessAttack(int attackingTeam, int targetTeam, int cellIndex)
        {
            if (!TeamBoards.ContainsKey(targetTeam))
                return null;

            return TeamBoards[targetTeam].ReceiveAttack(cellIndex, attackingTeam);
        }

        /// <summary>
        /// Get the complete battleship view for a specific viewer.
        /// Returns the view of both their own grid (defense) and opponent grid (attack).
        /// </summary>
        public BattleshipTeamView? GetViewForUser(int userTeam)
        {
            if (!TeamBoards.ContainsKey(userTeam))
                return null;

            var ownBoard = TeamBoards[userTeam];

            // Defense grid & ship cells (masked appropriately by GetViewForTeam)
            var ownView = ownBoard.GetViewForTeam(userTeam);

            // Attack grid: aggregate what this team has attacked across all opponent boards
            var totalCells = BoardSize * BoardSize;
            var attackGrid = new CellState[totalCells];
            for (int i = 0; i < totalCells; i++)
                attackGrid[i] = CellState.Unknown;

            foreach (var kv in TeamBoards)
            {
                var b = kv.Value;
                if (b.Team == userTeam)
                    continue;
                for (int i = 0; i < totalCells; i++)
                {
                    if (b.AttackedGrid[i] == userTeam)
                    {
                        var newState = b.ShipGrid[i] ? CellState.Hit : CellState.Miss;
                        // Prefer Hit over Miss. Don't let a later Miss overwrite a previous Hit.
                        if (attackGrid[i] != CellState.Hit)
                            attackGrid[i] = newState;
                    }
                }
            }

            // Combine sunk ships across opponents for visibility
            var sunkShips = TeamBoards.Values.SelectMany(b => b.GetSunkShips()).ToArray();
            ownView = ownView with { AttackGrid = attackGrid, SunkShips = sunkShips };

            return ownView;
        }

        /// <summary>
        /// Check if the game is over (one team's ships all sunk).
        /// Returns the winning team index, or -1 if game continues.
        /// </summary>
        public int CheckGameOver()
        {
            var alive = TeamBoards.Where(kv => !kv.Value.AllShipsSunk()).ToList();
            if (alive.Count == 1)
                return alive[0].Key;
            return -1;
        }
    }
}
