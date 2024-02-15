namespace m4d.ViewModels;

public class BotHitModel
{
    public string Agent { get; set; }
    public long Hits { get; set; }
    public double Rate { get; set; }

    public static IEnumerable<BotHitModel> Create(TimeSpan upTime, long emptyAgentCount,
        Dictionary<string, long> bots)
    {
        var bhs = new List<BotHitModel>();
        var seconds = upTime.TotalSeconds;
        if (emptyAgentCount > 0)
        {
            bhs.Add(CreateOne("<null>", emptyAgentCount, seconds));
        }

        bhs.AddRange(
            from entry in bots
            where entry.Value > 0
            select CreateOne(entry.Key, entry.Value, seconds));

        return bhs;
    }

    private static BotHitModel CreateOne(string agent, long count, double upTime)
    {
        return new() { Agent = agent, Hits = count, Rate = Math.Round(count / upTime, 4) };
    }
}
