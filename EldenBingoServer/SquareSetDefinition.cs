using EldenBingoCommon;
using Newtonsoft.Json.Linq;

namespace EldenBingoServer;

// Shared by live lobbies and the local distribution tester.
public sealed class SquareSetDefinition
{
    public JObject Root { get; }
    public CategoryConfig Categories { get; }
    private SquareSetDefinition(JObject root)
    {
        Root = root;
        Categories = CategoryConfig.FromJson(root);
        if (root["category limits"] is JObject limits)
            foreach (var pair in limits) Categories.SetCategory(pair.Key, pair.Value!.Value<int>());
        var minimums = root.GetValue("category minimums", StringComparison.OrdinalIgnoreCase) as JObject;
        if (minimums != null) Categories.ParseMinimums(minimums);
        if (root.GetValue("setRegionLimits", StringComparison.OrdinalIgnoreCase) is JArray regions)
            root["setRegionLimits"] = regions;
    }
    public static SquareSetDefinition Parse(string json)
    {
        var token = JToken.Parse(json);
        var root = token is JArray array ? new JObject { ["squares"] = array } : token as JObject;
        if (root?["squares"] is not JArray) throw new ArgumentException("The JSON must be an array of objectives or an object containing a squares array.");
        return new SquareSetDefinition(root);
    }
}
