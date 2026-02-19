using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EldenBingoCommon
{
    public class CategoryConfig
    {
        [JsonProperty]
        private readonly Dictionary<string, int> _categories;

        public CategoryConfig()
        {
            _categories = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public void SetCategory(string category, int limit)
        {
            _categories[category] = limit;
        }

        public void RemoveCategory(string category)
        {
            _categories.Remove(category);
        }

        public int GetCategoryLimit(string category)
        {
            return _categories.TryGetValue(category, out var limit)
                ? limit
                : int.MaxValue;
        }
        public static CategoryConfig FromJson(JObject root)
        {
            var config = new CategoryConfig();

            if (root["categoryLimits"] is not JObject limits)
                return config;

            foreach (var kv in limits)
            {
                if (kv.Value?.Type == JTokenType.Integer)
                {
                    config.SetCategory(kv.Key, kv.Value.Value<int>());
                }
            }

            return config;
        }

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
                    catch (Exception) { }
                }
            }
            catch (JsonReaderException) { }
            return config;
        }
    }
}

