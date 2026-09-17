using System.Net;
using System.Text.Json;

using m4d.PublicApi;

using m4dModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.Tests.PublicApi;

[TestClass]
public class PublicApiTokenTests
{
    [TestMethod]
    public async Task Refresh_RotatesAndSlidesThirtyDayExpiry()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync();
        await AssertStoredLifetimeAsync(host, TokenTypeIdentifiers.RefreshToken, TimeSpan.FromDays(30));

        host.Clock.Advance(TimeSpan.FromDays(20));
        using var response = await host.RefreshAsync(tokens.RefreshToken);
        var renewed = await PublicApiTestHost.ReadTokensAsync(response);
        Assert.AreNotEqual(tokens.RefreshToken, renewed.RefreshToken);
        Assert.AreNotEqual(tokens.AccessToken, renewed.AccessToken);
        await AssertStoredLifetimeAsync(host, TokenTypeIdentifiers.RefreshToken, TimeSpan.FromDays(30));

        // Beyond the original token's lifetime, but inside the renewed token's lifetime.
        host.Clock.Advance(TimeSpan.FromDays(20));
        using var nextResponse = await host.RefreshAsync(renewed.RefreshToken);
        await PublicApiTestHost.ReadTokensAsync(nextResponse);
    }

    [TestMethod]
    public async Task Refresh_ExpiresAfterThirtyDaysWithoutUse()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync();
        host.Clock.Advance(TimeSpan.FromDays(31));
        using var response = await host.RefreshAsync(tokens.RefreshToken);

        await AssertErrorAsync(response, Errors.InvalidGrant);
    }

    [TestMethod]
    public async Task Refresh_ImmediateReplayRevokesGrantAndIssuedTokens()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var original = await host.IssueTokensAsync();
        using var firstRefresh = await host.RefreshAsync(original.RefreshToken);
        var renewed = await PublicApiTestHost.ReadTokensAsync(firstRefresh);
        using var replay = await host.RefreshAsync(original.RefreshToken);
        await AssertErrorAsync(replay, Errors.InvalidGrant);
        await AssertNoValidGrantAsync(host);

        using var access = await host.ProtectedAsync(renewed.AccessToken);
        Assert.AreEqual(HttpStatusCode.Unauthorized, access.StatusCode);
        using var refresh = await host.RefreshAsync(renewed.RefreshToken);
        await AssertErrorAsync(refresh, Errors.InvalidGrant);

        using var newConsent = await host.AuthorizeAsync(PublicApiTestHost.AuthorizationParameters());
        Assert.AreEqual(HttpStatusCode.OK, newConsent.StatusCode, "A compromised grant must require new consent.");
        var form = await PublicApiTestHost.ReadFormAsync(newConsent);
        form.Fields["decision"] = "accept";
        using var accepted = await host.PostFormAsync(form);
        using var recovered = await host.ExchangeAsync(PublicApiTestHost.CallbackValue(accepted, Parameters.Code));
        var fresh = await PublicApiTestHost.ReadTokensAsync(recovered);
        using var freshAccess = await host.ProtectedAsync(fresh.AccessToken);
        Assert.AreEqual(HttpStatusCode.OK, freshAccess.StatusCode);
    }

    [TestMethod]
    public async Task Code_IsOneTimeAndReplayInvalidatesItsGrant()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var code = await host.GrantCodeAsync();
        using var exchange = await host.ExchangeAsync(code);
        var tokens = await PublicApiTestHost.ReadTokensAsync(exchange);
        using var replay = await host.ExchangeAsync(code);

        await AssertErrorAsync(replay, Errors.InvalidGrant);
        await AssertNoValidGrantAsync(host);
        using var access = await host.ProtectedAsync(tokens.AccessToken);
        Assert.AreEqual(HttpStatusCode.Unauthorized, access.StatusCode);
    }

    [TestMethod]
    public async Task Code_ExpiresAfterOneMinute()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var code = await host.GrantCodeAsync();
        await AssertStoredLifetimeAsync(host, TokenTypeIdentifiers.Private.AuthorizationCode, TimeSpan.FromMinutes(1));
        host.Clock.Advance(TimeSpan.FromMinutes(2));
        using var response = await host.ExchangeAsync(code);

        await AssertErrorAsync(response, Errors.InvalidGrant);
    }

    [TestMethod]
    [DataRow("code_verifier", "wrong-but-valid-length-verifier-aaaaaaaaaaaaaaaaaaaaa", "invalid_grant")]
    [DataRow("code_verifier", "", "invalid_request")]
    [DataRow("redirect_uri", "com.example:/different-callback", "invalid_grant")]
    [DataRow("client_id", "unknown-client", "invalid_client")]
    public async Task Code_RejectsIncorrectProofOrBinding(string parameter, string value, string error)
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var code = await host.GrantCodeAsync();
        using var response = await host.ExchangeAsync(code, fields => fields[parameter] = value);

        await AssertErrorAsync(response, error);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Tokens_CannotBeRedeemedByAnotherRegisteredPublicClient(bool refresh)
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        await host.WithServicesAsync(async services =>
        {
            var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
            var descriptor = DanzQClient.CreateDescriptor();
            descriptor.ClientId = "other-native-client";
            await applications.CreateAsync(descriptor);
        });

        var value = refresh ? (await host.IssueTokensAsync()).RefreshToken : await host.GrantCodeAsync();
        using var response = refresh
            ? await host.RefreshAsync(value, fields => fields[Parameters.ClientId] = "other-native-client")
            : await host.ExchangeAsync(value, fields => fields[Parameters.ClientId] = "other-native-client");

        await AssertErrorAsync(response, Errors.InvalidGrant);
    }

    [TestMethod]
    public async Task Refresh_CannotExpandGrantedScopes()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync("account:read offline_access");
        using var response = await host.RefreshAsync(tokens.RefreshToken,
            fields => fields[Parameters.Scope] = PublicApiTestHost.DefaultScopes);

        await AssertErrorAsync(response, Errors.InvalidGrant);
    }

    [TestMethod]
    public async Task Refresh_NarrowedScopesStayNarrowedOnTheNextRefresh()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync();
        using var response = await host.RefreshAsync(tokens.RefreshToken,
            fields => fields[Parameters.Scope] = "account:read offline_access");
        var narrowed = await PublicApiTestHost.ReadTokensAsync(response);
        using var next = await host.RefreshAsync(narrowed.RefreshToken);
        var renewed = await PublicApiTestHost.ReadTokensAsync(next);
        using var access = await host.ProtectedAsync(renewed.AccessToken);

        Assert.AreEqual(HttpStatusCode.OK, access.StatusCode);
        using var principal = JsonDocument.Parse(await access.Content.ReadAsStringAsync());
        CollectionAssert.AreEquivalent(new[] { "account:read", "offline_access" },
            principal.RootElement.GetProperty("scopes").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    [TestMethod]
    public async Task WithoutOfflineAccess_NoRefreshTokenIsIssued()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync("account:read songs:read");

        Assert.AreEqual(string.Empty, tokens.RefreshToken);
        using var access = await host.ProtectedAsync(tokens.AccessToken);
        Assert.AreEqual(HttpStatusCode.OK, access.StatusCode);
    }

    [TestMethod]
    [DataRow("locked", false)]
    [DataRow("locked", true)]
    [DataRow("unconfirmed", false)]
    [DataRow("unconfirmed", true)]
    [DataRow("deleted", false)]
    [DataRow("deleted", true)]
    [DataRow("stamp-changed", false)]
    [DataRow("stamp-changed", true)]
    public async Task InvalidatedAccount_CannotReceiveNewTokens(string reason, bool refresh)
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var value = refresh ? (await host.IssueTokensAsync()).RefreshToken : await host.GrantCodeAsync();
        if (reason == "deleted")
        {
            await host.WithServicesAsync(async services =>
            {
                var users = services.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByIdAsync(PublicApiTestHost.UserId);
                Assert.IsNotNull(user);
                Assert.IsTrue((await users.DeleteAsync(user)).Succeeded);
            });
        }
        else
        {
            await host.ChangeUserAsync(user =>
            {
                switch (reason)
                {
                    case "locked": user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1); break;
                    case "unconfirmed": user.EmailConfirmed = false; break;
                    case "stamp-changed": user.SecurityStamp = Guid.NewGuid().ToString(); break;
                }
            });
        }

        using var response = refresh ? await host.RefreshAsync(value) : await host.ExchangeAsync(value);
        await AssertErrorAsync(response, Errors.InvalidGrant);
    }

    [TestMethod]
    public async Task AccessToken_ExpiresAfterOneHour()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync();
        host.Clock.Advance(TimeSpan.FromHours(2));
        using var access = await host.ProtectedAsync(tokens.AccessToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, access.StatusCode);
    }

    [TestMethod]
    public async Task BearerEndpoint_RejectsSiteCookieAndNonHeaderTokens()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync();
        using var cookieOnly = await host.ProtectedAsync();
        Assert.AreEqual(HttpStatusCode.Unauthorized, cookieOnly.StatusCode);
        using var query = await host.Browser.GetAsync("/test/protected?access_token=" + Uri.EscapeDataString(tokens.AccessToken));
        Assert.AreEqual(HttpStatusCode.Unauthorized, query.StatusCode);
        using var body = await host.PostAsync("/test/protected", new Dictionary<string, string>
        {
            [Parameters.AccessToken] = tokens.AccessToken
        });
        Assert.AreEqual(HttpStatusCode.Unauthorized, body.StatusCode);
        using var bearer = await host.ProtectedAsync(tokens.AccessToken);
        Assert.AreEqual(HttpStatusCode.OK, bearer.StatusCode);
    }

    [TestMethod]
    public async Task TokenRateLimit_ReturnsRetryAfterWithoutBlockingTheSite()
    {
        await using var host = await PublicApiTestHost.StartAsync(tokenLimit: 2);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await host.ExchangeAsync("invalid-code");
            await AssertErrorAsync(response, Errors.InvalidGrant);
        }

        using var limited = await host.ExchangeAsync("invalid-code");
        Assert.AreEqual(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.IsNotNull(limited.Headers.RetryAfter);
        Assert.AreEqual("site", await host.Browser.GetStringAsync("/"));
    }

    internal static async Task AssertErrorAsync(HttpResponseMessage response, string error)
    {
        Assert.AreEqual(error == Errors.InvalidClient ? HttpStatusCode.Unauthorized : HttpStatusCode.BadRequest,
            response.StatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual(error, document.RootElement.GetProperty(Parameters.Error).GetString(), document.RootElement.ToString());
        foreach (var field in new[] { Parameters.AccessToken, Parameters.RefreshToken, Parameters.IdToken })
        {
            Assert.IsFalse(document.RootElement.TryGetProperty(field, out _));
        }
    }

    private static Task AssertStoredLifetimeAsync(PublicApiTestHost host, string type, TimeSpan lifetime) =>
        host.WithServicesAsync(async services =>
        {
            var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
            var count = 0;
            await foreach (var token in tokens.FindBySubjectAsync(PublicApiTestHost.UserId))
            {
                if (await tokens.GetTypeAsync(token) != type || await tokens.GetStatusAsync(token) != Statuses.Valid)
                {
                    continue;
                }

                Assert.AreEqual(lifetime, await tokens.GetExpirationDateAsync(token) - await tokens.GetCreationDateAsync(token));
                count++;
            }

            Assert.AreEqual(1, count);
        });

    private static Task AssertNoValidGrantAsync(PublicApiTestHost host) => host.WithServicesAsync(async services =>
    {
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
        var count = 0;
        await foreach (var authorization in authorizations.FindBySubjectAsync(PublicApiTestHost.UserId))
        {
            Assert.AreEqual(Statuses.Revoked, await authorizations.GetStatusAsync(authorization));
            var tokenCount = 0;
            await foreach (var token in tokens.FindByAuthorizationIdAsync((await authorizations.GetIdAsync(authorization))!))
            {
                Assert.AreEqual(Statuses.Revoked, await tokens.GetStatusAsync(token), await tokens.GetTypeAsync(token));
                tokenCount++;
            }
            Assert.IsTrue(tokenCount >= 3, "Expected the code, access token and refresh token to remain as revoked records.");
            count++;
        }

        Assert.AreEqual(1, count);
    });
}
