using m4d.ViewModels;

namespace m4d.Utilities;

public class BotFilterInfo
{
    public const string SectionName = "Configuration:BotFilter";

    public string ExcludeTokens { get; set; }
    public string ExcludeFragments { get; set; }
    public string BadFragments { get; set; }

    public IReadOnlyList<string> ExcludeTokenList => ListFromString(ExcludeTokens);
    public IReadOnlyList<string> ExcludeFragmentList => ListFromString(ExcludeFragments);
    public IReadOnlyList<string> BadFragmentList => ListFromString(BadFragments);

    private static IReadOnlyList<string> ListFromString(string value) =>
        value != null
        ? value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : new List<string>();
}

public static class SpiderManager
{
    public static DateTime StartTime { get; } = DateTime.Now;
    public static TimeSpan UpTime => DateTime.Now - StartTime;

    public static bool CheckAnySpiders(string userAgent, IConfiguration configuration)
    {
        return CheckSpiders(userAgent, false, configuration);
    }

    public static bool CheckBadSpiders(string userAgent, IConfiguration configuration)
    {
        return CheckSpiders(userAgent, true, configuration);
    }

    private static bool CheckSpiders(string userAgent, bool badOnly, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            EmptyAgents += 1;
            return true;
        }

        var agent = userAgent.ToLower();
        var botFilter = configuration
            .GetSection(BotFilterInfo.SectionName).Get<BotFilterInfo>();

        if (!botFilter.ExcludeFragmentList.Any(agent.Contains) &&
            !botFilter.ExcludeTokenList.Any(t => agent.Equals(t)))
        {
            return false;
        }

        lock (s_botHits)
        {
            if (!s_botHits.TryGetValue(agent, out var count))
            {
                count = 0;
            }

            s_botHits[agent] = count + 1;
        }

        return !badOnly || botFilter.BadFragmentList.Any(agent.Contains);
    }

    public static IEnumerable<BotHitModel> CreateBotReport()
    {
        lock (s_botHits)
        {
            return BotHitModel.Create(UpTime, EmptyAgents, s_botHits);
        }
    }

    private static readonly Dictionary<string, long> s_botHits = [];

    public static int EmptyAgents { get; set; }
}
