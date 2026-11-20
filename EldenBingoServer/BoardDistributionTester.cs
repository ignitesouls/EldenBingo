using EldenBingoCommon;
using Newtonsoft.Json.Linq;

namespace EldenBingoServer;

public sealed record ObjectiveDistribution(int Index, string Objective, decimal Weight, int Points, string Center, string Categories, string Regions, int Selected, int CenterSelected, double BoardPercent, string Template = "", string Timing = "");
public sealed record GroupDistribution(string Group, int Occurrences, int Boards, double AveragePerBoard, double BoardPercent);
public sealed record GenerationFailureDistribution(string Reason, string Detail, int Attempts, double AttemptPercent);
public sealed record ObjectiveSelectionDistribution(int Index, string Objective, int Selected, int SelectedOnFailedBoards, int CategoryRejected,
    int RegionRejected, int ForcedCenterExcluded, int CenterRejected, int NotReached, int PassedOver);
public sealed record BoardDistributionReport(int Attempts, int Successful, int Failed, ObjectiveDistribution[] Objectives,
    GroupDistribution[] Categories, GroupDistribution[] Regions, ServerBingoBoard? Sample, double AveragePoints)
{
    public ObjectiveDistribution[] ResolvedObjectives { get; init; } = Array.Empty<ObjectiveDistribution>();
    public GenerationFailureDistribution[] GenerationFailures { get; init; } = Array.Empty<GenerationFailureDistribution>();
    public ObjectiveSelectionDistribution[] SelectionDiagnostics { get; init; } = Array.Empty<ObjectiveSelectionDistribution>();
    // One-based source indexes retain exact category membership, including tags
    // containing commas and objectives with multiple categories.
    public Dictionary<int, string[]> ObjectiveCategories { get; init; } = new();
}

