using Azure;
using Azure.Data.Tables;

using m4dApi;

namespace m4d.PublicApi;

/// <summary>
/// One client's counts for one UTC day, as m4d-api's usage table holds them. See the Metering
/// section of architecture/plans/partner-api-standalone-host.md.
/// </summary>
public record PartnerUsageDay(
    DateOnly Day,
    long Exact = 0,
    long Ambiguous = 0,
    long Fuzzy = 0,
    long None = 0,
    long Total = 0,
    long Limited = 0,
    long CapLimited = 0,
    long RateLimitedMinutes = 0)
{
    /// <summary>Requests the rate limit turned away (the cap's are the rest of Limited).</summary>
    public long RateLimited => Math.Max(0, Limited - CapLimited);

    public static PartnerUsageDay FromEntity(TableEntity entity) =>
        new(DateOnly.ParseExact(entity.RowKey, "yyyy-MM-dd"),
            Count(entity, UsageColumns.Exact),
            Count(entity, UsageColumns.Ambiguous),
            Count(entity, UsageColumns.Fuzzy),
            Count(entity, UsageColumns.None),
            Count(entity, UsageColumns.Total),
            Count(entity, UsageColumns.Limited),
            Count(entity, UsageColumns.CapLimited),
            Count(entity, UsageColumns.RateLimitedMinutes));

    private static long Count(TableEntity entity, string column) => entity.GetInt64(column) ?? 0;
}

/// <summary>Reads m4d-api's usage table. The web app only reads it; the API writes it.</summary>
public interface IPartnerUsageReader
{
    /// <summary>False when PartnerApi:UsageTableUri isn't set (local development, typically).</summary>
    bool IsConnected { get; }

    /// <summary>The client's rows from <paramref name="from"/> to <paramref name="to"/>, inclusive.
    /// Days with no traffic have no row.</summary>
    Task<IReadOnlyList<PartnerUsageDay>> Days(string clientId, DateOnly from, DateOnly to);
}

public class PartnerUsageReader(TableClient table) : IPartnerUsageReader
{
    public bool IsConnected => true;

    public async Task<IReadOnlyList<PartnerUsageDay>> Days(string clientId, DateOnly from, DateOnly to)
    {
        var filter = TableClient.CreateQueryFilter(
            $"PartitionKey eq {clientId} and RowKey ge {Key(from)} and RowKey le {Key(to)}");
        var days = new List<PartnerUsageDay>();
        try
        {
            await foreach (var entity in table.QueryAsync<TableEntity>(filter))
            {
                days.Add(PartnerUsageDay.FromEntity(entity));
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // The API creates the table on its first flush; before then there's no usage
        }

        return days;
    }

    private static string Key(DateOnly day) => day.ToString("yyyy-MM-dd");
}

public class UnconnectedPartnerUsageReader : IPartnerUsageReader
{
    public bool IsConnected => false;

    public Task<IReadOnlyList<PartnerUsageDay>> Days(string clientId, DateOnly from, DateOnly to) =>
        Task.FromResult<IReadOnlyList<PartnerUsageDay>>([]);
}

/// <summary>
/// A client's trailing 30 UTC days, newest first, with a zero row for every day without traffic,
/// and the figures the usage page leads with.
/// </summary>
public class PartnerUsageReport
{
    public const int TrailingDays = 30;

    public IReadOnlyList<PartnerUsageDay> Days { get; init; } = [];
    public DateOnly Today { get; init; }
    public long SoftCap { get; init; }

    public long Total => Days.Sum(d => d.Total);
    public long AveragePerDay => Days.Count == 0 ? 0 : Total / Days.Count;
    public PartnerUsageDay Busiest => Days.Where(d => d.Total > 0).MaxBy(d => d.Total);
    public int DaysOverSoftCap => Days.Count(d => d.Total >= SoftCap);
    public long RateLimited => Days.Sum(d => d.RateLimited);
    public long RateLimitedMinutes => Days.Sum(d => d.RateLimitedMinutes);
    public long CapLimited => Days.Sum(d => d.CapLimited);
    public long Largest => Days.Count == 0 ? 0 : Days.Max(d => Math.Max(d.Total, d.Limited));

    public static PartnerUsageReport Trailing(IEnumerable<PartnerUsageDay> rows, DateOnly today, long softCap)
    {
        var byDay = rows.ToDictionary(r => r.Day);
        return new PartnerUsageReport
        {
            Today = today,
            SoftCap = softCap,
            Days = [.. Enumerable.Range(0, TrailingDays)
                .Select(offset => today.AddDays(-offset))
                .Select(day => byDay.GetValueOrDefault(day) ?? new PartnerUsageDay(day))]
        };
    }
}
