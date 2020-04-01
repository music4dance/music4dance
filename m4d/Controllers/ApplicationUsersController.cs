using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.Controllers
{
    [Authorize(Roles = "dbAdmin")]
    //[RequireHttps]
    public class ApplicationUsersController : DanceMusicController
    {
        public override string DefaultTheme => AdminTheme;

        public ApplicationUsersController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        { }

        //public ApplicationUsersController()
        //{
        //    //TODO: For some reason lazy loading isn't working for the roles collection, so explicitly loading (for now)
        //    //Context.Users.AsQueryable().Include("Roles").Load();
        //}

        // GET: ApplicationUsers
        public async Task<ActionResult> Index(bool showUnconfirmed = false)
        {
            //ViewBag.Roles = Context.Roles;
            ViewBag.ShowUnconfirmed = showUnconfirmed;
            return View("Index", await GetUserDictionary(Database.UserManager));
        }

        // GET: ApplicationUsers/Details/5
        public async Task<ActionResult> Details(string id)
        {
            if (id == null)
            {
                return StatusCode((int)HttpStatusCode.BadRequest);
            }

            //TODO: Figure out how to get user details (login & roles) down to view
            ViewBag.Roles = Database.Context.Roles;
            
            return View(await UserManager.FindByIdAsync(id));
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
        public ActionResult Create([Bind("UserName,Email")] ApplicationUser applicationUser)
        {
            if (!ModelState.IsValid) return View(applicationUser);

            var user = Database.FindOrAddUser(applicationUser.UserName, DanceMusicCoreService.PseudoRole);
            user.Email = applicationUser.Email;
            user.EmailConfirmed = true;
            user.Privacy = 255;
            Context.SaveChanges();
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
                return StatusCode((int) HttpStatusCode.NotFound);
            }
            return View(applicationUser);
        }

        // POST: ApplicationUsers/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditPost(string id)
        {
            if (id == null)
            {
                return StatusCode((int)HttpStatusCode.BadRequest);
            }

            var applicationUser = Context.Users.Find(id);

            var oldUserName = applicationUser.UserName;
            var oldSubscriptionLevel = applicationUser.SubscriptionLevel;

            // TODO: Verify that this works
            //var fields = new[]
            //{
            //    "UserName", "Email", "EmailConfirmed", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled",
            //    "LockoutEndDateUtc", "LockoutEnabled", "AccessFailedCount", "SubscriptionLevel", "SubscriptionStart", "SubscriptionEnd"
            //};
            if (await TryUpdateModelAsync(applicationUser, string.Empty))
            {
                try
                {
                    if (!string.Equals(oldUserName, applicationUser.UserName))
                    {
                        Database.ChangeUserName(oldUserName, applicationUser.UserName);
                    }

                    await UpdateSubscriptionRole(applicationUser.Id, oldSubscriptionLevel, applicationUser.SubscriptionLevel);

                    Context.SaveChanges();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                    ModelState.AddModelError("", @"Unable to save changes. Try again, and if the problem persists, please <a href='https://music4dance.blog/feedback/'>report the issue</a>.");
                }
            }

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

            var newRoles = roles == null ? new List<string>() : new List<string>(roles);

            foreach (var role in Context.Roles.ToList())
            {
                // New Role
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
            return View("Details", user);
        }

        // GET: ApplicationUsers/Details/5
        public ActionResult AutoLike(string id)
        {
            // DBKill: Do we need this?
            return RestoreBatch();
            //if (id == null)
            //{
            //    return StatusCode((int)HttpStatusCode.BadRequest);
            //}

            //var user = UserManager.FindById(id);

            //ViewBag.Title = "AutoLike";
            //var count = Database.BatchUserLike(user, true);
            //ViewBag.Message = $"{count} songs liked.";

            //return View("Info");
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

        // GET: ApplicationUsers/Create
        public async Task<ActionResult> ClearCache()
        {
            ClearUserCache();
            return await Index();
        }

        // GET: ApplicationUsers
        public async Task<ActionResult> VotingResults()
        {
            var records = Database.GetVotingRecords().OrderByDescending(r => r.Total).ToList();
            foreach (var record in records)
            {
                record.User = (await GetUserDictionary(Database.UserManager)).GetValueOrDefault(record.UserId).User;
            }

            return View(records);
        }


        // POST: ApplicationUsers/Delete/5
        [HttpPost, ActionName("Delete")]
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
            Context.SaveChanges();
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

        private async Task UpdateSubscriptionRole(string userId, SubscriptionLevel oldLevel, SubscriptionLevel newLevel)
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
                            await UserManager.AddToRoleAsync(applicationUser, DanceMusicCoreService.TrialRole);
                            break;
                        default:
                            await UserManager.AddToRoleAsync(applicationUser, DanceMusicCoreService.PremiumRole);
                            break;
                    }
                    break;
                case SubscriptionLevel.Trial:
                    switch (newLevel)
                    {
                        case SubscriptionLevel.None:
                            await UserManager.RemoveFromRoleAsync(applicationUser, DanceMusicCoreService.TrialRole);
                            break;
                        case SubscriptionLevel.Trial:                            
                            break;
                        default:
                            await UserManager.AddToRoleAsync(applicationUser, DanceMusicCoreService.PremiumRole);
                            await UserManager.RemoveFromRoleAsync(applicationUser, DanceMusicCoreService.TrialRole);
                            break;
                    }
                    break;
                default:
                    switch (newLevel)
                    {
                        case SubscriptionLevel.None:
                            await UserManager.RemoveFromRoleAsync(applicationUser, DanceMusicCoreService.PremiumRole);
                            break;
                        case SubscriptionLevel.Trial:
                            await UserManager.AddToRoleAsync(applicationUser, DanceMusicCoreService.TrialRole);
                            await UserManager.RemoveFromRoleAsync(applicationUser, DanceMusicCoreService.PremiumRole);
                            break;
                        default:
                            break;
                    }
                    break;

            }
        }

        public static async Task<IReadOnlyDictionary<string, UserInfo>> GetUserDictionary(UserManager<ApplicationUser> userManager)
        {
            if (s_cachedUsers.Count == 0)
            {
                foreach (var user in userManager.Users)
                {
                    var roles = await userManager.GetRolesAsync(user);
                    var logins = await userManager.GetLoginsAsync(user);

                    var userInfo = new UserInfo
                    {
                        User = user,
                        Roles = roles.ToList(),
                        Logins = logins.Select(l => l.LoginProvider).ToList()
                    };

                    s_cachedUsers.Add(user.UserName, userInfo);
                }

                CacheTime = DateTime.Now;
            }

            return s_cachedUsers;
        }

        private static void ClearUserCache()
        {
            s_cachedUsers.Clear();
            CacheTime = DateTime.MinValue;
        }
        private static readonly Dictionary<string, UserInfo> s_cachedUsers = new Dictionary<string, UserInfo>(StringComparer.OrdinalIgnoreCase);

        private static DateTime CacheTime { get; set; }
    }
}
