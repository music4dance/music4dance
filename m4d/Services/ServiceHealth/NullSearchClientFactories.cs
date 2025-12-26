#nullable enable

using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Azure;

namespace m4d.Services.ServiceHealth;

/// <summary>
/// Null implementation of SearchClient factory for when Azure Search is unavailable
/// </summary>
public class NullSearchClientFactory : IAzureClientFactory<SearchClient>
{
    public SearchClient CreateClient(string name)
    {
        throw new InvalidOperationException(
            $"Azure Search service is unavailable. Cannot create SearchClient for '{name}'. " +
            "This is expected if search service configuration failed during startup. " +
            "Check service health status for details.");
    }
}

/// <summary>
/// Null implementation of SearchIndexClient factory for when Azure Search is unavailable
/// </summary>
public class NullSearchIndexClientFactory : IAzureClientFactory<SearchIndexClient>
{
    public SearchIndexClient CreateClient(string name)
    {
        throw new InvalidOperationException(
            $"Azure Search service is unavailable. Cannot create SearchIndexClient for '{name}'. " +
            "This is expected if search service configuration failed during startup. " +
            "Check service health status for details.");
    }
}
