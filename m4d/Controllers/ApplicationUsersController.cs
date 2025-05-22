using System.Net;

using m4d.Services;
using m4d.Utilities;

using m4dModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

[Authorize(Roles = "dbAdmin")]
//[RequireHttps]
public class ApplicationUsersController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
    IFeatureManager featureManager, ILogger<ActivityLogController> logger) : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backroundTaskQueue, featureManager, logger)
{


    //public ApplicationUsersController()
    //{
    //    //TODO: For some reason lazy loading isn't working for the roles collection, so explicitly loading (for now)
    //    //Context.Users.AsQueryable().Include("Roles").Load();
    //}

    // GET: ApplicationUsers
    public async Task<ActionResult> Index(bool showUnconfirmed = false, bool showPseudo = false,
        bool hidePrivate = false, string sort = "")
    {
        ViewBag.ShowUnconfirmed = showUnconfirmed;
        ViewBag.ShowPseudo = showPseudo;
        ViewBag.HidePrivate = hidePrivate;
        ViewBag.Sort = sort;
        return View("Index", await UserMapper.GetUserNameDictionary(Database.UserManager));
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
        return View(user);
    }

    public async Task<ActionResult> PremiumUsers()
    {
        return View(await UserMapper.GetPremiumUsers(UserManager));
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

        await Database.AddPseudoUser(applicationUser.UserName, applicationUser.Email);
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
            if ((await UserManager.FindByNameAsync(applicationUser.DecoratedName)) != null)
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

                await Context.SaveChangesAsync();
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
                    await UserManager.AddToRoleAsync(user, role.Name);
                }
            }
            else
            {
                if (await UserManager.IsInRoleAsync(user, role.Name))
                {
                    await UserManager.RemoveFromRoleAsync(user, role.Name);
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

    public async Task<ActionResult> ClearCache()
    {
        UserMapper.Clear();
        UsersController.ClearCache();
        AdmAuthentication.Clear();
        return await Index();
    }

    // GET: ApplicationUsers
    public async Task<ActionResult> VotingResults()
    {
        var records = (await SongIndex.GetVotingRecords()).OrderByDescending(r => r.Total)
            .ToList();
        foreach (var record in records)
        {
            var user =
                (await UserMapper.GetUserNameDictionary(Database.UserManager))
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

        var searches = Context.Searches.Where(s => s.ApplicationUserId == applicationUser.Id);
        foreach (var search in searches)
        {
            Context.Searches.Remove(search);
        }

        Context.Users.Remove(applicationUser);
        await Context.SaveChangesAsync();
        UserMapper.Clear();
        return RedirectToAction("Index");
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
                        await UserManager.AddToRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.TrialRole);
                        break;
                    default:
                        await UserManager.AddToRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.PremiumRole);
                        break;
                }

                break;
            case SubscriptionLevel.Trial:
                switch (newLevel)
                {
                    case SubscriptionLevel.None:
                        await UserManager.RemoveFromRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.TrialRole);
                        break;
                    case SubscriptionLevel.Trial:
                        break;
                    default:
                        await UserManager.AddToRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.PremiumRole);
                        await UserManager.RemoveFromRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.TrialRole);
                        break;
                }

                break;
            default:
                switch (newLevel)
                {
                    case SubscriptionLevel.None:
                        await UserManager.RemoveFromRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.PremiumRole);
                        break;
                    case SubscriptionLevel.Trial:
                        await UserManager.AddToRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.TrialRole);
                        await UserManager.RemoveFromRoleAsync(
                            applicationUser,
                            DanceMusicCoreService.PremiumRole);
                        break;
                }

                break;
        }
    }
}
