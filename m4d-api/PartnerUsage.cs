namespace m4dApi;

/// <summary>
/// Limits for a client whose config secret leaves them out. The API's app settings
/// (PARTNER_DEFAULT_*) override the built-in values. Linked into the web app, which shows the
/// built-in values next to a client's blank limits.
/// </summary>
/// <remarks>
/// The rate, burst and daily cap are hard limits: past them a request gets 429. They're sized so
/// a client is never turned away unless it threatens the shared Search service. The soft cap only
/// warns: crossing it logs <c>Client {Client} passed its soft daily limit</c> once a day, for an
/// alert, while requests keep working.
/// </remarks>
public record PartnerDefaults(
    double RatePerSecond = PartnerDefaults.BuiltInRate,
    int Burst = PartnerDefaults.BuiltInBurst,
    long DailyCap = PartnerDefaults.BuiltInDailyCap,
    long SoftCap = PartnerDefaults.BuiltInSoftCap)
{
    public const double BuiltInRate = 20;
    public const int BuiltInBurst = 50;
    public const long BuiltInDailyCap = 100_000;
    public const long BuiltInSoftCap = 25_000;
}

/// <summary>
/// The columns of the API's usage table: one row per client and UTC day (PartitionKey client id,
/// RowKey yyyy-MM-dd). Linked into the web app, which reads the table for the usage pages.
/// </summary>
public static class UsageColumns
{
    public const string TableName = "usage";

    // One per resolve result; these are what the total and the daily cap count
    public const string Exact = "exact";
    public const string Ambiguous = "ambiguous";
    public const string Fuzzy = "fuzzy";
    public const string None = "none";

    public const string Total = "total";

    // Every request the rate limit or the daily cap turned away
    public const string Limited = "limited";

    // The part of Limited that the daily cap turned away; the rest the rate limit did
    public const string CapLimited = "capLimited";

    // Minutes in which an instance's rate limit turned away at least one request. With two
    // instances one busy minute can count twice. It says how often the limit bit, where Limited
    // also says how hard a client kept retrying.
    public const string RateLimitedMinutes = "rateLimitedMinutes";

    /// <summary>True for the result columns the total adds up.</summary>
    public static bool IsCounted(string column) =>
        column is not (Limited or CapLimited or RateLimitedMinutes or Total);
}
