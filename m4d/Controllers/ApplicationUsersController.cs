using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.Utilities;
using m4d.ViewModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

using System.Net;

namespace m4d.Controllers;

[Authorize(Roles = "dbAdmin")]
//[RequireHttps]
public class ApplicationUsersController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
    IFeatureManagerSnapshot featureManager, ILogger<ActivityLogController> logger,
    ServiceHealthManager serviceHealth) : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backroundTaskQueue, featureManager, logger, serviceHealth)
{
    private const int UnconfirmedRetentionDays = 90;


    //public ApplicationUsersController()
    //{
    //    //TODO: For some reason lazy loading isn't working for the roles collection, so explicitly loading (for now)
    //    //Context.Users.AsQueryable().Include("Roles").Load();
    //}

    // GET: ApplicationUsers
    public async Task<ActionResult> Index()
    {
        var dict = await UserMapper.GetUserNameDictionary(Database.UserManager, ServiceHealth);
        var roles = Context.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToList();
        var services = MusicService.GetServices()
            .Select(s => new AdminServiceInfo { Cid = s.CID.ToString(), Name = s.Name })
            .ToList();

        var users = dict.Values.Select(u => new AdminUserSummary
        {
            Id                 = u.User.Id,
            UserName           = u.User.UserName,
            Email              = u.User.Email,
            EmailConfirmed     = u.User.EmailConfirmed,
            IsPseudo           = u.IsPseudo,
            StartDate          = u.User.StartDate,
            LastActive         = u.User.LastActive,
            HitCount           = u.User.HitCount,
            LifetimePurchased  = u.User.LifetimePurchased,
            SubscriptionLevel  = (int)u.User.SubscriptionLevel,
            Privacy            = u.User.Privacy,
            CanContact         = (int)u.User.CanContact,
            ServicePreference  = u.User.ServicePreference,
            FailedCardAttempts = u.User.FailedCardAttempts,
            Roles              = u.Roles,
            Logins             = u.Logins,
        }).ToList();

        var model = new AdminUsersModel { Users = users, AllRoles = roles, Services = services };
        return Vue3("User Administrator", "Admin: User list", "admin-users", model);
    }

    // GET: ApplicationUsers/Details/5
    public ActionResult Details(string id)
    {
        if (id == null)
        {
            return StatusCode((int)HttpStatusCode.BadRequest);
        }

        var user = Database.Context.Users.Where(u => u.Id == id)
            .Include(u => u.ActivityLog).Include(u => u.Searches).FirstOrDefault();

        if (user == null)
        {
            return StatusCode((int)HttpStatusCode.NotFound);
        }

        return View(user);
    }

    // GET: ApplicationUsers/Create
    public ActionResult Create()
    {
        return View();
    }

    // POST: Users/Create
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> Create(
        [Bind("UserName,Email")] ApplicationUser applicationUser)
    {
        if (!ModelState.IsValid)
        {
            return View(applicationUser);
        }

        _ = await Database.AddPseudoUser(applicationUser.UserName, applicationUser.Email);
        return RedirectToAction("Index");
    }

    // GET: Users/Edit/5
    public ActionResult Edit(string id)
    {
        if (id == null)
        {
            return StatusCode((int)HttpStatusCode.BadRequest);
        }

        var applicationUser = Context.Users.Find(id);
        if (applicationUser == null)
        {
            return StatusCode((int)HttpStatusCode.NotFound);
        }

        return View(applicationUser);
    }

    // POST: ApplicationUsers/Edit/5
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ActionName("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> EditPost(string id)
    {
        if (id == null)
        {
            return StatusCode((int)HttpStatusCode.BadRequest);
        }

        var applicationUser = await Context.Users.FindAsync(id);

        var oldUserName = applicationUser.DecoratedName;
        var oldSubscriptionLevel = applicationUser.SubscriptionLevel;

        // TODO: Verify that this works
        //var fields = new[]
        //{
        //    "UserName", "Email", "EmailConfirmed", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled",
        //    "LockoutEndDateUtc", "LockoutEnabled", "AccessFailedCount", "SubscriptionLevel", "SubscriptionStart", "SubscriptionEnd"
        //};

        if (await TryUpdateModelAsync(applicationUser, string.Empty))
        {
            if (!string.Equals(applicationUser.DecoratedName, oldUserName, StringComparison.OrdinalIgnoreCase) &&
                (await UserManager.FindByNameAsync(applicationUser.DecoratedName)) != null)
            {
                ModelState.AddModelError(
                    "UserName",
                    $"{applicationUser.DecoratedName} is already used, please try another.");
                return View(applicationUser);
            }

            try
            {
                if (!string.Equals(oldUserName, applicationUser.DecoratedName))
                {
                    applicationUser.NormalizedUserName = applicationUser.UserName.ToUpper();
                    await Database.ChangeUserName(oldUserName, applicationUser.DecoratedName);
                }

                await UpdateSubscriptionRole(
                    applicationUser.Id, oldSubscriptionLevel,
                    applicationUser.SubscriptionLevel);

                _ = await Context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                ModelState.AddModelError(
                    "",
                    @"Unable to save changes. Try again, and if the problem persists, please <a href='https://music4dance.blog/feedback/'>report the issue</a>.");
            }
        }

        UserMapper.Clear();

        return View(applicationUser);
    }

    // GET: ApplicationUsers/Merge
    public ActionResult Merge(string mergeName = null)
    {
        ViewBag.MergeName = mergeName;
        return View();
    }

    // GET: ApplicationUsers/MergeConfirm?keepName=x&mergeName=y
    public async Task<ActionResult> MergeConfirm(string keepName, string mergeName)
    {
        var keepUser = string.IsNullOrWhiteSpace(keepName) ? null : await Database.FindUser(keepName);
        var mergeUser = string.IsNullOrWhiteSpace(mergeName) ? null : await Database.FindUser(mergeName);

        if (keepUser == null || mergeUser == null)
        {
            ModelState.AddModelError("", "Both usernames must match an existing user.");
            return View("Merge");
        }

        if (keepUser.Id == mergeUser.Id)
        {
            ModelState.AddModelError("", "Please choose two different users.");
            return View("Merge");
        }

        if (!keepUser.IsPseudo || !mergeUser.IsPseudo)
        {
            ModelState.AddModelError("", "Merge is only supported for pseudo (service) users.");
            return View("Merge");
        }

        return View(new MergeUsersModel { KeepUser = keepUser, MergeUser = mergeUser });
    }

    // POST: ApplicationUsers/MergeConfirm
    [HttpPost]
    [ActionName("MergeConfirm")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> MergeConfirmed(string keepId, string mergeId)
    {
        try
        {
            await Database.MergeUsers(keepId, mergeId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to merge user {MergeId} into {KeepId}", mergeId, keepId);
            ModelState.AddModelError(
                "",
                $"Unable to merge users: {ex.Message}");
            return View("Merge");
        }

        UserMapper.Clear();
        return RedirectToAction("Index");
    }

    // GET: Users/ClearPremium/5
    public async Task<ActionResult> ClearPremium(string id)
    {
        if (id == null)
        {
            return StatusCode((int)HttpStatusCode.BadRequest);
        }

        var user = await UserManager.FindByIdAsync(id);
        if (user == null)
        {
            return StatusCode((int)HttpStatusCode.NotFound);
        }

        user.SubscriptionLevel = SubscriptionLevel.None;
        user.SubscriptionStart = null;
        user.SubscriptionEnd = null;

        if (Context.Roles.Any(r => r.Name == "premium"))
        {
            _ = await UserManager.RemoveFromRoleAsync(user, "premium");
        }
        _ = await Context.SaveChangesAsync();

        UserMapper.Clear();
        return View("Details", user);
    }

    // GET: Users/ChangeRoles/5
    public async Task<ActionResult> ChangeRoles(string id)
    {
        if (id == null)
        {
            return StatusCode((int)HttpStatusCode.BadRequest);
        }

        var applicationUser = await UserManager.FindByIdAsync(id);
        if (applicationUser == null)
        {
            return StatusCode((int)HttpStatusCode.NotFound);
        }

        ViewBag.Roles = Context.Roles;
        return View(applicationUser);
    }

    // POST: ApplicationUsers/ChangeRoles/5
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> ChangeRoles(string id, string[] roles)
    {
        if (id == null)
        {
            return StatusCode((int)HttpStatusCode.BadRequest);
        }

        var user = await UserManager.FindByIdAsync(id);
        if (user == null)
        {
            return StatusCode((int)HttpStatusCode.NotFound);
        }

        var newRoles = roles == null ? new List<string>() : [.. roles];

        foreach (var role in Context.Roles.ToList())
        // New Role
        {
            if (newRoles.Contains(role.Name))
            {
                if (!await UserManager.IsInRoleAsync(user, role.Name))
                {
                    _ = await UserManager.AddToRoleAsync(user, role.Name);
                }
            }
            else
            {
                if (await UserManager.IsInRoleAsync(user, role.Name))
                {
                    _ = await UserManager.RemoveFromRoleAsync(user, role.Name);
                }
            }
        }

        ViewBag.Roles = Context.Roles;
        UserMapper.Clear();
        return View("Details", user);
    }

    // GET: ApplicationUsers/Delete/5
    public async Task<ActionResult> Delete(string id)
    {
        if (id == null)
        {
            return StatusCode((int)HttpStatusCode.BadRequest);
        }

        var applicationUser = await UserManager.FindByIdAsync(id);
        if (applicationUser == null)
        {
            return StatusCode((int)HttpStatusCode.NotFound);
        }

        return View(applicationUser);
    }

    public ActionResult ClearCache()
    {
        UserMapper.Clear();
        UsersController.ClearCache();
        AdmAuthentication.Clear();
        return RedirectToAction("Index");
    }

    // GET: ApplicationUsers
    public async Task<ActionResult> VotingResults()
    {
        var records = (await SongIndex.GetVotingRecords()).OrderByDescending(r => r.Total)
            .ToList();
        foreach (var record in records)
        {
            var user =
                (await UserMapper.GetUserNameDictionary(Database.UserManager, ServiceHealth))
                .GetValueOrDefault(record.UserId);
            if (user != null)
            {
                record.User = user.User;
            }
        }

        return View(records);
    }


    // POST: ApplicationUsers/Delete/5
    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> DeleteConfirmed(string id)
    {
        var applicationUser = await UserManager.FindByIdAsync(id);

        if (applicationUser == null)
        {
            return StatusCode((int)HttpStatusCode.NotFound);
        }

        // Reassign all song contributions (votes, tags, edits) from the deleted user's name
        // to their GUID ID, so they appear as Anonymous rather than being lost.
        if (!ServiceHealth.IsServiceHealthy("SearchService"))
        {
            Logger.LogError("Cannot delete user {UserId}: Azure Search is unavailable. " +
                "Delete aborted to avoid losing song contributions.", id);
            return StatusCode((int)HttpStatusCode.ServiceUnavailable,
                "Azure Search is currently unavailable. User deletion requires the search index " +
                "to be reachable so that song contributions can be anonymized before the account " +
                "is removed. Please try again once the search service has recovered.");
        }

        try
        {
            await Database.ChangeUserName(applicationUser.DecoratedName, applicationUser.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to anonymize song contributions for user {UserId} ({UserName}) " +
                "before deletion. Delete aborted.", id, applicationUser.UserName);
            return StatusCode((int)HttpStatusCode.ServiceUnavailable,
                "Failed to anonymize song contributions. User deletion aborted to avoid data loss. " +
                "Please check the search service and try again.");
        }

        var searches = await Context.Searches.Where(s => s.ApplicationUserId == applicationUser.Id)
            .ToListAsync();
        Context.Searches.RemoveRange(searches);

        _ = Context.Users.Remove(applicationUser);
        _ = await Context.SaveChangesAsync();
        UserMapper.Clear();
        return RedirectToAction("Index");
    }

    // GET: ApplicationUsers/DeleteUnconfirmed
    public async Task<ActionResult> DeleteUnconfirmed()
    {
        var users = await UnconfirmedUsersOlderThanRetention().OrderBy(u => u.StartDate)
            .ToListAsync();
        return View(users);
    }

    // POST: ApplicationUsers/DeleteUnconfirmed
    // Only deletes users both shown on the confirm page (userIds) and still matching the
    // unconfirmed/retention rule at submit time - re-validated server-side in case a user's
    // state changed (e.g. confirmed their email) between the GET and this POST.
    [HttpPost]
    [ActionName("DeleteUnconfirmed")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> DeleteUnconfirmedConfirmed(List<string> userIds)
    {
        // Reassign all song contributions (votes, tags, edits) from each deleted user's name
        // to their GUID ID, so they appear as Anonymous rather than being lost.
        if (!ServiceHealth.IsServiceHealthy("SearchService"))
        {
            Logger.LogError("Cannot bulk-delete unconfirmed users: Azure Search is unavailable. " +
                "Delete aborted to avoid losing song contributions.");
            return StatusCode((int)HttpStatusCode.ServiceUnavailable,
                "Azure Search is currently unavailable. User deletion requires the search index " +
                "to be reachable so that song contributions can be anonymized before the accounts " +
                "are removed. Please try again once the search service has recovered.");
        }

        var users = await UnconfirmedUsersOlderThanRetention()
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        foreach (var user in users)
        {
            try
            {
                await Database.ChangeUserName(user.DecoratedName, user.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to anonymize song contributions for user {UserId} " +
                    "({UserName}) during bulk delete of unconfirmed users. Skipping this user.",
                    user.Id, user.UserName);
                continue;
            }

            var searches = await Context.Searches.Where(s => s.ApplicationUserId == user.Id)
                .ToListAsync();
            Context.Searches.RemoveRange(searches);

            _ = Context.Users.Remove(user);
        }

        _ = await Context.SaveChangesAsync();
        UserMapper.Clear();
        return RedirectToAction("Index");
    }

    // Pseudo/service users are always created with EmailConfirmed = true (see
    // DanceMusicService.FindOrAddUser), so this can never match one - no need to filter IsPseudo
    // separately.
    private IQueryable<ApplicationUser> UnconfirmedUsersOlderThanRetention()
    {
        var cutoff = DateTime.Now.AddDays(-UnconfirmedRetentionDays);
        return Context.Users.Where(u => !u.EmailConfirmed && u.StartDate < cutoff);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Context.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task UpdateSubscriptionRole(string userId, SubscriptionLevel oldLevel,
        SubscriptionLevel newLevel)
    {
        var applicationUser = await UserManager.FindByIdAsync(userId);

        switch (oldLevel)
        {
            case SubscriptionLevel.None:
                switch (newLevel)
                {
                    case SubscriptionLevel.None:
                        break;
                    case SubscriptionLevel.Trial:
                        _ = await UserManager.AddToRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.TrialRole);
                        break;
                    default:
                        _ = await UserManager.AddToRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.PremiumRole);
                        break;
                }

                break;
            case SubscriptionLevel.Trial:
                switch (newLevel)
                {
                    case SubscriptionLevel.None:
                        _ = await UserManager.RemoveFromRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.TrialRole);
                        break;
                    case SubscriptionLevel.Trial:
                        break;
                    default:
                        _ = await UserManager.AddToRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.PremiumRole);
                        _ = await UserManager.RemoveFromRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.TrialRole);
                        break;
                }

                break;
            default:
                switch (newLevel)
                {
                    case SubscriptionLevel.None:
                        _ = await UserManager.RemoveFromRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.PremiumRole);
                        break;
                    case SubscriptionLevel.Trial:
                        _ = await UserManager.AddToRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.TrialRole);
                        _ = await UserManager.RemoveFromRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.PremiumRole);
                        break;
                }

                break;
        }
    }
}
