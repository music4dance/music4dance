using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace m4dApi;

public class ResolveFunctions(SongResolver resolver, ILogger<ResolveFunctions> logger)
{
    // Spike diagnostics: lets the caller tell a cold instance's first request from warm ones
    private static readonly DateTime s_processStartedUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();
    private static long s_requestCount;

    private static readonly SemaphoreSlim s_healthLock = new(1, 1);
    private static DateTime s_healthCheckedUtc = DateTime.MinValue;
    private static bool s_healthy;

    [Function("Resolve")]
    public async Task<IActionResult> Resolve(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/songs/resolve")] HttpRequest request)
    {
        var requestNumber = Interlocked.Increment(ref s_requestCount);
        var stopwatch = Stopwatch.StartNew();

        ResolveRequest body;
        try
        {
            body = await request.ReadFromJsonAsync<ResolveRequest>(request.HttpContext.RequestAborted);
        }
        catch (System.Text.Json.JsonException)
        {
            return new BadRequestObjectResult(new { error = "Request body is not valid JSON" });
        }

        var error = SongResolver.Validate(body);
        if (error != null)
        {
            return new BadRequestObjectResult(new { error });
        }

        try
        {
            var result = await resolver.Resolve(body, request.HttpContext.RequestAborted);
            return new OkObjectResult(new
            {
                result.Match,
                result.Method,
                result.Candidates,
                diagnostics = new
                {
                    instance = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName,
                    processStartedUtc = s_processStartedUtc,
                    requestNumber,
                    result.Queries,
                    totalMs = stopwatch.ElapsedMilliseconds,
                },
            });
        }
        catch (Azure.RequestFailedException ex)
        {
            logger.LogError(ex, "Search request failed with status {Status}", ex.Status);
            request.HttpContext.Response.Headers.RetryAfter = "5";
            return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [Function("Health")]
    public async Task<IActionResult> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequest request)
    {
        // Cache the Search check for 30 seconds so the health endpoint can't be used to load Search
        if (DateTime.UtcNow - s_healthCheckedUtc > TimeSpan.FromSeconds(30))
        {
            await s_healthLock.WaitAsync(request.HttpContext.RequestAborted);
            try
            {
                if (DateTime.UtcNow - s_healthCheckedUtc > TimeSpan.FromSeconds(30))
                {
                    try
                    {
                        s_healthy = await resolver.Ping(request.HttpContext.RequestAborted);
                    }
                    catch (Azure.RequestFailedException ex)
                    {
                        logger.LogWarning(ex, "Health check could not reach Search");
                        s_healthy = false;
                    }
                    s_healthCheckedUtc = DateTime.UtcNow;
                }
            }
            finally
            {
                s_healthLock.Release();
            }
        }

        return s_healthy
            ? new OkObjectResult(new { status = "ok" })
            : new ObjectResult(new { status = "search unavailable" }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
    }
}
