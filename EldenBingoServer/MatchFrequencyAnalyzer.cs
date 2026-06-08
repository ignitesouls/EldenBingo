using Newtonsoft.Json;

namespace EldenBingoServer
{
    public sealed class MatchFrequencyAnalyzer
    {
        public MatchFrequencyReport AnalyzeDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Match log directory not found: {directory}");

            var stats = new Dictionary<string, MutableSquareFrequency>(StringComparer.OrdinalIgnoreCase);
            int matches = 0;
            int skippedFiles = 0;

            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                MatchLogData? log;
                try
                {
                    log = JsonConvert.DeserializeObject<MatchLogData>(File.ReadAllText(path));
                }
                catch (JsonException)
                {
                    skippedFiles++;
                    continue;
                }
                catch (IOException)
                {
                    skippedFiles++;
                    continue;
                }

                if (log?.Squares == null || log.Squares.Length == 0)
                {
                    skippedFiles++;
                    continue;
                }

                matches++;
                var usedIndexes = new HashSet<int>(
                    (log.Events ?? Array.Empty<MatchEventData>())
                        .Where(e => e.Checked && e.SquareIndex >= 0 && e.SquareIndex < log.Squares.Length)
                        .Select(e => e.SquareIndex));
                var appearedThisMatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var usedThisMatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < log.Squares.Length; i++)
                {
                    string square = log.Squares[i]?.Trim() ?? string.Empty;
                    if (square.Length == 0)
                        continue;

                    if (!stats.TryGetValue(square, out var frequency))
                    {
                        frequency = new MutableSquareFrequency(square);
                        stats.Add(square, frequency);
                    }

                    frequency.Appearances++;
                    appearedThisMatch.Add(square);

                    if (usedIndexes.Contains(i))
                    {
                        frequency.Uses++;
                        usedThisMatch.Add(square);
                    }
                }

                foreach (string square in appearedThisMatch)
                    stats[square].MatchesAppeared++;
                foreach (string square in usedThisMatch)
                    stats[square].MatchesUsed++;
            }

            var squares = stats.Values
                .Select(s => new SquareFrequency(
                    s.Square,
                    s.Appearances,
                    s.Uses,
                    s.MatchesAppeared,
                    s.MatchesUsed,
                    matches == 0 ? 0 : (double)s.MatchesAppeared / matches,
                    s.Appearances == 0 ? 0 : (double)s.Uses / s.Appearances))
                .OrderByDescending(s => s.AppearanceProbability)
                .ThenByDescending(s => s.UseRate)
                .ThenBy(s => s.Square, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new MatchFrequencyReport(matches, skippedFiles, squares);
        }

        private sealed class MutableSquareFrequency
        {
            public MutableSquareFrequency(string square)
            {
                Square = square;
            }

            public string Square { get; }
            public int Appearances { get; set; }
            public int Uses { get; set; }
            public int MatchesAppeared { get; set; }
            public int MatchesUsed { get; set; }
        }

        private sealed class MatchLogData
        {
            public string[]? Squares { get; set; }
            public MatchEventData[]? Events { get; set; }
        }

        private sealed class MatchEventData
        {
            public int SquareIndex { get; set; }
            public bool Checked { get; set; }
        }
    }

    public sealed record MatchFrequencyReport(
        int MatchesAnalyzed,
        int SkippedFiles,
        IReadOnlyList<SquareFrequency> Squares);

    public sealed record SquareFrequency(
        string Square,
        int Appearances,
        int Uses,
        int MatchesAppeared,
        int MatchesUsed,
        double AppearanceProbability,
        double UseRate);
}
