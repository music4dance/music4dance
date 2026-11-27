using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Search.Documents;

namespace m4dModels;

/// <summary>
/// Tags Search requests that page through an index (backups, reindex passes, exports, the
/// merge-candidate scan) so <c>SearchCircuitBreaker</c> leaves them on the client's network
/// timeout and doesn't open on their timeouts. Its 15 s query timeout is sized for page views,
/// where nothing took longer than 3.3 s outside Search's periodic stalls; a 1,000-document page
/// buffered under load can take longer, and an admin job against a test index on the same service
/// shouldn't take Search away from the whole site.
/// </summary>
public static class SearchBulkRead
{
    /// <summary>The <see cref="HttpMessage"/> property that marks a bulk read.</summary>
    public const string PropertyName = "m4d.SearchBulkRead";

    /// <summary>
    /// Queries asking for at least this many documents are bulk reads. Page views ask for 100 at
    /// most; the bulk paths use 250 (snapshot loads) or 1,000.
    /// </summary>
    public const int MinPageSize = 250;

    public static bool IsBulk(SearchOptions options) => (options?.Size ?? 0) >= MinPageSize;

    public static bool IsBulk(HttpMessage message) =>
        message.TryGetProperty(PropertyName, out var value) && value is true;

    /// <summary>
    /// Tags every Search request started inside the returned scope (it flows with the async
    /// context, so wrap the call itself, not an async iterator's consumer). Returns null when
    /// <paramref name="bulk"/> is false, which <c>using</c> accepts.
    /// </summary>
    public static IDisposable Scope(bool bulk = true) =>
        bulk
            ? HttpPipeline.CreateHttpMessagePropertiesScope(new Dictionary<string, object> { [PropertyName] = true })
            : null;
}
