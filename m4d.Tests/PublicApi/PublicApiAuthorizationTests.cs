using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using m4d.PublicApi;
using m4d.Security;
using m4d.Tests.Identity;

using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.Tests.PublicApi;

[TestClass]
public class PublicApiAuthorizationTests
{
    [TestMethod]
    public async Task Login_CachedReturnUrlPreservesOriginalStateThroughExistingLogin()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        var parameters = PublicApiTestHost.AuthorizationParameters();
        using var response = await host.AuthorizeAsync(parameters);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.IsNotNull(response.Headers.Location);
        Assert.AreEqual("/Identity/Account/Login", response.Headers.Location.AbsolutePath);
        var returnUrl = QueryHelpers.ParseQuery(response.Headers.Location.Query)["ReturnUrl"].ToString();
        Assert.IsTrue(returnUrl.StartsWith("/connect/authorize?", StringComparison.Ordinal));
        var returnQuery = QueryHelpers.ParseQuery(new Uri(host.Browser.BaseAddress!, returnUrl).Query);
        Assert.IsTrue(returnQuery.ContainsKey(Parameters.RequestUri));
        Assert.IsFalse(returnQuery.ContainsKey(Parameters.State));

        var login = new TestableLoginModel(Mock.Of<IUrlHelperFactory>(),
            NullLogger<TestableLoginModel>.Instance, new AuthenticationTracker());
        var cleanedReturnUrl = login.PublicCleanUrl(returnUrl);
        Assert.AreEqual(returnUrl, cleanedReturnUrl);
        await host.SignInAsync();
        using var consent = await host.Browser.GetAsync(cleanedReturnUrl);
        var form = await PublicApiTestHost.ReadFormAsync(consent);
        form.Fields["decision"] = "accept";
        using var accepted = await host.PostFormAsync(form);

