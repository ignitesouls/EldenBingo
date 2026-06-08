using EldenBingoCommon;
using Newtonsoft.Json.Linq;

namespace EldenBingoServer
{
    public sealed class BoardFrequencySimulator
    {
        public BoardSimulationReport Simulate(
            string jsonPath,
            int requestedBoards = 1000,
            int boardSize = 5,
            int categoryLimit = 0,
            int randomSeed = 0)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("Square JSON file not found.", jsonPath);
            if (requestedBoards <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedBoards), "Board count must be greater than zero.");
            if (boardSize < BingoConstants.BoardSizeMin || boardSize > BingoConstants.BoardSizeMax)
                throw new ArgumentOutOfRangeException(
                    nameof(boardSize),
                    $"Board size must be between {BingoConstants.BoardSizeMin} and {BingoConstants.BoardSizeMax}.");

            JObject root = ParseRoot(File.ReadAllText(jsonPath));
            var settings = new BingoGameSettings(
                boardSize,
                false,
                false,
                new HashSet<EldenRingClasses>(),
                0,
                Math.Max(0, categoryLimit),
                randomSeed,
                0,
                0);
            var room = new ServerRoom("Simulation", string.Empty, null!, settings)
            {
                CategoryConfig = ParseCategoryConfig(root)
            };
            var generator = new BingoBoardGenerator(root, randomSeed);
            var frequencies = new Dictionary<string, MutableBoardFrequency>(StringComparer.OrdinalIgnoreCase);
            int generatedBoards = 0;

            for (int i = 0; i < requestedBoards; i++)
            {
                var board = generator.CreateBingoBoard(room);
                if (board == null)
                    continue;

                generatedBoards++;
                var seenOnBoard = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var square in board.Squares)
                {
                    string text = square.Text.Trim();
                    if (text.Length == 0)
                        continue;

                    if (!frequencies.TryGetValue(text, out var frequency))
                    {
                        frequency = new MutableBoardFrequency(text);
                        frequencies.Add(text, frequency);
                    }

                    frequency.Appearances++;
                    seenOnBoard.Add(text);
                }

                foreach (string text in seenOnBoard)
                    frequencies[text].BoardsContaining++;
            }

            var squares = frequencies.Values
                .Select(f => new SimulatedSquareFrequency(
                    f.Square,
                    f.BoardsContaining,
                    f.Appearances,
                    generatedBoards == 0 ? 0 : (double)f.BoardsContaining / generatedBoards,
                    generatedBoards == 0 ? 0 : (double)f.Appearances / generatedBoards))
                .OrderByDescending(f => f.AppearanceProbability)
                .ThenByDescending(f => f.AveragePerBoard)
                .ThenBy(f => f.Square, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new BoardSimulationReport(
                requestedBoards,
                generatedBoards,
                requestedBoards - generatedBoards,
                boardSize,
                randomSeed,
                squares);
        }

        private static JObject ParseRoot(string json)
        {
            var token = JToken.Parse(json);
            if (token is JArray array)
                return new JObject { ["squares"] = array };
            if (token is JObject root && root["squares"] is JArray)
                return root;
            throw new Exception("Square JSON must be an array or an object containing a 'squares' array.");
        }

        private static CategoryConfig ParseCategoryConfig(JObject root)
        {
            var config = CategoryConfig.FromJson(root);

            if (root.TryGetValue("category limits", StringComparison.OrdinalIgnoreCase, out var limits)
                && limits is JObject limitsObject)
            {
                foreach (var item in limitsObject)
                {
                    if (item.Value?.Type == JTokenType.Integer)
                        config.SetCategory(item.Key, item.Value.Value<int>());
                }
            }

            if (root.TryGetValue("category minimums", StringComparison.OrdinalIgnoreCase, out var minimums)
                && minimums is JObject minimumsObject)
            {
                config.ParseMinimums(minimumsObject);
            }

            return config;
        }

        private sealed class MutableBoardFrequency
        {
            public MutableBoardFrequency(string square)
            {
                Square = square;
            }

            public string Square { get; }
            public int BoardsContaining { get; set; }
            public int Appearances { get; set; }
        }
    }

    public sealed record BoardSimulationReport(
        int RequestedBoards,
        int GeneratedBoards,
        int FailedBoards,
        int BoardSize,
        int RandomSeed,
        IReadOnlyList<SimulatedSquareFrequency> Squares);

    public sealed record SimulatedSquareFrequency(
        string Square,
        int BoardsContaining,
        int Appearances,
        double AppearanceProbability,
        double AveragePerBoard);
}
