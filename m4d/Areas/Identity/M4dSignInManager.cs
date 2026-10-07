using m4d.Configuration;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace m4d.Areas.Identity;

/// <summary>
/// Hides external login providers whose credentials aren't configured. They're always registered
/// (see AuthenticationBuilderExtensions) so that credentials arriving late, when App Configuration
/// recovers after a failed startup load, take effect without a restart; until then the login,
/// register and manage-logins pages must not offer them.
/// </summary>
public class M4dSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation,
    IConfiguration configuration)
    : SignInManager<ApplicationUser>(
        userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<IEnumerable<AuthenticationScheme>> GetExternalAuthenticationSchemesAsync()
    {
        var all = await base.GetExternalAuthenticationSchemesAsync();
        return all
            .Where(s => SecretBackedServices.ExternalLoginProviders.All(p => p.Scheme != s.Name)
                || SecretBackedServices.IsExternalLoginConfigured(configuration, s.Name))
            .ToList();
    }
}
