using System.Security.Claims;

using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.PublicApi;

public sealed class PublicApiUserService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn)
{
    private const string SecurityStampClaim = "m4d:security_stamp";

    public async Task<ApplicationUser> FindBrowserUserAsync(ClaimsPrincipal principal)
    {
        var user = principal == null ? null : await signIn.ValidateSecurityStampAsync(principal);
        return await CanSignInAsync(user) ? user : null;
    }

    public async Task<ApplicationUser> FindTokenUserAsync(ClaimsPrincipal principal)
    {
        var subject = principal?.GetClaim(Claims.Subject);
        var user = string.IsNullOrEmpty(subject) ? null : await users.FindByIdAsync(subject);
        if (!await CanSignInAsync(user) ||
            !await signIn.ValidateSecurityStampAsync(user, principal.GetClaim(SecurityStampClaim)))
        {
            return null;
        }

        return user;
    }

    public ClaimsPrincipal CreatePrincipal(ApplicationUser user, IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType);
        identity.SetClaim(Claims.Subject, user.Id);
        identity.SetClaim(SecurityStampClaim, user.SecurityStamp);
        // The security stamp stays inside encrypted codes/refresh tokens, never access tokens.
        identity.SetDestinations(_ => []);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources(PublicApiDefaults.Resource);
        return principal;
    }

    private async Task<bool> CanSignInAsync(ApplicationUser user) =>
        user != null && await signIn.CanSignInAsync(user) && !await users.IsLockedOutAsync(user);
}
