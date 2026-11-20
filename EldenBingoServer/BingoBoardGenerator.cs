using EldenBingoCommon;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace EldenBingoServer
{
    public class BingoBoardGenerator
    {
        private readonly IList<BingoJsonObj> _list;
        private int _randomSeed;
        private Random _random;

        private readonly RegionLimitConfig _regionLimitConfig;


        public BingoBoardGenerator(JObject root, int randomSeed)
        {
            RandomSeed = randomSeed;
            _list = new List<BingoJsonObj>();


            var categoryConfig = CategoryConfig.FromJson(root);

            _regionLimitConfig = RegionLimitConfig.FromJson(root);


            var squareArray = root["squares"] as JArray
                ?? throw new Exception("Missing 'squares' array");

            foreach (var square in squareArray)
            {
                string? name = square.Value<string>("name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string? tooltip = square.Value<string>("tooltip");
                decimal? weight = square.Value<decimal?>("weight");
                if (weight <= 0)
                    throw new Exception($"Weight must be greater than zero for '{name}'");

                string? category = square.Value<string>("category");
                int? center = square.Value<int?>("center");
                var pointToken = square["point-value"];
                var maxPoints = int.MaxValue / (BingoConstants.BoardSizeMax * BingoConstants.BoardSizeMax);
                if (pointToken != null && (pointToken.Type != JTokenType.Integer || pointToken.Value<long>() < 0 || pointToken.Value<long>() > maxPoints))
                    throw new Exception($"point-value for '{name}' must be a whole number between 0 and {maxPoints}");
                int pointValue = pointToken?.Value<int>() ?? 1;

                var categories = new HashSet<string>();

                if (category != null)
                    categories.Add(category.Trim());

                var categoryArray = square.Value<JArray>("categories");
                if (categoryArray != null)
                {
                    foreach (var v in categoryArray.OfType<JValue>())
                    {
                        if (v.Value is string c)
                            categories.Add(c.Trim());
                    }
                }

                var regions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string? region = square.Value<string>("region");
                if (!string.IsNullOrWhiteSpace(region))
                    regions.Add(region.Trim());

                var regionsArray = square.Value<JArray>("regions");
                if (regionsArray != null)
                {
                    foreach (var v in regionsArray.OfType<JValue>())
                    {
                        if (v.Value is string r && !string.IsNullOrWhiteSpace(r))
                            regions.Add(r.Trim());
                    }
                }


                var tokenDict = new Dictionary<string, string[]>();
                var timedTokens = new Dictionary<string, TimedTokenDefinition>();
                foreach (var textToken in getTokens(name).Distinct())
                {
                    if (square[textToken] is JObject timed)
                    {
                        timedTokens[textToken] = TimedTokenDefinition.Parse(timed, textToken, name);
                        continue;
                    }
                    var tokenArray = square.Value<JArray>(textToken)
                        ?? throw new Exception($"Non-existent token '{textToken}' in '{name}'");

                    if (!tokenDict.ContainsKey(textToken))
                    {
                        tokenDict[textToken] = tokenArray
                            .Select(t => t.Value<string>()!)
                            .ToArray();
                    }
                }

                _list.Add(new BingoJsonObj(
                    name,
                    tooltip,
                    weight.GetValueOrDefault(1),
                    categories.ToArray(),
                    tokenDict.Count == 0 ? null : tokenDict,
                    center switch { 0 => CenterType.ForbiddenCenter, 1 => CenterType.ForcedCenter, _ => CenterType.None },
                    regions.Count == 0 ? null : regions.ToArray(),
                    pointValue,
                    squareArray.IndexOf(square),
                    timedTokens
                ));
            }
        }

        public int RandomSeed
        {
            get { return _randomSeed; }
            set
            {
                //Only create a new Random when the seed changes. As long as we're using the same random seed, it will
                //generate a sequence of boards based on that seed, but not the same board every time
                if (value == 0 || value != _randomSeed)
                {
                    _randomSeed = value;
                    _random = value == 0 ? new Random() : new Random(value);
                }
            }
        }

        public ServerBingoBoard? CreateBingoBoard(ServerRoom room, Action<int, int>? selectedObjective = null, GenerationDiagnostics? diagnostics = null)
        {
            var squareList = new List<BingoJsonObj>(weightedShuffleSquares(_list));
            var squares = new List<BingoJsonObj>();
            var categoryCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var usedRegionsByGroup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in _regionLimitConfig.Groups)
                usedRegionsByGroup[g.Name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var numSquares = room.GameSettings.BoardSize * room.GameSettings.BoardSize;
            bool anySquareFailedCategoryLimit = false;
            bool minimumSquaresWereForced = false;

            BingoJsonObj? centerSquare = null;
            if (diagnostics != null) foreach (var square in _list) diagnostics.For(square.SourceIndex);
            void completeTrace()
            {
                if (diagnostics == null) return;
                foreach (var square in squares) diagnostics.For(square.SourceIndex).Chosen = true;
                if (centerSquare.HasValue) diagnostics.For(centerSquare.Value.SourceIndex).Chosen = true;
            }
            ServerBingoBoard? fail(string reason, string detail)
            {
                if (diagnostics != null)
                {
                    completeTrace();
                    diagnostics.Failures.Add(new(reason, detail));
                    if (diagnostics.CategoryBlockers.Count > 0)
                        diagnostics.Failures.Add(new("Category limits blocked candidates", string.Join("; ", diagnostics.CategoryBlockers.OrderBy(s => s))));
                    if (diagnostics.RegionBlockers.Count > 0)
                        diagnostics.Failures.Add(new("Region limits blocked candidates", string.Join("; ", diagnostics.RegionBlockers.OrderBy(s => s))));
                }
                return null;
            }
            void categoryRejected(BingoJsonObj square)
            {
                if (diagnostics == null) return;
                diagnostics.For(square.SourceIndex).CategoryRejected = true;
                foreach (var category in square.Categories)
                {
                    var limit = room.GameSettings.CategoryLimit > 0 ? room.GameSettings.CategoryLimit : 99999;
                    limit = Math.Min(limit, room.CategoryConfig?.GetCategoryLimit(category) ?? int.MaxValue);
                    if (categoryCount.GetValueOrDefault(category) + 1 > limit)
                        diagnostics.CategoryBlockers.Add($"{category}: maximum {limit}");
                }
            }
            void regionRejected(BingoJsonObj square)
            {
                if (diagnostics == null) return;
                diagnostics.For(square.SourceIndex).RegionRejected = true;
                foreach (var group in _regionLimitConfig.Groups)
                {
                    var used = usedRegionsByGroup[group.Name];
                    var added = square.Regions.Count(r => group.RegionNames.Contains(r) && !used.Contains(r));
                    if (used.Count + added > group.RegionLimit)
                        diagnostics.RegionBlockers.Add($"{group.Name}: maximum {group.RegionLimit} distinct regions");
                }
            }

            if (numSquares % 2 == 1)
            {
                for (int i = squareList.Count - 1; i >= 0; --i)
                {
                    if (squareList[i].CenterType == CenterType.ForcedCenter)
                    {
                        centerSquare = squareList[i];
                        squareList.RemoveAt(i);
                    }
                }
            }

            if (centerSquare.HasValue)
            {
                if (diagnostics != null)
                    foreach (var square in _list.Where(s => s.CenterType == CenterType.ForcedCenter && s.SourceIndex != centerSquare.Value.SourceIndex))
                        diagnostics.For(square.SourceIndex).ForcedCenterExcluded = true;
                --numSquares;

                foreach (var category in centerSquare.Value.Categories)
                {
                    categoryCount.TryGetValue(category, out int count);
                    categoryCount[category] = count + 1;
                }

                ApplySquareRegions(centerSquare.Value, usedRegionsByGroup);
            }

            var remainingMin = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (room.CategoryConfig != null)
            {
                foreach (var kv in room.CategoryConfig.GetAllMinimums())
                {
                    if (kv.Value > 0)
                        remainingMin[kv.Key] = kv.Value;
                }
            }

            if (centerSquare.HasValue && remainingMin.Count > 0)
            {
                DecrementMinimums(remainingMin, centerSquare.Value.Categories);
            }

            int guard = 0;
            while (remainingMin.Count > 0 && AnyMinimumsRemaining(remainingMin))
            {
                guard++;
                if (guard > 50000)
                    return fail("Generation safety limit", "Category minimum selection exceeded 50,000 passes.");

                int bestIndex = -1;
                int bestScore = 0;

                for (int i = 0; i < squareList.Count; i++)
                {
                    var sq = squareList[i];

                    int score = ScoreSquareForMinimums(remainingMin, sq);
                    if (score <= 0)
                        continue;
                    if (diagnostics != null) diagnostics.For(sq.SourceIndex).Considered = true;

                    if (!PassesMaxCategoryLimits(room, sq, categoryCount))
                    {
                        categoryRejected(sq);
                        continue;
                    }

                    if (!PassesRegionDistinctLimit(sq, usedRegionsByGroup))
                    {
                        regionRejected(sq);
                        continue;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                if (bestIndex == -1)
                    return fail("Category minimums cannot be satisfied", string.Join("; ", remainingMin.Where(p => p.Value > 0)
                        .Select(p => $"{p.Key}: minimum {room.CategoryConfig!.GetCategoryMinimum(p.Key)}, selected {categoryCount.GetValueOrDefault(p.Key)}, still needs {p.Value}, maximum {Math.Min(room.GameSettings.CategoryLimit > 0 ? room.GameSettings.CategoryLimit : int.MaxValue, room.CategoryConfig.GetCategoryLimit(p.Key))}")));

                var picked = squareList[bestIndex];
                squareList.RemoveAt(bestIndex);

                squares.Add(picked);
                minimumSquaresWereForced = true;

                foreach (var category in picked.Categories)
                {
                    categoryCount.TryGetValue(category, out int count);
                    categoryCount[category] = count + 1;
                }

                ApplySquareRegions(picked, usedRegionsByGroup);
                DecrementMinimums(remainingMin, picked.Categories);

                if (squares.Count >= numSquares)
                    break;
            }

            if (AnyMinimumsRemaining(remainingMin))
                return fail("Board filled before category minimums were met", string.Join("; ", remainingMin.Where(p => p.Value > 0).Select(p => $"{p.Key}: still needs {p.Value}")));

            while (squareList.Count > 0 && squares.Count < numSquares)
            {
                bool thisSquareFailedCategoryCheck = false;

                BingoJsonObj potentialSquare = squareList[0];
                squareList.RemoveAt(0);
                if (diagnostics != null) diagnostics.For(potentialSquare.SourceIndex).Considered = true;

                foreach (var category in potentialSquare.Categories)
                {
                    int limit = room.GameSettings.CategoryLimit > 0 ? room.GameSettings.CategoryLimit : 99999;

                    if (room.CategoryConfig != null)
                        limit = Math.Min(limit, room.CategoryConfig.GetCategoryLimit(category));

                    categoryCount.TryGetValue(category, out int count);
                    if (count + 1 > limit)
                    {
                        anySquareFailedCategoryLimit = true;
                        thisSquareFailedCategoryCheck = true;
                        break;
                    }
                }

                if (thisSquareFailedCategoryCheck)
                {
                    categoryRejected(potentialSquare);
                    continue;
                }

                if (!PassesRegionDistinctLimit(potentialSquare, usedRegionsByGroup))
                {
                    regionRejected(potentialSquare);
                    continue;
                }

                // Leave the final slot for a center-eligible objective if none
                // has been selected yet. This still applies normal limits.
                if (!centerSquare.HasValue && numSquares % 2 == 1 && squares.Count == numSquares - 1 &&
                    potentialSquare.CenterType == CenterType.ForbiddenCenter &&
                    !squares.Any(s => s.CenterType != CenterType.ForbiddenCenter))
                {
                    if (diagnostics != null) diagnostics.For(potentialSquare.SourceIndex).CenterRejected = true;
                    continue;
                }

                squares.Add(potentialSquare);

                foreach (var category in potentialSquare.Categories)
                {
                    categoryCount.TryGetValue(category, out int count);
                    categoryCount[category] = count + 1;
                }

                ApplySquareRegions(potentialSquare, usedRegionsByGroup);
            }

            if (squares.Count != numSquares)
            {
                var available = _list.Count - (centerSquare.HasValue ? _list.Count(s => s.CenterType == CenterType.ForcedCenter) - 1 : 0);
                if (available < room.GameSettings.BoardSize * room.GameSettings.BoardSize)
                    return fail("Insufficient objectives", $"Need {room.GameSettings.BoardSize * room.GameSettings.BoardSize} objectives; only {available} are available after excluding unchosen forced-center candidates.");
                if (!centerSquare.HasValue && numSquares % 2 == 1 && !squares.Any(s => s.CenterType != CenterType.ForbiddenCenter))
                    return fail("No eligible center", "No center-eligible objective passed selection. Omit center or use center: 1 on an eligible objective; check its category and region limits.");
                return fail("Not enough eligible objectives", $"Selected {squares.Count + (centerSquare.HasValue ? 1 : 0)} of {room.GameSettings.BoardSize * room.GameSettings.BoardSize} required objectives. Review the constraint blockers below.");
            }

            if (anySquareFailedCategoryLimit || minimumSquaresWereForced)
                squares = shuffleList(squares, _random).ToList();

            if (centerSquare.HasValue)
            {
                squares.Insert(numSquares / 2, centerSquare.Value);
            }

            balanceBoard(squares, centerSquare.HasValue);

            // Explicit center: 0 only constrains placement, not inclusion.
            // Swap existing objectives so category/region counts stay intact.
            if (squares.Count % 2 == 1 && squares[squares.Count / 2].CenterType == CenterType.ForbiddenCenter)
            {
                var eligible = Enumerable.Range(0, squares.Count)
                    .Where(i => squares[i].CenterType != CenterType.ForbiddenCenter).ToArray();
                if (eligible.Length == 0) return fail("No eligible center", "Every selected objective has center: 0.");
                var replacement = eligible[_random.Next(eligible.Length)];
                var middle = squares.Count / 2;
                (squares[middle], squares[replacement]) = (squares[replacement], squares[middle]);
            }

            EldenRingClasses[] classes;

            if (room.GameSettings.RandomClasses && room.GameSettings.NumberOfClasses > 0)
            {
                classes = randomizeAvailableClasses(room.GameSettings.ValidClasses, room.GameSettings.NumberOfClasses);
            }
            else
            {
                classes = Array.Empty<EldenRingClasses>();
                _random.Next();
            }

            var timedSquares = new Dictionary<int, TimedSquareState>();
            var board = new ServerBingoBoard(
                room,
                room.GameSettings.BoardSize,
                room.GameSettings.Lockout,
                squares.Select((s, index) =>
                {
                    var text = getTextWithResolvedTokens(s);
                    var tooltip = s.Tooltip;
                    if (s.TimedTokens.Count > 0)
                    {
                        var state = new TimedSquareState { Template = text, OriginalTooltip = tooltip, Tokens = s.TimedTokens };
                        timedSquares[index] = state;
                        text = state.TextAt(0);
                        tooltip = state.Tooltip();
                    }
                    return new BingoBoardSquare(
                        text,
                        tooltip,
                        Array.Empty<int>(),
                        false,
                        Array.Empty<SquareCounter>(),
                        s.PointValue
                    );
                }).ToArray(),
                classes
            ) { TimedSquares = timedSquares };
            for (var i = 0; i < squares.Count; i++) selectedObjective?.Invoke(squares[i].SourceIndex, i);
            completeTrace();
            return board;
        }

        private EldenRingClasses[] randomizeAvailableClasses(IEnumerable<EldenRingClasses> availableClasses, int numberOfClasses)
        {
            var classRandom = new Random(_random.Next());
            var classesQueue = new Queue<EldenRingClasses>(shuffleList(availableClasses, classRandom));
            var pickedClasses = new List<EldenRingClasses>();
            while (classesQueue.Count > 0 && pickedClasses.Count < numberOfClasses)
            {
                pickedClasses.Add(classesQueue.Dequeue());
            }
            return pickedClasses.ToArray();
        }

        private IEnumerable<T> shuffleList<T>(IEnumerable<T> squares, Random random)
        {
            return squares.OrderBy(s => random.Next()).ToList();
        }

        private IEnumerable<BingoJsonObj> weightedShuffleSquares(IEnumerable<BingoJsonObj> squares)
        {
            // Weighted sampling without replacement: lower weights tend to appear later,
            // where category and region limits make them less likely to reach the boardish.
            return squares
                .Select(square => new
                {
                    Square = square,
                    Priority = -Math.Log(1d - _random.NextDouble()) / (double)square.Weight
                })
                .OrderBy(item => item.Priority)
                .Select(item => item.Square)
                .ToList();
        }

        private void balanceBoard(IList<BingoJsonObj> squares, bool centerLocked)
        {
            //TODO
        }

        //Add New Function for Regions as I don't want to add anything to EldenBingoCommon for Regions
        private sealed class RegionLimitConfig
        {
            public sealed class Group
            {
                public string Name { get; }
                public HashSet<string> RegionNames { get; }
                public int RegionLimit { get; }

                public Group(string name, HashSet<string> regionNames, int regionLimit)
                {
                    Name = name;
                    RegionNames = regionNames;
                    RegionLimit = regionLimit;
                }
            }

            public IReadOnlyList<Group> Groups => _groups;
            private readonly List<Group> _groups = new();

            // region -> groups containing it
            private readonly Dictionary<string, List<Group>> _regionToGroups =
                new(StringComparer.OrdinalIgnoreCase);

            public static RegionLimitConfig FromJson(JObject root)
            {
                var cfg = new RegionLimitConfig();

                var arr = root["setRegionLimits"] as JArray;
                if (arr == null) return cfg; // optional

                foreach (var token in arr.OfType<JObject>())
                {
                    var name = token.Value<string>("name") ?? "unnamed";
                    var limit = token.Value<int?>("regionLimit") ?? 0;
                    if (limit <= 0) continue;

                    var namesArr = token["regionNames"] as JArray;
                    if (namesArr == null) continue;

                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var v in namesArr.OfType<JValue>())
                    {
                        if (v.Value is string s && !string.IsNullOrWhiteSpace(s))
                            set.Add(s.Trim());
                    }

                    if (set.Count == 0) continue;

                    var g = new Group(name, set, limit);
                    cfg._groups.Add(g);

                    foreach (var r in set)
                    {
                        if (!cfg._regionToGroups.TryGetValue(r, out var list))
                        {
                            list = new List<Group>();
                            cfg._regionToGroups[r] = list;
                        }
                        list.Add(g);
                    }
                }

                return cfg;
            }

            public IReadOnlyList<Group> GetGroupsForRegion(string region)
            {
                return _regionToGroups.TryGetValue(region, out var list) ? list : Array.Empty<Group>();
            }
        }


        //Added to handle Minimums
        private static void DecrementMinimums(Dictionary<string, int> remainingMin, ISet<string> categories)
        {
            foreach (var c in categories)
            {
                if (remainingMin.TryGetValue(c, out var need) && need > 0)
                    remainingMin[c] = need - 1;
            }
        }

        private static bool AnyMinimumsRemaining(Dictionary<string, int> remainingMin)
            => remainingMin.Values.Any(v => v > 0);

        private static int ScoreSquareForMinimums(Dictionary<string, int> remainingMin, BingoJsonObj sq)
        {
            int score = 0;
            foreach (var c in sq.Categories)
            {
                if (remainingMin.TryGetValue(c, out var need) && need > 0)
                    score++;
            }
            return score;
        }

        private bool PassesMaxCategoryLimits(ServerRoom room, BingoJsonObj potentialSquare, Dictionary<string, int> categoryCount)
        {
            foreach (var category in potentialSquare.Categories)
            {
                int limit = room.GameSettings.CategoryLimit > 0 ? room.GameSettings.CategoryLimit : 99999;
                if (room.CategoryConfig != null)
                    limit = Math.Min(limit, room.CategoryConfig.GetCategoryLimit(category));

                categoryCount.TryGetValue(category, out int count);
                if (count + 1 > limit)
                    return false;
            }
            return true;
        }

        //Added to handle Minimums

        private IEnumerable<string> getTokens(string text)
        {
            return Regex.Matches(text, @"%(\w+)%").Select(m => m.Groups[1].Value);
        }

        private string getTextWithResolvedTokens(BingoJsonObj obj)
        {
            string text = obj.Text;
            if (obj.Tokens != null)
            {
                foreach (var kv in obj.Tokens)
                {
                    text = text.Replace($"%{kv.Key}%", pickOneAtRandom(kv.Value));
                }
            }
            return text;
        }

        private T pickOneAtRandom<T>(IList<T> items)
        {
            return items[_random.Next(items.Count)];
        }

        private bool PassesRegionDistinctLimit(
            BingoJsonObj sq,
            Dictionary<string, HashSet<string>> usedRegionsByGroup)
        {
            if (sq.Regions.Count == 0) return true;
            if (_regionLimitConfig.Groups.Count == 0) return true;

            // For each group, count how many NEW distinct regions this square would add
            foreach (var g in _regionLimitConfig.Groups)
            {
                if (!usedRegionsByGroup.TryGetValue(g.Name, out var used))
                    continue;

                int newDistinct = 0;

                foreach (var region in sq.Regions)
                {
                    if (!g.RegionNames.Contains(region)) continue; // not governed by this group
                    if (used.Contains(region)) continue;           // already activated
                    newDistinct++;
                }

                if (newDistinct > 0 && used.Count + newDistinct > g.RegionLimit)
                    return false;
            }

            return true;
        }

        private void ApplySquareRegions(BingoJsonObj sq, Dictionary<string, HashSet<string>> usedRegionsByGroup)
        {
            if (sq.Regions.Count == 0) return;
            if (_regionLimitConfig.Groups.Count == 0) return;

            foreach (var region in sq.Regions)
            {
                var groups = _regionLimitConfig.GetGroupsForRegion(region);

                if (groups.Count == 0) continue;

                foreach (var g in groups)
                {
                    if (usedRegionsByGroup.TryGetValue(g.Name, out var used))
                        used.Add(region);
                }
            }
        }

        private struct BingoJsonObj
        {
            public BingoJsonObj(string text, string? tooltip = null, decimal weight = 1, string[]? categories = null, IDictionary<string, string[]>? tokens = null, CenterType center = CenterType.None, string[]? regions = null, int pointValue = 1, int sourceIndex = -1, Dictionary<string, TimedTokenDefinition>? timedTokens = null)
            {
                Text = text;
                Tooltip = tooltip == null ? string.Empty : tooltip;
                Weight = weight;
                Categories = new HashSet<string>(categories ?? Array.Empty<string>());
                Tokens = tokens;
                CenterType = center;
                PointValue = pointValue;
                SourceIndex = sourceIndex;
                TimedTokens = timedTokens ?? new();
                Regions = new HashSet<string>(regions ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            }

            public string Text { get; init; }
            public string Tooltip { get; init; }
            public decimal Weight { get; init; }
            public ISet<string> Categories { get; init; }
            public IDictionary<string, string[]>? Tokens { get; init; }
            public CenterType CenterType { get; init; }
            public int PointValue { get; init; }
            public int SourceIndex { get; init; }
            public Dictionary<string, TimedTokenDefinition> TimedTokens { get; init; }
            public ISet<string> Regions { get; init; }

            public override string ToString()
            {
                return Text;
            }
        }

        private enum CenterType
        {
            None,
            ForcedCenter,
            ForbiddenCenter,
        }
    }
}
