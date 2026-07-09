using System.Security.Claims;

using m4d.Utilities;

using m4dModels;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SelfCrawler;

/// <summary>
/// Manual/live tests for the Spotify refresh-token expiration recovery path
/// (see architecture/music-service-api-calls.md § Spotify Refresh-Token Expiration Handling).
///
/// These hit the real Spotify token endpoint (https://accounts.spotify.com/api/token) with
/// deliberately invalid credentials/refresh tokens. Spotify rejects the request the same way
/// it rejects a genuinely expired refresh token - with no access_token in the response body -
/// which is the only thing AdmAuthentication.CreateToken checks for, so no real Spotify app
/// credentials or user tokens are needed to exercise this path. Run manually via the
/// "Server: Test SelfCrawler" task; never run as part of "Server: Test" or CI.
/// </summary>
[TestClass]
public class SpotifyAuthRecoveryTests
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        // AdmAuthentication's static Logger field needs ApplicationLogging.LoggerFactory,
        // which m4d/Program.cs normally sets during app startup. SelfCrawler never runs that
        // startup, so wire up a no-op factory before touching any AdmAuthentication member.
        ApplicationLogging.LoggerFactory = LoggerFactory.Create(_ => { });
    }

    private static IConfiguration FakeSpotifyConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Spotify:ClientId"] = "self-crawler-test-client-id",
                ["Authentication:Spotify:ClientSecret"] = "self-crawler-test-client-secret"
            })
            .Build();
    }

    private static AuthenticateResult BuildAuthResult(
        ClaimsPrincipal principal, string accessToken, string refreshToken, TimeSpan expiresIn)
    {
        var properties = new AuthenticationProperties();

        // StoreTokens (not UpdateTokenValue, which only replaces an *existing* entry and is a
        // silent no-op otherwise) seeds the token list the way the real Spotify OAuth handler's
        // SaveTokens does when a user first signs in.
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = accessToken },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken },
            new AuthenticationToken
            {
                Name = "expires_at", Value = DateTime.UtcNow.Add(expiresIn).ToString("o")
            }
        ]);

        var ticket = new AuthenticationTicket(principal, properties, "Test");
        return AuthenticateResult.Success(ticket);
    }

    [TestMethod]
    public async Task RejectedRefreshToken_ThrowsSpotifyAuthExpiredException()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, $"selfcrawler-{Guid.NewGuid()}")], "Test"));

        // Already-expired access token forces TryCreate to attempt a refresh immediately.
        var authResult = BuildAuthResult(
            principal, "expired-access-token", "not-a-real-refresh-token",
            TimeSpan.FromHours(-1));

        await Assert.ThrowsExactlyAsync<SpotifyAuthExpiredException>(
            () => AdmAuthentication.TryCreate(
                FakeSpotifyConfiguration(), ServiceType.Spotify, authResult));
    }

    [TestMethod]
    public async Task AfterRejectedRefreshToken_ReconnectingWithFreshTokensSucceeds()
    {
        // This is the regression test for the cache-poisoning bug: before the fix, a failed
        // refresh got cached in AdmAuthentication's static per-user dictionary and was returned
        // unconditionally forever after, so a user could never recover by reconnecting Spotify
        // within the same app process. Same username both times; different tokens the second
        // time, simulating a real reconnect (fresh OAuth round-trip -> new cookie).
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, $"selfcrawler-{Guid.NewGuid()}")], "Test"));

        var badAuthResult = BuildAuthResult(
            principal, "expired-access-token", "not-a-real-refresh-token",
            TimeSpan.FromHours(-1));

        await Assert.ThrowsExactlyAsync<SpotifyAuthExpiredException>(
            () => AdmAuthentication.GetServiceAuthorization(
                FakeSpotifyConfiguration(), ServiceType.Spotify, principal, badAuthResult));

        var goodAuthResult = BuildAuthResult(
            principal, "fresh-access-token", "fresh-refresh-token", TimeSpan.FromHours(1));

        var authHeader = await AdmAuthentication.GetServiceAuthorization(
            FakeSpotifyConfiguration(), ServiceType.Spotify, principal, goodAuthResult);

        Assert.AreEqual("Bearer fresh-access-token", authHeader);
    }
}
