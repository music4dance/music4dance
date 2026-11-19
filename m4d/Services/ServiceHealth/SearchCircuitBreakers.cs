namespace m4d.Services.ServiceHealth;

/// <summary>
/// One <see cref="SearchCircuitBreaker"/> per Azure AI Search service, so throttling on one
/// service (the song indexes on music4dance.search.windows.net) doesn't fail queries on another
/// (the pages index on m4d.search.windows.net). Each breaker is named for the
/// <see cref="ServiceHealthManager"/> service it marks and gates: see <see cref="AssignServiceNames"/>.
/// </summary>
public sealed class SearchCircuitBreakers(ServiceHealthManager serviceHealth, ILoggerFactory loggerFactory)
{
    /// <summary>The health name for a Search service that holds no song index (the pages index).</summary>
    public const string PageServiceName = "PageSearchService";

    private readonly Dictionary<string, SearchCircuitBreaker> _breakers = [];

    /// <summary>The breaker for a health service name, created on first use.</summary>
    public SearchCircuitBreaker For(string serviceName)
    {
        lock (_breakers)
        {
            if (!_breakers.TryGetValue(serviceName, out var breaker))
            {
                breaker = new SearchCircuitBreaker(serviceHealth,
                    loggerFactory.CreateLogger<SearchCircuitBreaker>(), serviceName);
                _breakers[serviceName] = breaker;
            }
            return breaker;
        }
    }

    public bool IsOpen(string serviceName)
    {
        lock (_breakers)
        {
            return _breakers.TryGetValue(serviceName, out var breaker) && breaker.IsOpen;
        }
    }

    /// <summary>
    /// The health service name for each index configuration section (key to endpoint), one name
    /// per endpoint host: <see cref="SearchCircuitBreaker.ServiceName"/> for a host with a
    /// <c>SongIndex*</c> section, <see cref="PageServiceName"/> for one with <c>PageIndex</c>, and
    /// <c>SearchService (host)</c> for any other.
    /// </summary>
    internal static Dictionary<string, string> AssignServiceNames(IEnumerable<(string Key, string Endpoint)> sections)
    {
        var list = sections.ToList();
        static string Host(string endpoint) =>
            Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : endpoint ?? "";

        var hostNames = list
            .GroupBy(s => Host(s.Endpoint))
            .ToDictionary(g => g.Key, g =>
                g.Any(s => s.Key.StartsWith("SongIndex", StringComparison.OrdinalIgnoreCase))
                    ? SearchCircuitBreaker.ServiceName
                    : g.Any(s => s.Key.Equals("PageIndex", StringComparison.OrdinalIgnoreCase))
                        ? PageServiceName
                        : $"{SearchCircuitBreaker.ServiceName} ({g.Key})");

        return list.ToDictionary(s => s.Key, s => hostNames[Host(s.Endpoint)]);
    }
}