        Assert.IsFalse(string.IsNullOrEmpty(PublicApiTestHost.CallbackValue(accepted, Parameters.Code)));
        Assert.AreEqual(parameters[Parameters.State], PublicApiTestHost.CallbackValue(accepted, Parameters.State));
    }

    [TestMethod]
    public async Task Consent_AcceptsOnlyAfterAnAntiforgeryProtectedPost()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var parameters = PublicApiTestHost.AuthorizationParameters();
        parameters["decision"] = "accept";
        using var page = await host.AuthorizeAsync(parameters);
        Assert.AreEqual(HttpStatusCode.OK, page.StatusCode, "A GET must never approve a new grant.");
        var html = await page.Content.ReadAsStringAsync();
        StringAssert.Contains(html, "DanzQ");
        var form = await PublicApiTestHost.ReadFormAsync(page);
        Assert.IsTrue(form.Fields.ContainsKey("__RequestVerificationToken"));

        form.Fields["decision"] = "accept";
        using var response = await host.PostFormAsync(form);
        var code = PublicApiTestHost.CallbackValue(response, Parameters.Code);
        using var exchanged = await host.ExchangeAsync(code);
        var tokens = await PublicApiTestHost.ReadTokensAsync(exchanged);
        Assert.IsFalse(string.IsNullOrEmpty(tokens.RefreshToken));
        Assert.IsFalse(tokens.AccessToken.Contains('.'), "The access token should be a reference, not a JWT.");

        using var protectedResponse = await host.ProtectedAsync(tokens.AccessToken);
        Assert.AreEqual(HttpStatusCode.OK, protectedResponse.StatusCode);
        using var principal = JsonDocument.Parse(await protectedResponse.Content.ReadAsStringAsync());
        Assert.AreEqual(PublicApiTestHost.UserId, principal.RootElement.GetProperty("subject").GetString());
        var claims = principal.RootElement.GetProperty("claims").EnumerateArray().Select(value => value.GetString()).ToArray();
        foreach (var forbidden in new[] { Claims.Name, Claims.Email, Claims.Role, "AspNet.Identity.SecurityStamp", "m4d:security_stamp", "subscription" })
        {
            CollectionAssert.DoesNotContain(claims, forbidden);
        }
    }

    [TestMethod]
    public async Task AuthorizationPages_SuppressAllPageTrackingEvenWhenEnabled()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.WithServicesAsync(services =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            foreach (var feature in new[] { "GoogleTagManager", "GoogleTags", "ClientSideUsageLogging" })
            {
                configuration[$"FeatureManagement:{feature}"] = "true";
            }
            return Task.CompletedTask;
        });
        await host.SignInAsync();
        using var consent = await host.AuthorizeAsync(PublicApiTestHost.AuthorizationParameters());
        using var connections = await host.Browser.GetAsync(PublicApiTestHost.ConnectedAppsPath);

        foreach (var response in new[] { consent, connections })
        {
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.IsFalse(html.Contains("googletagmanager.com", StringComparison.Ordinal));
            StringAssert.Contains(html, "useClientSideTracking: false");
            Assert.AreEqual("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.IsTrue(response.Headers.CacheControl?.NoStore == true);
        }
    }

    [TestMethod]
    public async Task Consent_DenialReturnsStateWithoutCreatingPermanentGrant()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var parameters = PublicApiTestHost.AuthorizationParameters();
        using var page = await host.AuthorizeAsync(parameters);
        var form = await PublicApiTestHost.ReadFormAsync(page);
        form.Fields["decision"] = "deny";
        using var denied = await host.PostFormAsync(form);

        Assert.AreEqual(Errors.AccessDenied, PublicApiTestHost.CallbackValue(denied, Parameters.Error));
        Assert.AreEqual(parameters[Parameters.State], PublicApiTestHost.CallbackValue(denied, Parameters.State));
        await host.WithServicesAsync(async services =>
        {
            var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
            Assert.AreEqual(0L, await authorizations.CountAsync());
        });
    }

    [TestMethod]
    [DataRow("accept", false)]
    [DataRow("deny", false)]
    [DataRow("accept", true)]
    public async Task Consent_RejectsMissingOrInvalidAntiforgeryToken(string decision, bool invalidToken)
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        using var page = await host.AuthorizeAsync(PublicApiTestHost.AuthorizationParameters());
        var form = await PublicApiTestHost.ReadFormAsync(page);
        form.Fields["decision"] = decision;
        if (invalidToken)
        {
            form.Fields["__RequestVerificationToken"] = "invalid-form-token";
        }
        else
        {
            form.Fields.Remove("__RequestVerificationToken");
        }

        using var response = await host.PostFormAsync(form);
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsNull(response.Headers.Location);
    }

    [TestMethod]
    public async Task Consent_ExistingGrantIsReusedButAdditionalScopesRequireConsent()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        await host.GrantCodeAsync("account:read");

        using var existing = await host.AuthorizeAsync(PublicApiTestHost.AuthorizationParameters("account:read"));
        Assert.IsFalse(string.IsNullOrEmpty(PublicApiTestHost.CallbackValue(existing, Parameters.Code)));
        using var expanded = await host.AuthorizeAsync(PublicApiTestHost.AuthorizationParameters());
        Assert.AreEqual(HttpStatusCode.OK, expanded.StatusCode);
        var form = await PublicApiTestHost.ReadFormAsync(expanded);
        Assert.IsTrue(form.Fields.ContainsKey("__RequestVerificationToken"));
    }

    [TestMethod]
    public async Task Consent_CachedRequestCannotBeExpandedByEditingTheForm()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        using var page = await host.AuthorizeAsync(PublicApiTestHost.AuthorizationParameters("account:read"));
        var form = await PublicApiTestHost.ReadFormAsync(page);
        form.Fields["decision"] = "accept";
        form.Fields[Parameters.Scope] = PublicApiTestHost.DefaultScopes;
        using var response = await host.PostFormAsync(form);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return;
        }

        using var exchange = await host.ExchangeAsync(PublicApiTestHost.CallbackValue(response, Parameters.Code));
        var tokens = await PublicApiTestHost.ReadTokensAsync(exchange);
        Assert.AreEqual(string.Empty, tokens.RefreshToken);
        using var access = await host.ProtectedAsync(tokens.AccessToken);
        Assert.AreEqual(HttpStatusCode.OK, access.StatusCode);
        using var principal = JsonDocument.Parse(await access.Content.ReadAsStringAsync());
        CollectionAssert.AreEquivalent(new[] { "account:read" },
            principal.RootElement.GetProperty("scopes").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    [TestMethod]
    public async Task Consent_ExplicitPromptRequiresConsentEvenWithExistingGrant()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        await host.GrantCodeAsync();
        var parameters = PublicApiTestHost.AuthorizationParameters();
        parameters[Parameters.Prompt] = PromptValues.Consent;
        using var response = await host.AuthorizeAsync(parameters);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        await PublicApiTestHost.ReadFormAsync(response);
    }

    [TestMethod]
    [DataRow(false, "login_required")]
    [DataRow(true, "consent_required")]
    public async Task SilentAuthorization_ReportsRequiredInteraction(bool signedIn, string error)
    {
        await using var host = await PublicApiTestHost.StartAsync();
        if (signedIn)
        {
            await host.SignInAsync();
        }

        var parameters = PublicApiTestHost.AuthorizationParameters();
        parameters[Parameters.Prompt] = PromptValues.None;
        using var response = await host.AuthorizeAsync(parameters);
        Assert.AreEqual(error, PublicApiTestHost.CallbackValue(response, Parameters.Error));
    }

    [TestMethod]
    public async Task BearerToken_CannotReplaceWebsiteSignIn()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync();
        using var client = host.App.GetTestClient();
        client.BaseAddress = host.Browser.BaseAddress;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var parameters = new FormUrlEncodedContent(PublicApiTestHost.AuthorizationParameters());
        using var cached = await client.GetAsync("/connect/authorize?" + await parameters.ReadAsStringAsync());
        Assert.AreEqual(HttpStatusCode.Redirect, cached.StatusCode);
        Assert.IsNotNull(cached.Headers.Location);
        using var response = await client.GetAsync(cached.Headers.Location);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.IsNotNull(response.Headers.Location);
        StringAssert.Contains(response.Headers.Location.ToString(), "/Identity/Account/Login");
        using var connections = await client.GetAsync(PublicApiTestHost.ConnectedAppsPath);
        Assert.AreEqual(HttpStatusCode.Redirect, connections.StatusCode);
        Assert.IsNotNull(connections.Headers.Location);
        StringAssert.Contains(connections.Headers.Location.ToString(), "/Identity/Account/Login");
    }

    [TestMethod]
    [DataRow("redirect_uri", "https://attacker.example/callback")]
    [DataRow("client_id", "unknown-client")]
    public async Task Authorization_RejectsUntrustedCallbackOrClientWithoutRedirect(string parameter, string value)
    {
        await using var host = await PublicApiTestHost.StartAsync();
        var parameters = PublicApiTestHost.AuthorizationParameters();
        parameters[parameter] = value;
        using var response = await host.AuthorizeAsync(parameters);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsNull(response.Headers.Location);
    }

    [TestMethod]
    [DataRow("code_challenge", "", "invalid_request")]
    [DataRow("code_challenge_method", "plain", "invalid_request")]
    [DataRow("scope", "openid", "invalid_scope")]
    [DataRow("scope", "account:read votes:write", "invalid_scope")]
    [DataRow("state", "", "invalid_request")]
    public async Task Authorization_RejectsInvalidPkceOrScopes(string parameter, string value, string error)
    {
        await using var host = await PublicApiTestHost.StartAsync();
        var parameters = PublicApiTestHost.AuthorizationParameters();
        parameters[parameter] = value;
        using var response = await host.AuthorizeAsync(parameters);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), error);
        Assert.IsNull(response.Headers.Location);
    }

    [TestMethod]
    public async Task CachedAuthorization_ExpiresWithoutIssuingCode()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        using var login = await host.AuthorizeAsync(PublicApiTestHost.AuthorizationParameters());
        Assert.IsNotNull(login.Headers.Location);
        var returnUrl = QueryHelpers.ParseQuery(login.Headers.Location.Query)["ReturnUrl"].ToString();
        host.Clock.Advance(TimeSpan.FromMinutes(11));
        await host.SignInAsync();
        using var response = await host.Browser.GetAsync(returnUrl);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsNull(response.Headers.Location);
    }

    [TestMethod]
    public async Task Disabled_LeavesConsentAndConnectedAppsUnavailableWithRealRoutesMapped()
    {
        await using var host = await PublicApiTestHost.StartAsync(enabled: false);
        await host.SignInAsync();
        foreach (var path in new[] { "/connect/authorize", PublicApiTestHost.ConnectedAppsPath })
        {
            using var response = await host.Browser.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, path);
            using var posted = await host.PostAsync(path, new Dictionary<string, string> { ["decision"] = "accept" });
            Assert.IsTrue(posted.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest, path);
        }

        using var token = await host.ExchangeAsync("invalid-code");
        Assert.AreEqual(HttpStatusCode.NotFound, token.StatusCode);
        Assert.AreEqual("site", await host.Browser.GetStringAsync("/"));
    }
}
