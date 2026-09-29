using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.PublicApi;

[PublicApiEnabled]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthorizationController(
    IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations,
    PublicApiUserService users) : Controller
{
    private const string ServerScheme = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;

    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null) return NotFound();

        var cookie = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var user = await users.FindBrowserUserAsync(cookie.Principal);
        if (user == null)
        {
            if (request.HasPromptValue(PromptValues.None)) return Reject(Errors.LoginRequired);
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + Request.QueryString
            }, IdentityConstants.ApplicationScheme);
        }

        var application = await applications.FindByClientIdAsync(request.ClientId);
        var applicationId = await applications.GetIdAsync(application);
        await foreach (var authorization in authorizations.FindAsync(
            subject: user.Id, client: applicationId, status: Statuses.Valid,
            type: AuthorizationTypes.Permanent, scopes: request.GetScopes()))
        {
            if (!request.HasPromptValue(PromptValues.Consent))
            {
                return IssueCode(user, request, await authorizations.GetIdAsync(authorization));
            }
        }

        if (request.HasPromptValue(PromptValues.None)) return Reject(Errors.ConsentRequired);

        return View(new AuthorizationViewModel(
            await applications.GetDisplayNameAsync(application), user.UserName,
            request.GetScopes().Select(DescribeScope).ToArray(),
            request.ClientId, Request.Query[Parameters.RequestUri].ToString()));
    }

    [HttpPost("~/connect/authorize")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Consent(string decision)
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null) return NotFound();

        var cookie = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var user = await users.FindBrowserUserAsync(cookie.Principal);
        if (user == null) return Reject(Errors.LoginRequired);
        if (decision == "deny") return Reject(Errors.AccessDenied);
        if (decision != "accept") return BadRequest();

        var application = await applications.FindByClientIdAsync(request.ClientId);
        var principal = users.CreatePrincipal(user, request.GetScopes());
        var authorization = await authorizations.CreateAsync(
            principal, user.Id, await applications.GetIdAsync(application),
            AuthorizationTypes.Permanent, principal.GetScopes());
        principal.SetAuthorizationId(await authorizations.GetIdAsync(authorization));
        return SignIn(principal, ServerScheme);
    }

    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null) return NotFound();
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return Reject(Errors.UnsupportedGrantType);
        }

        var ticket = await HttpContext.AuthenticateAsync(ServerScheme);
        var user = await users.FindTokenUserAsync(ticket.Principal);
        if (user == null) return Reject(Errors.InvalidGrant);

        var grantedScopes = ticket.Principal.GetScopes();
        var scopes = string.IsNullOrEmpty(request.Scope) ? grantedScopes : request.GetScopes();
        if (scopes.Except(grantedScopes, StringComparer.Ordinal).Any())
        {
            return Reject(Errors.InvalidScope);
        }

        var principal = users.CreatePrincipal(user, scopes);
        principal.SetAuthorizationId(ticket.Principal.GetAuthorizationId());
        return SignIn(principal, ServerScheme);
    }

    private IActionResult IssueCode(ApplicationUser user, OpenIddictRequest request, string authorizationId)
    {
        var principal = users.CreatePrincipal(user, request.GetScopes());
        principal.SetAuthorizationId(authorizationId);
        return SignIn(principal, ServerScheme);
    }

    private ForbidResult Reject(string error) => Forbid(new AuthenticationProperties(
        new Dictionary<string, string>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error
        }), ServerScheme);

    private static string DescribeScope(string scope) => scope switch
    {
        PublicApiDefaults.Scopes.AccountRead => "See your username and subscription status",
        PublicApiDefaults.Scopes.SongsRead => "Look up songs and their dance recommendations",
        Scopes.OfflineAccess => "Stay connected without asking you to sign in each time",
        _ => throw new InvalidOperationException("An unsupported consent scope was requested.")
    };
}

public sealed record AuthorizationViewModel(
    string ApplicationName, string UserName, string[] Permissions, string ClientId, string RequestUri);
