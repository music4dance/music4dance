using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace m4dApi;

public class ResolveFunctions(SongResolver resolver, PartnerGate gate, UsageMeter usage, ILogger<ResolveFunctions> logger)
{
    // Anonymous at the platform level: PartnerGate checks the partner's API key, so the client is
    // known to the rate limit, the usage table and the logs. The platform's function keys carry
    // no client identity and are stored in plaintext.
    [Function("Resolve")]
    public async Task<IActionResult> Resolve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/songs/resolve")] HttpRequest request)
    {
        var cancellationToken = request.HttpContext.RequestAborted;

        var admission = await gate.Admit(request);
        if (!admission.Admitted)
        {
            return admission.Rejection;
        }

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
            usage.Record(admission.ClientId, result.Match);
            // Per-query Search timings are the load guard for the shared replica (see the plan)
            logger.LogInformation(
                "Resolved {Match} by {Method} for {Client}: {Queries}", result.Match, result.Method,
                admission.ClientId,
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
