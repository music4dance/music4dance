using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace m4dApi;

/// <summary>
/// What every authenticated request passes before it reaches Search: the API key, then the
/// client's status, daily cap and rate limit, and last the soft cap, which only warns. The order matters: an unknown key is rejected from
/// the in-memory cache before anything else, and a request over the cap doesn't spend a token.
/// </summary>
public class PartnerGate(
    PartnerKeyStore keys, ClientRateLimiter limiter, UsageMeter usage, PartnerDefaults defaults,
    TimeProvider time, ILogger<PartnerGate> logger)
{
    /// <summary>The client a request was admitted for, or the response that turns it away.</summary>
    public record Admission(string ClientId, IActionResult Rejection)
    {
        public bool Admitted => Rejection == null;
    }

    // How long a request on a cold instance waits for the first key load before getting 503
    public static readonly TimeSpan FirstLoadWait = TimeSpan.FromSeconds(10);

    public async Task<Admission> Admit(HttpRequest request)
    {
        var response = request.HttpContext.Response;

        // A key in the URL ends up in access logs and browser history; refuse it outright
        if (request.Query.Any(q => PartnerKeyFormat.LooksLikeKey(q.Key) || q.Value.Any(PartnerKeyFormat.LooksLikeKey)))
        {
            return Reject(StatusCodes.Status400BadRequest,
                "Send the API key in the Authorization header, never in the URL");
        }

        var key = BearerToken(request);
        if (key == null)
        {
            response.Headers.WWWAuthenticate = "Bearer";
            return Reject(StatusCodes.Status401Unauthorized, "Authorization: Bearer <API key> is required");
        }

        _ = await keys.WaitUntilLoaded(FirstLoadWait, request.HttpContext.RequestAborted);
        var auth = keys.Authenticate(key);
        switch (auth.Status)
        {
            case AuthStatus.Unavailable:
                // Only an instance that couldn't reach the vault since it started gets here
                response.Headers.RetryAfter = "30";
                return Reject(StatusCodes.Status503ServiceUnavailable, "Starting up; try again shortly");
            case AuthStatus.Unauthorized:
                response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
                return Reject(StatusCodes.Status401Unauthorized, "The API key is not valid");
            case AuthStatus.Forbidden:
                logger.LogWarning("Rejected a request from disabled client {Client}", auth.ClientId);
                return Reject(StatusCodes.Status403Forbidden, "This client is disabled", auth.ClientId);
        }

        // Each warning below is logged once per episode on an instance (a day for the caps, a
        // minute for the rate limit), not once per request: a client that ignores Retry-After
        // would otherwise write a log line for every refused call.
        var client = auth.Client;
        var dailyCap = client.DailyCap ?? defaults.DailyCap;
        var today = usage.DailyTotal(auth.ClientId);
        if (today >= dailyCap)
        {
            if (usage.RecordCapLimited(auth.ClientId))
            {
                logger.LogWarning("Client {Client} reached its daily cap of {Cap}", auth.ClientId, dailyCap);
            }

            var midnight = time.GetUtcNow().UtcDateTime.Date.AddDays(1);
            response.Headers.RetryAfter = Seconds(midnight - time.GetUtcNow().UtcDateTime);
            return Reject(StatusCodes.Status429TooManyRequests, "Daily request cap reached", auth.ClientId);
        }

        var rate = client.RatePerSecond ?? defaults.RatePerSecond;
        var burst = client.Burst ?? defaults.Burst;
        if (!limiter.TryAcquire(auth.ClientId, rate, burst, out var retryAfter))
        {
            if (usage.RecordRateLimited(auth.ClientId))
            {
                logger.LogWarning("Client {Client} hit its rate limit of {Rate}/s with burst {Burst}",
                    auth.ClientId, rate, burst);
            }

            response.Headers.RetryAfter = Seconds(retryAfter);
            return Reject(StatusCodes.Status429TooManyRequests, "Rate limit exceeded", auth.ClientId);
        }

        // The soft cap never refuses a request; it's the early warning, well before the hard cap
        var softCap = client.SoftCap ?? defaults.SoftCap;
        if (today >= softCap && usage.NoticeToday(auth.ClientId, "softCap"))
        {
            logger.LogWarning("Client {Client} passed its soft daily limit of {SoftCap} ({Total} calls so far)",
                auth.ClientId, softCap, today);
        }

        return new Admission(auth.ClientId, null);
    }

    private static string BearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        const string scheme = "Bearer ";
        return header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
            ? header[scheme.Length..].Trim()
            : null;
    }

    // Retry-After is whole seconds; round up so a client that honors it isn't refused again
    private static string Seconds(TimeSpan wait) => Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString();

    private static Admission Reject(int status, string error, string clientId = null) =>
        new(clientId, new ObjectResult(new { error }) { StatusCode = status });
}
