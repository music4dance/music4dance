using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace m4dApi;

public class ResolveFunctions(SongResolver resolver, ILogger<ResolveFunctions> logger)
{
    // AuthorizationLevel.Function (the platform's function key) keeps the endpoint closed until
    // API key validation replaces it in PR 3 of the plan
    [Function("Resolve")]
    public async Task<IActionResult> Resolve(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/songs/resolve")] HttpRequest request)
    {
        var cancellationToken = request.HttpContext.RequestAborted;

        ResolveRequest body;
        try
        {
            body = await request.ReadFromJsonAsync<ResolveRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new { error = "Request body is not valid JSON" });
        }
        catch (InvalidOperationException)
        {
            // ReadFromJsonAsync throws this when the content type isn't JSON
            return new BadRequestObjectResult(new { error = "Content-Type must be application/json" });
        }

        var error = SongResolver.Validate(body);
        if (error != null)
        {
            return new BadRequestObjectResult(new { error });
        }

        try
        {
            var result = await resolver.Resolve(body, cancellationToken);
            // Per-query Search timings are the load guard for the shared replica (see the plan)
            logger.LogInformation(
                "Resolved {Match} by {Method}: {Queries}", result.Match, result.Method,
                string.Join(", ", result.Queries.Select(q => $"{q.Kind} {q.Ms} ms {q.Results} results")));
            return new OkObjectResult(result);
        }
        catch (Azure.RequestFailedException ex)
        {
            logger.LogError(ex, "Search request failed with status {Status}", ex.Status);
            request.HttpContext.Response.Headers.RetryAfter = "5";
            return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
