using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using m4dModels;
using Microsoft.AspNet.Identity;

namespace m4d.Controllers
{
    [Authorize(Roles = "dbAdmin")]
    //[RequireHttps]
    public class ApplicationUsersController : DMController
    {
        public override string DefaultTheme => AdminTheme;

        //public ApplicationUsersController()
        //{
        //    //TODO: For some reason lazy loading isn't working for the roles collection, so explicitly loading (for now)
        //    //Context.Users.AsQueryable().Include("Roles").Load();
        //}

        // GET: ApplicationUsers
        public ActionResult Index(bool showUnconfirmed = false)
        {
            //ViewBag.Roles = Context.Roles;
            ViewBag.ShowUnconfirmed = showUnconfirmed;
            return View("Index", UserManager);
        }

        // GET: ApplicationUsers/Details/5
        public ActionResult Details(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            //TODO: Figure out how to get user details (login & roles) down to view
            ViewBag.Roles = Context.Roles;
            return View(UserManager.FindById(id));
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
        public ActionResult Create([Bind(Include = "UserName,Email")] ApplicationUser applicationUser)
        {
            if (!ModelState.IsValid) return View(applicationUser);

            var user = Database.FindOrAddUser(applicationUser.UserName, DanceMusicService.PseudoRole);
            user.Email = applicationUser.Email;
            user.EmailConfirmed = true;
            user.Privacy = 255;
            Context.SaveChanges();
            return RedirectToAction("Index");
        }

        // GET: Users/Edit/5
        public ActionResult Edit(string id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var applicationUser = Context.Users.Find(id);
            if (applicationUser == null)
            {
                return HttpNotFound();
            }
            return View(applicationUser);
        }

        // POST: ApplicationUsers/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public ActionResult EditPost(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var applicationUser = Context.Users.Find(id);

            var oldUserName = applicationUser.UserName;
            var oldSubscriptionLevel = applicationUser.SubscriptionLevel;

            var fields = new[]
            {
                "UserName", "Email", "EmailConfirmed", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled",
                "LockoutEndDateUtc", "LockoutEnabled", "AccessFailedCount", "SubscriptionLevel", "SubscriptionStart", "SubscriptionEnd"
            };
            if (TryUpdateModel(applicationUser, string.Empty, fields))
            {
                try
                {
                    if (!string.Equals(oldUserName, applicationUser.UserName))
                    {
                        Database.ChangeUserName(oldUserName, applicationUser.UserName);
                    }

                    UpdateSubscriptionRole(applicationUser.Id, oldSubscriptionLevel, applicationUser.SubscriptionLevel);

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
        public ActionResult ChangeRoles(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var applicationUser = UserManager.FindById(id);
            if (applicationUser == null)
            {
                return HttpNotFound();
            }
            ViewBag.Roles = Context.Roles;
            return View(applicationUser);
        }

        // POST: ApplicationUsers/ChangeRoles/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeRoles(string id, string[] roles)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var user = UserManager.FindById(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            var newRoles = roles == null ? new List<string>() : new List<string>(roles);

            foreach (var role in Context.Roles.ToList())
            {
                // New Role
                if (newRoles.Contains(role.Name))
                {
                    if (user.Roles.All(iur => iur.RoleId != role.Id))
                    {
                        UserManager.AddToRole(user.Id, role.Name);
                    }
                }
                else 
                { 
                    if (user.Roles.Any(iur => iur.RoleId == role.Id))
                    {
                        UserManager.RemoveFromRole(user.Id, role.Name);
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
            //    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            //}

            //var user = UserManager.FindById(id);

            //ViewBag.Title = "AutoLike";
            //var count = Database.BatchUserLike(user, true);
            //ViewBag.Message = $"{count} songs liked.";

            //return View("Info");
        }

        // GET: ApplicationUsers/Delete/5
        public ActionResult Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var applicationUser = UserManager.FindById(id);
            if (applicationUser == null)
            {
                return HttpNotFound();
            }
            return View(applicationUser);
        }

        // GET: ApplicationUsers/Create
        public ActionResult ClearCache()
        {
            ApplicationUserManager.ClearUserCache();
            return Index();
        }

        // GET: ApplicationUsers
        public ActionResult VotingResults()
        {
            var records = Database.GetVotingRecords().OrderByDescending(r => r.Total).ToList();
            foreach (var record in records)
            {
                record.User = ApplicationUserManager.UserDictionary.GetValueOrDefault(record.UserId);
            }

            return View(records);
        }


        // POST: ApplicationUsers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(string id)
        {
            var applicationUser = UserManager.FindById(id);

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

        private void UpdateSubscriptionRole(string userId, SubscriptionLevel oldLevel, SubscriptionLevel newLevel)
        {
            switch (oldLevel)
            {
                case SubscriptionLevel.None:
                    switch (newLevel)
                    {
                        case SubscriptionLevel.None:
                            break;
                        case SubscriptionLevel.Trial:
                            UserManager.AddToRole(userId, DanceMusicService.TrialRole);
                            break;
                        default:
                            UserManager.AddToRole(userId, DanceMusicService.PremiumRole);
                            break;
                    }
                    break;
                case SubscriptionLevel.Trial:
                    switch (newLevel)
                    {
                        case SubscriptionLevel.None:
                            UserManager.RemoveFromRole(userId, DanceMusicService.TrialRole);
                            break;
                        case SubscriptionLevel.Trial:                            
                            break;
                        default:
                            UserManager.AddToRole(userId, DanceMusicService.PremiumRole);
                            UserManager.RemoveFromRole(userId, DanceMusicService.TrialRole);
                            break;
                    }
                    break;
                default:
                    switch (newLevel)
                    {
                        case SubscriptionLevel.None:
                            UserManager.RemoveFromRole(userId, DanceMusicService.PremiumRole);
                            break;
                        case SubscriptionLevel.Trial:
                            UserManager.AddToRole(userId, DanceMusicService.TrialRole);
                            UserManager.RemoveFromRole(userId, DanceMusicService.PremiumRole);
                            break;
                        default:
                            break;
                    }
                    break;

            }
        }
    }
}
