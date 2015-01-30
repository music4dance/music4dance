using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using m4dModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace m4d.Controllers
{
    [Authorize(Roles = "dbAdmin")]
    //[RequireHttps]
    public class ApplicationUsersController : DMController
    {
        public override string DefaultTheme
        {
            get
            {
                return AdminTheme;
            }
        }

        //public ApplicationUsersController()
        //{
        //    //TODO: For some reason lazy loading isn't working for the roles collection, so explicitly loading (for now)
        //    //Context.Users.AsQueryable().Include("Roles").Load();
        //}

        // GET: ApplicationUsers
        public ActionResult Index()
        {
            //ViewBag.Roles = Context.Roles;
            return View(UserManager);
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
        public ActionResult Create([Bind(Include = "UserName")] ApplicationUser applicationUser)
        {
            if (ModelState.IsValid)
            {
                Database.FindOrAddUser(applicationUser.UserName, DanceMusicService.PseudoRole);
                Context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(applicationUser);
        }

        // GET: Users/Edit/5
        public ActionResult Edit(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ApplicationUser applicationUser = Context.Users.Find(id);
            if (applicationUser == null)
            {
                return HttpNotFound();
            }
            return View(applicationUser);
        }

        // POST: ApplicationUsers/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Email,EmailConfirmed,PasswordHash,SecurityStamp,PhoneNumber,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEndDateUtc,LockoutEnabled,AccessFailedCount,UserName")] ApplicationUser applicationUser)
        {
            if (ModelState.IsValid)
            {
                Context.Entry(applicationUser).State = EntityState.Modified;
                Context.SaveChanges();
                return RedirectToAction("Index");
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
            ApplicationUser applicationUser = Context.Users.Find(id);
            if (applicationUser == null)
            {
                return HttpNotFound();
            }
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

            ApplicationUser user = Context.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            List<string> newRoles;
            newRoles = roles == null ? new List<string>() : new List<string>(roles);

            var ustore = new UserStore<ApplicationUser>(Context);
            var umanager = new UserManager<ApplicationUser>(ustore);

            foreach (var role in Context.Roles)
            {
                // New Role
                if (newRoles.Contains(role.Name))
                {
                    if (!user.Roles.Any(iur => iur.RoleId == role.Id))
                    {
                        umanager.AddToRole(user.Id, role.Name);
                    }
                }
                else 
                { 
                    if (!user.Roles.Any(iur => iur.RoleId == role.Id))
                    {
                        umanager.RemoveFromRole(user.Id, role.Name);
                    }
                }
            }

            ViewBag.Roles = Context.Roles;
            return View("Details", user);
        }


        // GET: ApplicationUsers/Delete/5
        public ActionResult Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ApplicationUser applicationUser = Context.Users.Find(id);
            if (applicationUser == null)
            {
                return HttpNotFound();
            }
            return View(applicationUser);
        }

        // POST: ApplicationUsers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(string id)
        {
            ApplicationUser applicationUser = Context.Users.Find(id);
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
    }
}
