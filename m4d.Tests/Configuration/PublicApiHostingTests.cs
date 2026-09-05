using System.Net;
using System.Text.Json;

using m4d.PublicApi;
using m4d.Utilities;

using m4dModels;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.Tests.Configuration;

[TestClass]
public class PublicApiHostingTests
{
    [TestMethod]
    public async Task Disabled_LeavesProtocolAndApiPathsUnmapped()
    {
        await using var app = await StartApplication(enabled: false);
        using var client = CreateClient(app);

        foreach (var (method, path) in new[]
        {
            (HttpMethod.Get, "/.well-known/openid-configuration"),
            (HttpMethod.Get, "/.well-known/oauth-authorization-server"),
            (HttpMethod.Get, "/.well-known/jwks"),
            (HttpMethod.Get, "/connect/authorize"),
            (HttpMethod.Post, "/connect/token"),
            (HttpMethod.Post, "/connect/revocation"),
            (HttpMethod.Get, "/v1/me"),
            (HttpMethod.Post, "/v1/songs/resolve")
        })
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await client.SendAsync(request);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, path);
        }

        Assert.AreEqual("site", await client.GetStringAsync("/"));
    }

    [TestMethod]
    public async Task Enabled_DiscoveryAdvertisesTheSubscriberContract()
    {
        await using var app = await StartApplication(enabled: true);
        using var client = CreateClient(app);
        using var response = await client.GetAsync("/.well-known/openid-configuration");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var metadata = document.RootElement;

        Assert.AreEqual("https://m4d.test/", metadata.GetProperty("issuer").GetString());
        Assert.AreEqual("https://m4d.test/connect/authorize", metadata.GetProperty("authorization_endpoint").GetString());
        Assert.AreEqual("https://m4d.test/connect/token", metadata.GetProperty("token_endpoint").GetString());
        Assert.AreEqual("https://m4d.test/connect/revocation", metadata.GetProperty("revocation_endpoint").GetString());
        Assert.AreEqual("https://m4d.test/.well-known/jwks", metadata.GetProperty("jwks_uri").GetString());
        CollectionAssert.AreEquivalent(
            new[] { "account:read", "songs:read", "offline_access" },
            metadata.GetProperty("scopes_supported").EnumerateArray().Select(value => value.GetString()).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "S256" },
            metadata.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(value => value.GetString()).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "authorization_code", "refresh_token" },
            metadata.GetProperty("grant_types_supported").EnumerateArray().Select(value => value.GetString()).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { "code" },
            metadata.GetProperty("response_types_supported").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    [TestMethod]
    public async Task Enabled_JwksPublishesOnlyPublicSigningMaterial()
    {
        await using var app = await StartApplication(enabled: true);
        using var client = CreateClient(app);
        using var response = await client.GetAsync("/.well-known/jwks");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var keys = document.RootElement.GetProperty("keys").EnumerateArray().ToArray();

        Assert.IsNotEmpty(keys);
        foreach (var key in keys)
        {
            Assert.AreEqual("sig", key.GetProperty("use").GetString());
            Assert.IsFalse(string.IsNullOrEmpty(key.GetProperty("kid").GetString()));
            foreach (var field in new[] { "d", "p", "q", "dp", "dq", "qi", "oth", "k" })
            {
                Assert.IsFalse(key.TryGetProperty(field, out _), $"JWKS must not expose {field}.");
            }
        }
    }

    [TestMethod]
    [DataRow("GET", "/connect/authorize")]
    [DataRow("POST", "/connect/token")]
    [DataRow("POST", "/connect/revocation")]
    public async Task MissingParameters_ReturnProtocolError(string method, string path)
    {
        await using var app = await StartApplication(enabled: true);
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (request.Method == HttpMethod.Post)
        {
            request.Content = new FormUrlEncodedContent([]);
        }

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), Errors.InvalidRequest);
    }

    [TestMethod]
    [DataRow("authorization_code", "invalid_grant")]
    [DataRow("refresh_token", "invalid_grant")]
    [DataRow("client_credentials", "unsupported_grant_type")]
    public async Task TokenRequest_WithoutValidGrant_CannotIssueTokens(string grantType, string expectedError)
    {
        await using var app = await StartApplication(enabled: true);
        using var client = CreateClient(app);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = PublicApiDefaults.Clients.DanzQ,
            ["grant_type"] = grantType,
            ["code"] = "invalid-test-code",
            ["code_verifier"] = new('a', 43),
            ["redirect_uri"] = PublicApiDefaults.Clients.DanzQRedirectUri,
            ["refresh_token"] = "invalid-test-refresh-token"
        });
        using var response = await client.PostAsync("/connect/token", content);
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(expectedError, document.RootElement.GetProperty("error").GetString());
        foreach (var field in new[] { "access_token", "refresh_token", "id_token" })
        {
            Assert.IsFalse(document.RootElement.TryGetProperty(field, out _));
        }
    }

    [TestMethod]
    public async Task Authorization_ValidRequest_DoesNotIssueACodeBeforePr2()
    {
        await using var app = await StartApplication(enabled: true);
        using var client = CreateClient(app);
        using var parameters = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = PublicApiDefaults.Clients.DanzQ,
            ["redirect_uri"] = PublicApiDefaults.Clients.DanzQRedirectUri,
            ["response_type"] = "code",
            ["scope"] = "account:read songs:read offline_access",
            ["code_challenge"] = new('a', 43),
            ["code_challenge_method"] = "S256",
            ["state"] = "test-state"
        });
        var uri = "/connect/authorize?" + await parameters.ReadAsStringAsync();

        using var response = await client.GetAsync(uri);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.IsNotNull(response.Headers.Location);
        var callback = response.Headers.Location;
        Assert.AreEqual(PublicApiDefaults.Clients.DanzQRedirectUri, callback.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(callback.Query);
        Assert.AreEqual(Errors.TemporarilyUnavailable, query["error"].ToString());
        Assert.AreEqual("test-state", query["state"].ToString());
        Assert.IsFalse(query.ContainsKey("code"));
    }

    [TestMethod]
    public async Task Enabled_RejectsInsecureHttp()
    {
        await using var app = await StartApplication(enabled: true);
        using var client = CreateClient(app);
        using var response = await client.GetAsync("http://m4d.test/.well-known/openid-configuration");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), Errors.InvalidRequest);
    }

    private static async Task<WebApplication> StartApplication(bool enabled)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Staging
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Configuration[$"FeatureManagement:{FeatureFlags.PublicApi}"] = enabled.ToString();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        builder.Services.AddAuthorization();
        var databaseName = $"public-api-http-{Guid.NewGuid()}";
        builder.Services.AddDbContext<DanceMusicContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services.AddPublicApiFoundation(builder.Configuration, builder.Environment);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/", () => "site");
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://m4d.test");
        return client;
    }
}
