using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace m4dApi;

public class HealthFunctions(SearchClient client, ILogger<HealthFunctions> logger)
{
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromSeconds(30);
    private static readonly SemaphoreSlim s_lock = new(1, 1);
    private static DateTime s_checkedUtc = DateTime.MinValue;
    private static bool s_healthy;

    [Function("Health")]
    public async Task<IActionResult> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequest request)
    {
        // Cache the Search check so the unauthenticated endpoint can't be used to load Search
        if (DateTime.UtcNow - s_checkedUtc > s_cacheDuration)
        {
            await s_lock.WaitAsync(request.HttpContext.RequestAborted);
            try
            {
                if (DateTime.UtcNow - s_checkedUtc > s_cacheDuration)
                {
                    s_healthy = await PingSearch(request.HttpContext.RequestAborted);
                    s_checkedUtc = DateTime.UtcNow;
                }
            }
            finally
            {
                s_lock.Release();
            }
        }

        return s_healthy
            ? new OkObjectResult(new { status = "ok" })
            : new ObjectResult(new { status = "search unavailable" })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
    }

    private async Task<bool> PingSearch(CancellationToken cancellationToken)
    {
        try
        {
            var options = new SearchOptions { Size = 1, Select = { "SongId" } };
            var response = await client.SearchAsync<SearchDocument>("*", options, cancellationToken);
            return response.Value != null;
        }
        catch (Azure.RequestFailedException ex)
        {
            logger.LogWarning(ex, "Health check could not reach Search");
            return false;
        }
    }
}
