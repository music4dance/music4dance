using m4d.PublicApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.Areas.Identity.Pages.Account.Manage;

[PublicApiEnabled]
[Authorize(Policy = PublicApiDefaults.BrowserPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ConnectedAppsModel(
    UserManager<ApplicationUser> users,
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictApplicationManager applications,
    IOpenIddictTokenManager tokens) : PageModel
{
    public List<Connection> Connections { get; } = [];
    public string UserName { get; private set; }

    [TempData]
    public string StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return NotFound();
        UserName = user.UserName;
        await foreach (var authorization in authorizations.FindBySubjectAsync(user.Id))
        {
            if (!await authorizations.HasStatusAsync(authorization, Statuses.Valid) ||
                !await authorizations.HasTypeAsync(authorization, AuthorizationTypes.Permanent)) continue;

            var applicationId = await authorizations.GetApplicationIdAsync(authorization);
            var application = await applications.FindByIdAsync(applicationId);
            if (application == null) continue;
            Connections.Add(new Connection(
                await authorizations.GetIdAsync(authorization),
                await applications.GetDisplayNameAsync(application),
                await authorizations.GetCreationDateAsync(authorization)));
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string authorizationId)
    {
        var user = await users.GetUserAsync(User);
        if (user == null || string.IsNullOrEmpty(authorizationId)) return NotFound();
        var authorization = await authorizations.FindByIdAsync(authorizationId);
        if (authorization == null || await authorizations.GetSubjectAsync(authorization) != user.Id)
        {
            return NotFound();
        }
        if (!await authorizations.TryRevokeAsync(authorization))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        await tokens.RevokeByAuthorizationIdAsync(authorizationId);
        StatusMessage = "The app has been disconnected. It can no longer access your account.";
        return RedirectToPage();
    }

    public sealed record Connection(string Id, string ApplicationName, DateTimeOffset? CreatedAt);
}
