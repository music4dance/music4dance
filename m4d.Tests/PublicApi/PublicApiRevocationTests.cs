using System.Net;

using m4d.PublicApi;

using Microsoft.Extensions.DependencyInjection;

using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.Tests.PublicApi;

[TestClass]
public class PublicApiRevocationTests
{
    [TestMethod]
    public async Task ConnectedApps_DisconnectImmediatelyInvalidatesAccessAndRefresh()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync();
        using var page = await host.Browser.GetAsync(PublicApiTestHost.ConnectedAppsPath);
        StringAssert.Contains(await page.Content.ReadAsStringAsync(), "DanzQ");
        var form = await PublicApiTestHost.ReadFormAsync(page);
        using var revoked = await host.PostFormAsync(form);

        Assert.AreEqual(HttpStatusCode.Redirect, revoked.StatusCode);
        using var access = await host.ProtectedAsync(tokens.AccessToken);
        Assert.AreEqual(HttpStatusCode.Unauthorized, access.StatusCode);
        using var refresh = await host.RefreshAsync(tokens.RefreshToken);
        await PublicApiTokenTests.AssertErrorAsync(refresh, Errors.InvalidGrant);

        using var newConsent = await host.AuthorizeAsync(PublicApiTestHost.AuthorizationParameters());
        Assert.AreEqual(HttpStatusCode.OK, newConsent.StatusCode);
        await PublicApiTestHost.ReadFormAsync(newConsent);
    }

    [TestMethod]
    public async Task ConnectedApps_CannotRevokeAnotherUsersGrant()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var original = await host.IssueTokensAsync();
        using var firstPage = await host.Browser.GetAsync(PublicApiTestHost.ConnectedAppsPath);
        var firstForm = await PublicApiTestHost.ReadFormAsync(firstPage);
        var idField = firstForm.Fields.Keys.Single(key => key.Equals("authorizationId", StringComparison.OrdinalIgnoreCase));
        var target = firstForm.Fields[idField];
        await host.SignInAsync(PublicApiTestHost.OtherUserId);
        await host.IssueTokensAsync();
        using var otherPage = await host.Browser.GetAsync(PublicApiTestHost.ConnectedAppsPath);
        Assert.IsFalse((await otherPage.Content.ReadAsStringAsync()).Contains(target, StringComparison.Ordinal));
        var forged = await PublicApiTestHost.ReadFormAsync(otherPage);
        forged.Fields[idField] = target;
        using var response = await host.PostFormAsync(forged);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        using var access = await host.ProtectedAsync(original.AccessToken);
        Assert.AreEqual(HttpStatusCode.OK, access.StatusCode);
    }

    [TestMethod]
    public async Task ConnectedApps_RequiresAntiforgeryForDisconnect()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync();
        using var page = await host.Browser.GetAsync(PublicApiTestHost.ConnectedAppsPath);
        var form = await PublicApiTestHost.ReadFormAsync(page);
        form.Fields.Remove("__RequestVerificationToken");
        using var response = await host.PostFormAsync(form);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        using var access = await host.ProtectedAsync(tokens.AccessToken);
        Assert.AreEqual(HttpStatusCode.OK, access.StatusCode);
    }

    [TestMethod]
    public async Task ConnectedApps_RequiresWebsiteSignIn()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        using var response = await host.Browser.GetAsync(PublicApiTestHost.ConnectedAppsPath);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.IsNotNull(response.Headers.Location);
        StringAssert.Contains(response.Headers.Location.ToString(), "/Identity/Account/Login");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RevocationEndpoint_InvalidatesTheSpecifiedToken(bool refreshToken)
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync();
        using var revoked = await host.PostAsync("/connect/revocation", new Dictionary<string, string>
        {
            [Parameters.ClientId] = PublicApiDefaults.Clients.DanzQ,
            [Parameters.Token] = refreshToken ? tokens.RefreshToken : tokens.AccessToken,
            [Parameters.TokenTypeHint] = refreshToken ? TokenTypeHints.RefreshToken : TokenTypeHints.AccessToken
        });
        Assert.AreEqual(HttpStatusCode.OK, revoked.StatusCode);

        if (refreshToken)
        {
            using var refresh = await host.RefreshAsync(tokens.RefreshToken);
            await PublicApiTokenTests.AssertErrorAsync(refresh, Errors.InvalidGrant);
        }
        else
        {
            using var access = await host.ProtectedAsync(tokens.AccessToken);
            Assert.AreEqual(HttpStatusCode.Unauthorized, access.StatusCode);
        }
    }

    [TestMethod]
    public async Task RevocationEndpoint_DoesNotDiscloseUnknownTokens()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        using var response = await host.PostAsync("/connect/revocation", new Dictionary<string, string>
        {
            [Parameters.ClientId] = PublicApiDefaults.Clients.DanzQ,
            [Parameters.Token] = "unknown-token"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task RevocationEndpoint_CannotRevokeAnotherClientsToken()
    {
        await using var host = await PublicApiTestHost.StartAsync();
        await host.SignInAsync();
        var tokens = await host.IssueTokensAsync();
        await host.WithServicesAsync(async services =>
        {
            var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
            var descriptor = DanzQClient.CreateDescriptor();
            descriptor.ClientId = "other-native-client";
            await applications.CreateAsync(descriptor);
        });

        using var response = await host.PostAsync("/connect/revocation", new Dictionary<string, string>
        {
            [Parameters.ClientId] = "other-native-client",
            [Parameters.Token] = tokens.AccessToken
        });
        Assert.IsTrue(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest);
        using var access = await host.ProtectedAsync(tokens.AccessToken);
        Assert.AreEqual(HttpStatusCode.OK, access.StatusCode);
    }
}
