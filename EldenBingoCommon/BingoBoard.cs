using Newtonsoft.Json;

namespace EldenBingoCommon
{
    public class BingoBoard
    {
        public BingoBoard(int size, bool lockout, BingoBoardSquare[] squares, EldenRingClasses[] availableClasses)
        {
            Size = size;
            Lockout = lockout;
            if (squares.Length != size * size)
                throw new ArgumentException($"Needs exactly {size * size} squares");
            Squares = squares;
            AvailableClasses = availableClasses;
            
        }

        public int Size { get; init; }
        public int SquareCount => Size * Size;
        public BingoBoardSquare[] Squares { get; init; }
        public EldenRingClasses[] AvailableClasses { get; init; }
        public bool Lockout { get; init; }
        
    }

    public record struct BingoBoardSquare(string Text, string Tooltip, int[] Team, bool Marked, SquareCounter[] Counters, int PointValue = 1, bool CanChooseTimedResume = false)
    {
        [JsonProperty]
        public string Text { get; set; } = Text;
        [JsonProperty]
        public string Tooltip { get; set; } = Tooltip;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [System.ComponentModel.DefaultValue(1)]
        public int PointValue { get; set; } = PointValue;
        [JsonIgnore]
        public bool CanChooseTimedResume { get; set; } = CanChooseTimedResume;
        [JsonIgnore]
        public int[] Team { get; set; } = Team;
        [JsonIgnore]
        public bool Marked { get; set; } = Marked;
        [JsonIgnore]
        public SquareCounter[] Counters { get; set; } = Counters;
        
        public bool IsChecked(int team)
        {
            return Team.Contains(team);
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
