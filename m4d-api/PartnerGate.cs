using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace m4dApi;

/// <summary>Limits for a client whose config secret leaves them out. Set by app settings.</summary>
public record PartnerDefaults(double RatePerSecond = 20, int Burst = 50, long DailyCap = 100_000);

/// <summary>
/// What every authenticated request passes before it reaches Search: the API key, then the
/// client's status, daily cap and rate limit. The order matters: an unknown key is rejected from
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

        var client = auth.Client;
        var dailyCap = client.DailyCap ?? defaults.DailyCap;
        if (usage.DailyTotal(auth.ClientId) >= dailyCap)
        {
            usage.Record(auth.ClientId, UsageMeter.Limited);
            var midnight = time.GetUtcNow().UtcDateTime.Date.AddDays(1);
            response.Headers.RetryAfter = Seconds(midnight - time.GetUtcNow().UtcDateTime);
            logger.LogWarning("Client {Client} reached its daily cap of {Cap}", auth.ClientId, dailyCap);
            return Reject(StatusCodes.Status429TooManyRequests, "Daily request cap reached", auth.ClientId);
        }

        if (!limiter.TryAcquire(auth.ClientId, client.RatePerSecond ?? defaults.RatePerSecond,
                client.Burst ?? defaults.Burst, out var retryAfter))
        {
            usage.Record(auth.ClientId, UsageMeter.Limited);
            response.Headers.RetryAfter = Seconds(retryAfter);
            return Reject(StatusCodes.Status429TooManyRequests, "Rate limit exceeded", auth.ClientId);
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
