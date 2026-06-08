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
                foreach (var textToken in getTokens(name))
                {
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
                    (CenterType)center.GetValueOrDefault(0),
                    //Add Region
                    regions.Count == 0 ? null : regions.ToArray()
                //Add Region
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

        public ServerBingoBoard? CreateBingoBoard(ServerRoom room)
        {
            var squareList = new List<BingoJsonObj>(weightedShuffleSquares(_list));
            var squares = new List<BingoJsonObj>();
            var categoryCount = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            var usedRegionsByGroup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in _regionLimitConfig.Groups)
                usedRegionsByGroup[g.Name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var numSquares = room.GameSettings.BoardSize * room.GameSettings.BoardSize;
            bool anySquareFailedCategoryLimit = false;
            bool minimumSquaresWereForced = false;

            BingoJsonObj? centerSquare = null;

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
                --numSquares;

                foreach (var category in centerSquare.Value.Categories)
                {
                    categoryCount.TryGetValue(category, out decimal count);
                    categoryCount[category] = count + centerSquare.Value.Weight;
                }

                ApplySquareRegions(centerSquare.Value, usedRegionsByGroup);
            }

            var remainingMin = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

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
                DecrementMinimums(remainingMin, centerSquare.Value);
            }

            int guard = 0;
            while (remainingMin.Count > 0 && AnyMinimumsRemaining(remainingMin))
            {
                guard++;
                if (guard > 50000)
                    return null;

                int bestIndex = -1;
                decimal bestScore = 0;

                for (int i = 0; i < squareList.Count; i++)
                {
                    var sq = squareList[i];

                    decimal score = ScoreSquareForMinimums(remainingMin, sq);
                    if (score <= 0)
                        continue;

                    if (!PassesMaxCategoryLimits(room, sq, categoryCount))
                        continue;

                    if (!PassesRegionDistinctLimit(sq, usedRegionsByGroup))
                        continue;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                if (bestIndex == -1)
                    return null;

                var picked = squareList[bestIndex];
                squareList.RemoveAt(bestIndex);

                squares.Add(picked);
                minimumSquaresWereForced = true;

                foreach (var category in picked.Categories)
                {
                    categoryCount.TryGetValue(category, out decimal count);
                    categoryCount[category] = count + picked.Weight;
                }

                ApplySquareRegions(picked, usedRegionsByGroup);
                DecrementMinimums(remainingMin, picked);

                if (squares.Count >= numSquares)
                    break;
            }

            while (squareList.Count > 0 && squares.Count < numSquares)
            {
                bool thisSquareFailedCategoryCheck = false;

                BingoJsonObj potentialSquare = squareList[0];
                squareList.RemoveAt(0);

                foreach (var category in potentialSquare.Categories)
                {
                    int limit = room.GameSettings.CategoryLimit > 0 ? room.GameSettings.CategoryLimit : 99999;

                    if (room.CategoryConfig != null)
                        limit = Math.Min(limit, room.CategoryConfig.GetCategoryLimit(category));

                    categoryCount.TryGetValue(category, out decimal count);
                    if (count + potentialSquare.Weight > limit)
                    {
                        anySquareFailedCategoryLimit = true;
                        thisSquareFailedCategoryCheck = true;
                        break;
                    }
                }

                if (thisSquareFailedCategoryCheck)
                    continue;

                if (!PassesRegionDistinctLimit(potentialSquare, usedRegionsByGroup))
                    continue;

                squares.Add(potentialSquare);

                foreach (var category in potentialSquare.Categories)
                {
                    categoryCount.TryGetValue(category, out decimal count);
                    categoryCount[category] = count + potentialSquare.Weight;
                }

                ApplySquareRegions(potentialSquare, usedRegionsByGroup);
            }

            if (squares.Count != numSquares)
                return null;

            if (anySquareFailedCategoryLimit || minimumSquaresWereForced)
                squares = shuffleList(squares, _random).ToList();

            if (centerSquare.HasValue)
            {
                squares.Insert(numSquares / 2, centerSquare.Value);
            }

            balanceBoard(squares, centerSquare.HasValue);

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

            return new ServerBingoBoard(
                room,
                room.GameSettings.BoardSize,
                room.GameSettings.Lockout,
                squares.Select(s =>
                    new BingoBoardSquare(
                        getTextWithResolvedTokens(s),
                        s.Tooltip,
                        Array.Empty<int>(),
                        false,
                        Array.Empty<SquareCounter>()
                    )
                ).ToArray(),
                classes
            );
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
        private static void DecrementMinimums(Dictionary<string, decimal> remainingMin, BingoJsonObj square)
        {
            foreach (var c in square.Categories)
            {
                if (remainingMin.TryGetValue(c, out var need) && need > 0)
                    remainingMin[c] = Math.Max(0, need - square.Weight);
            }
        }

        private static bool AnyMinimumsRemaining(Dictionary<string, decimal> remainingMin)
            => remainingMin.Values.Any(v => v > 0);

        private static decimal ScoreSquareForMinimums(Dictionary<string, decimal> remainingMin, BingoJsonObj sq)
        {
            decimal score = 0;
            foreach (var c in sq.Categories)
            {
                if (remainingMin.TryGetValue(c, out var need) && need > 0)
                    score += Math.Min(need, sq.Weight);
            }
            return score;
        }

        private bool PassesMaxCategoryLimits(ServerRoom room, BingoJsonObj potentialSquare, Dictionary<string, decimal> categoryCount)
        {
            foreach (var category in potentialSquare.Categories)
            {
                int limit = room.GameSettings.CategoryLimit > 0 ? room.GameSettings.CategoryLimit : 99999;
                if (room.CategoryConfig != null)
                    limit = Math.Min(limit, room.CategoryConfig.GetCategoryLimit(category));

                categoryCount.TryGetValue(category, out decimal count);
                if (count + potentialSquare.Weight > limit)
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
            public BingoJsonObj(string text, string? tooltip = null, decimal weight = 1, string[]? categories = null, IDictionary<string, string[]>? tokens = null, CenterType center = CenterType.None, string[]? regions = null)
            {
                Text = text;
                Tooltip = tooltip == null ? string.Empty : tooltip;
                Weight = weight;
                Categories = new HashSet<string>(categories ?? Array.Empty<string>());
                Tokens = tokens;
                CenterType = center;
                Regions = new HashSet<string>(regions ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            }

            public string Text { get; init; }
            public string Tooltip { get; init; }
            public decimal Weight { get; init; }
            public ISet<string> Categories { get; init; }
            public IDictionary<string, string[]>? Tokens { get; init; }
            public CenterType CenterType { get; init; }
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
        }
    }
}
