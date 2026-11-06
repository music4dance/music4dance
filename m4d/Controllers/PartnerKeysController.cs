using m4d.PublicApi;
using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.ViewModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

/// <summary>
/// Partner API keys for the standalone partner API (m4d-api): partner developers generate and
/// revoke their own client's keys, and admins manage clients, their limits and who can issue keys.
/// See architecture/plans/partner-api-standalone-host.md.
/// </summary>
[Authorize]
public class PartnerKeysController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backgroundTaskQueue,
    IFeatureManagerSnapshot featureManager, ILogger<PartnerKeysController> logger,
    ServiceHealthManager serviceHealth, PartnerKeyService keys)
    : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backgroundTaskQueue, featureManager, logger, serviceHealth)
{
    private const string AdminRole = "dbAdmin";
    private const string DeveloperOrAdmin = "partnerDeveloper,dbAdmin";

    //
    // GET: /PartnerKeys
    [Authorize(Roles = DeveloperOrAdmin)]
    public async Task<ActionResult> Index(string message = null, string error = null)
    {
        var user = await GetApplicationUser();
        return View(new PartnerKeysModel
        {
            Clients = await keys.ClientsForUser(user.Id),
            IsLive = keys.IsLive,
            Message = message,
            Error = error
        });
    }

    //
    // POST: /PartnerKeys/Generate
    // Answers with the key itself rather than redirecting, so the key is never stored anywhere to
    // survive a redirect. Reloading the page doesn't show it again.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = DeveloperOrAdmin)]
    public async Task<ActionResult> Generate(string clientId, string label, bool fromAdmin = false)
    {
        if (!await CanManage(clientId))
        {
            return Forbid();
        }

        try
        {
            var issued = await keys.Issue(clientId, label, UserName);
            logger.LogInformation("{User} issued partner key {KeyId} for {Client}", UserName, issued.KeyId, clientId);
            Response.Headers.CacheControl = "no-store";
            return View("Issued", new IssuedKeyModel
            {
                Key = issued,
                ReturnAction = fromAdmin ? nameof(Clients) : nameof(Index)
            });
        }
        catch (PartnerKeyException ex)
        {
            return Back(fromAdmin, error: ex.Message);
        }
    }

    //
    // POST: /PartnerKeys/Revoke
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = DeveloperOrAdmin)]
    public async Task<ActionResult> Revoke(string keyId, bool fromAdmin = false)
    {
        var key = await Database.Context.PartnerApiKeys.FindAsync(keyId);
        if (key == null || !await CanManage(key.ClientId))
        {
            return Forbid();
        }

        _ = await keys.Revoke(keyId, UserName);
        logger.LogInformation("{User} revoked partner key {KeyId} for {Client}", UserName, keyId, key.ClientId);
        return Back(fromAdmin, $"Revoked key {keyId}. The API stops accepting it within 5 minutes.");
    }

    //
    // GET: /PartnerKeys/Clients
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult> Clients(string message = null, string error = null, string manual = null)
    {
        return View(new PartnerClientsModel
        {
            Clients = await keys.AllClients(),
            VaultConnected = keys.VaultConnected,
            IsLive = keys.IsLive,
            Message = message,
            Error = error,
            ManualCommand = manual
        });
    }

    //
    // POST: /PartnerKeys/SaveClient
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult> SaveClient(string id, string name, double? ratePerSecond, int? burst,
        long? dailyCap, bool disabled = false, bool create = false)
    {
        try
        {
            var manual = await keys.SaveClient(id, name, ratePerSecond, burst, dailyCap, disabled, create);
            return RedirectToAction(nameof(Clients), new
            {
                message = create ? $"Added client {id}." : $"Saved client {id}.",
                manual
            });
        }
        catch (PartnerKeyException ex)
        {
            return RedirectToAction(nameof(Clients), new { error = ex.Message });
        }
    }

    //
    // POST: /PartnerKeys/AddDeveloper
    // Links an account to a client and grants it the partnerDeveloper role
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult> AddDeveloper(string clientId, string userName,
        [FromServices] RoleManager<IdentityRole> roleManager)
    {
        var user = string.IsNullOrWhiteSpace(userName)
            ? null
            : await UserManager.FindByNameAsync(userName.Trim()) ?? await UserManager.FindByEmailAsync(userName.Trim());
        if (user == null)
        {
            return RedirectToAction(nameof(Clients), new { error = $"No account named {userName}." });
        }

        try
        {
            await keys.AddDeveloper(clientId, user);
        }
        catch (PartnerKeyException ex)
        {
            return RedirectToAction(nameof(Clients), new { error = ex.Message });
        }

        // Only Development seeds roles (UserManagerHelpers.SeedRoles), so the first developer
        // added on test or production creates the role; AddToRoleAsync throws if it's missing.
        var role = DanceMusicCoreService.PartnerDeveloperRole;
        if (!await roleManager.RoleExistsAsync(role))
        {
            var created = await roleManager.CreateAsync(new IdentityRole { Name = role });
            if (!created.Succeeded && !await roleManager.RoleExistsAsync(role))
            {
                return RedirectToAction(nameof(Clients), new { error = $"Couldn't create the {role} role: {Errors(created)}" });
            }
        }

        if (!await UserManager.IsInRoleAsync(user, role))
        {
            var added = await UserManager.AddToRoleAsync(user, role);
            if (!added.Succeeded)
            {
                return RedirectToAction(nameof(Clients), new { error = $"Couldn't give {user.UserName} the {role} role: {Errors(added)}" });
            }
        }

        return RedirectToAction(nameof(Clients), new { message = $"{user.UserName} can now issue keys for {clientId}." });
    }

    //
    // POST: /PartnerKeys/RemoveDeveloper
    // Keys the account issued stay active; revoke them separately
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult> RemoveDeveloper(string clientId, string userId)
    {
        var user = await UserManager.FindByIdAsync(userId);
        if (user == null)
        {
            return RedirectToAction(nameof(Clients), new { error = "No such account." });
        }

        if (!await keys.RemoveDeveloper(clientId, userId) &&
            await UserManager.IsInRoleAsync(user, DanceMusicCoreService.PartnerDeveloperRole))
        {
            _ = await UserManager.RemoveFromRoleAsync(user, DanceMusicCoreService.PartnerDeveloperRole);
        }

        return RedirectToAction(nameof(Clients), new { message = $"{user.UserName} can no longer issue keys for {clientId}." });
    }

    private async Task<bool> CanManage(string clientId)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return false;
        }

        if (User.IsInRole(AdminRole))
        {
            return true;
        }

        var user = await GetApplicationUser();
        return user != null && await keys.IsDeveloper(clientId, user.Id);
    }

    private RedirectToActionResult Back(bool fromAdmin, string message = null, string error = null) =>
        RedirectToAction(fromAdmin ? nameof(Clients) : nameof(Index), new { message, error });

    private static string Errors(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));
}