public static class BoardDistributionTester
{
    public static BoardDistributionReport Run(string json, BingoGameSettings settings, int attempts,
        CancellationToken cancellation = default, IProgress<int>? progress = null)
    {
        if (attempts is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(attempts));
        if (settings.BoardSize < BingoConstants.BoardSizeMin || settings.BoardSize > BingoConstants.BoardSizeMax)
            throw new ArgumentException("Invalid board size.");
        var definition = SquareSetDefinition.Parse(json);
        var generator = new BingoBoardGenerator(definition.Root, settings.RandomSeed);
        // This is an in-memory model, not a hosted lobby. No Server, client,
        // socket, or upload is created by the distribution tester.
        var room = new ServerRoom("Local distribution test", "", null!, settings) { CategoryConfig = definition.Categories };
        var objectives = ((JArray)definition.Root["squares"]!).Select((s, i) => (Square: s, Index: i))
            .Where(s => !string.IsNullOrWhiteSpace(s.Square.Value<string>("name"))).ToArray();
        var counts = new int[((JArray)definition.Root["squares"]!).Count];
        var centers = new int[counts.Length];
        var variants = new Dictionary<(int Index, string Text), (int Selected, int Center)>();
        var failureCounts = new Dictionary<GenerationIssue, int>();
        var selectionCounts = objectives.ToDictionary(o => o.Index, _ => new int[7]);
        var categories = new Dictionary<string, (int Count, int Boards)>(StringComparer.OrdinalIgnoreCase);
        var regions = new Dictionary<string, (int Count, int Boards)>(StringComparer.OrdinalIgnoreCase);
        var categoryNames = objectives.ToDictionary(s => s.Index, s => Names(s.Square, "category", "categories"));
        var regionNames = objectives.ToDictionary(s => s.Index, s => Names(s.Square, "region", "regions"));
        foreach (var name in categoryNames.Values.SelectMany(n => n)) categories.TryAdd(name, (0, 0));
        foreach (var name in regionNames.Values.SelectMany(n => n)) regions.TryAdd(name, (0, 0));
        int successful = 0;
        long points = 0;
        ServerBingoBoard? sample = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            var chosen = new List<int>();
            var positions = new Dictionary<int, int>();
            var diagnostics = new GenerationDiagnostics();
            var board = generator.CreateBingoBoard(room, (index, position) =>
            {
                chosen.Add(index);
                positions[index] = position;
                counts[index]++;
                if (settings.BoardSize % 2 == 1 && position == settings.BoardSize * settings.BoardSize / 2) centers[index]++;
            }, diagnostics);
            foreach (var issue in diagnostics.Failures.Distinct()) failureCounts[issue] = failureCounts.GetValueOrDefault(issue) + 1;
            foreach (var pair in diagnostics.Objectives)
            {
                var diagnostic = pair.Value;
                var stats = selectionCounts[pair.Key];
                if (diagnostic.Chosen && board == null) stats[0]++;
                if (diagnostic.CategoryRejected) stats[1]++;
                if (diagnostic.RegionRejected) stats[2]++;
                if (diagnostic.ForcedCenterExcluded) stats[3]++;
                if (diagnostic.CenterRejected) stats[4]++;
                if (!diagnostic.Chosen && !diagnostic.ForcedCenterExcluded && !diagnostic.Considered) stats[5]++;
                if (!diagnostic.Chosen && !diagnostic.ForcedCenterExcluded && diagnostic.Considered &&
                    !diagnostic.CategoryRejected && !diagnostic.RegionRejected && !diagnostic.CenterRejected) stats[6]++;
            }
            if (board != null)
            {
                successful++;
                sample ??= board;
                points += board.Squares.Sum(s => (long)s.PointValue);
                foreach (var index in chosen)
                {
                    var position = positions[index];
                    var key = (index, board.Squares[position].Text);
                    var previous = variants.GetValueOrDefault(key);
                    var inCenter = settings.BoardSize % 2 == 1 && position == settings.BoardSize * settings.BoardSize / 2;
                    variants[key] = (previous.Selected + 1, previous.Center + (inCenter ? 1 : 0));
                }
                AddGroups(categories, chosen.SelectMany(i => categoryNames[i]));
                AddGroups(regions, chosen.SelectMany(i => regionNames[i]));
            }
            if ((attempt + 1) % Math.Max(1, attempts / 100) == 0 || attempt + 1 == attempts)
                progress?.Report((attempt + 1) * 100 / attempts);
        }
        double percent(int count) => successful == 0 ? 0 : count * 100d / successful;
        GroupDistribution[] groups(Dictionary<string, (int Count, int Boards)> source) => source.OrderBy(p => p.Key)
            .Select(p => new GroupDistribution(p.Key, p.Value.Count, p.Value.Boards, successful == 0 ? 0 : p.Value.Count / (double)successful, percent(p.Value.Boards))).ToArray();
        var sources = objectives.Select(s => new ObjectiveDistribution(
            s.Index + 1, s.Square.Value<string>("name")!, s.Square.Value<decimal?>("weight") ?? 1,
            s.Square.Value<int?>("point-value") ?? 1, s.Square.Value<int?>("center") switch { 0 => "Never", 1 => "Forced", _ => "Normal" },
            string.Join(", ", categoryNames[s.Index]), string.Join(", ", regionNames[s.Index]), counts[s.Index], centers[s.Index], percent(counts[s.Index]), Timing: DescribeTiming(s.Square))).ToArray();
        var sourceByIndex = sources.ToDictionary(s => s.Index);
        return new(attempts, successful, attempts - successful, sources,
            groups(categories), groups(regions), sample, successful == 0 ? 0 : points / (double)successful)
        {
            ObjectiveCategories = categoryNames.ToDictionary(p => p.Key + 1, p => p.Value),
            GenerationFailures = failureCounts.OrderByDescending(p => p.Value).ThenBy(p => p.Key.Reason).ThenBy(p => p.Key.Detail)
                .Select(p => new GenerationFailureDistribution(p.Key.Reason, p.Key.Detail, p.Value, p.Value * 100d / attempts)).ToArray(),
            SelectionDiagnostics = sources.Select(s =>
            {
                var stats = selectionCounts[s.Index - 1];
                return new ObjectiveSelectionDistribution(s.Index, s.Objective, s.Selected, stats[0], stats[1], stats[2], stats[3], stats[4], stats[5], stats[6]);
            }).ToArray(),
            ResolvedObjectives = variants.OrderBy(v => v.Key.Index).ThenBy(v => v.Key.Text, StringComparer.Ordinal).Select(v =>
                sourceByIndex[v.Key.Index + 1] with { Objective = v.Key.Text, Template = sourceByIndex[v.Key.Index + 1].Objective,
                    Selected = v.Value.Selected, CenterSelected = v.Value.Center, BoardPercent = percent(v.Value.Selected) }).ToArray()
        };
    }

    private static string[] Names(JToken square, string singular, string plural)
    {
        var names = new List<string>();
        if (square.Value<string>(singular) is string name) names.Add(name.Trim());
        if (square[plural] is JArray array) names.AddRange(array.Values<string>().Where(s => s != null).Select(s => s!.Trim()));
        return names.Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static string DescribeTiming(JToken square)
    {
        var name = square.Value<string>("name")!;
        return string.Join(" | ", System.Text.RegularExpressions.Regex.Matches(name, @"%(\w+)%").Select(m => m.Groups[1].Value).Distinct()
            .Where(key => square[key] is JObject)
            .Select(key => key + ": " + TimedTokenDefinition.Parse((JObject)square[key]!, key, name).Describe()));
    }
    private static void AddGroups(Dictionary<string, (int Count, int Boards)> target, IEnumerable<string> names)
    {
        foreach (var group in names.GroupBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            var old = target[group.Key];
            target[group.Key] = (old.Count + group.Count(), old.Boards + 1);
        }
    }
}
