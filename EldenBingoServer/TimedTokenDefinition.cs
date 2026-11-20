using Newtonsoft.Json.Linq;

namespace EldenBingoServer;

public sealed record TimedTokenStage(int AtSeconds, string Value);
public sealed record TimedTokenDefinition(TimedTokenStage[] Stages)
{
    public static TimedTokenDefinition Parse(JObject token, string tokenName, string objective)
    {
        string error(string detail) => $"Timed token '{tokenName}' in '{objective}': {detail}";
        if (token.Value<string>("mode") != "timed") throw new ArgumentException(error("mode must be 'timed'. Use an array for random choices."));
        if (token["stages"] is not JArray stages || stages.Count == 0) throw new ArgumentException(error("stages must be a nonempty array."));
        var parsed = new List<TimedTokenStage>();
        foreach (var stage in stages)
        {
            if (stage is not JObject item || item["at-seconds"]?.Type != JTokenType.Integer)
                throw new ArgumentException(error("each stage needs a whole-number at-seconds."));
            var seconds = item.Value<long>("at-seconds");
            if (seconds < 0 || seconds > int.MaxValue / 1000 || parsed.Count == 0 && seconds != 0 || parsed.Count > 0 && seconds <= parsed[^1].AtSeconds)
                throw new ArgumentException(error($"at-seconds must start at 0, strictly increase, and not exceed {int.MaxValue / 1000}."));
            var value = item["value"];
            if (value == null || value.Type is not (JTokenType.String or JTokenType.Integer or JTokenType.Float))
                throw new ArgumentException(error("each stage needs a string or number value."));
            parsed.Add(new((int)seconds, value.Value<string>()!));
        }
        return new(parsed.ToArray());
    }

    public string ValueAt(int seconds) => Stages.Last(s => s.AtSeconds <= Math.Max(0, seconds)).Value;
    public string Describe() => string.Join("; ", Stages.Select(s => $"{s.AtSeconds}s={s.Value}"));
}

public sealed class TimedSquareState
{
    public string Template { get; set; } = "";
    public string OriginalTooltip { get; set; } = "";
    public Dictionary<string, TimedTokenDefinition> Tokens { get; set; } = new();
    public bool Frozen { get; set; }
    public int ElapsedSeconds { get; set; }

    public string TextAt(int seconds)
    {
        var text = Template;
        foreach (var token in Tokens) text = text.Replace($"%{token.Key}%", token.Value.ValueAt(seconds));
        return text;
    }

    public string Tooltip()
    {
        var times = Tokens.Values.SelectMany(t => t.Stages).Select(s => s.AtSeconds).Distinct().OrderBy(s => s);
        var timeline = string.Join("\n", times.Select(s => $"{s / 60:00}:{s % 60:00} — {TextAt(s)}"));
        return (string.IsNullOrEmpty(OriginalTooltip) ? "" : OriginalTooltip + "\n\n") +
            "Timed stages (match time; freezes on first claim):\n" + timeline +
            (Frozen ? "\nFrozen after first claim." : "");
    }
}
