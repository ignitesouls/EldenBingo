using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EldenBingoCommon
{
    public class CategoryConfig
    {
        [JsonProperty]
        private readonly Dictionary<string, int> _categoriesMax;

        [JsonProperty]
        private readonly Dictionary<string, int> _categoriesMin;

        public CategoryConfig()
        {
            _categoriesMax = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _categoriesMin = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        // ----- MAX -----
        public void SetCategory(string category, int limit) => _categoriesMax[category] = limit;
        public void RemoveCategory(string category) => _categoriesMax.Remove(category);

        public int GetCategoryLimit(string category)
            => _categoriesMax.TryGetValue(category, out var limit) ? limit : int.MaxValue;

        // ----- MIN -----
        public void SetCategoryMinimum(string category, int min) => _categoriesMin[category] = min;
        public void RemoveCategoryMinimum(string category) => _categoriesMin.Remove(category);

        public int GetCategoryMinimum(string category)
            => _categoriesMin.TryGetValue(category, out var min) ? min : 0;

        public IReadOnlyDictionary<string, int> GetAllMinimums() => _categoriesMin;

        // ----- JSON helpers (keep old behavior) -----
        public static CategoryConfig FromJson(JObject root)
        {
            var config = new CategoryConfig();

            // max limits (newer style)
            if (root["categoryLimits"] is JObject limits)
            {
                foreach (var kv in limits)
                {
                    if (kv.Value?.Type == JTokenType.Integer)
                        config.SetCategory(kv.Key, kv.Value.Value<int>());
                }
            }

            // minimums (newer style)
            if (root["categoryMinimums"] is JObject mins)
            {
                foreach (var kv in mins)
                {
                    if (kv.Value?.Type == JTokenType.Integer)
                        config.SetCategoryMinimum(kv.Key, kv.Value.Value<int>());
                }
            }

            return config;
        }

        // ParseConfig was your "category limits" parser; keep it, and add one for mins
        public static CategoryConfig ParseConfig(JObject configObject)
        {
            var config = new CategoryConfig();
            try
            {
                foreach (var kv in configObject)
                {
                    try
                    {
                        var i = Convert.ToInt32(kv.Value);
                        config.SetCategory(kv.Key, i);
                    }
                    catch { }
                }
            }
            catch (JsonReaderException) { }
            return config;
        }

        //Top 10 ChatGPT Comments
        public void ParseMinimums(JObject minsObject)
        {
            try
            {
                foreach (var kv in minsObject)
                {
                    try
                    {
                        var i = Convert.ToInt32(kv.Value);
                        SetCategoryMinimum(kv.Key, i);
                    }
                    catch { }
                }
            }
            catch (JsonReaderException) { }
        }
    }
}