using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using m4d.Context;
using m4dModels;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity;

namespace m4d.Controllers
{
    [Authorize(Roles = "dbAdmin")]
    [RequireHttps]
    public class ApplicationUsersController : DMController
    {
        public override string DefaultTheme
        {
            get
            {
                return AdminTheme;
            }
        }

        private DanceMusicContext _db = new DanceMusicContext();

        // GET: ApplicationUsers
        public ActionResult Index()
        {
            ViewBag.RoleDictionary = _db.RoleDictionary;
            return View(_db.Users.ToList());
        }

        // GET: ApplicationUsers/Details/5
        public ActionResult Details(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ApplicationUser applicationUser = _db.Users.Find(id);
            if (applicationUser == null)
            {
                return HttpNotFound();
            }
            ViewBag.RoleDictionary = _db.RoleDictionary;
            return View(applicationUser);
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
        public ActionResult Create([Bind(Include = "Id,Email,EmailConfirmed,PasswordHash,SecurityStamp,PhoneNumber,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEndDateUtc,LockoutEnabled,AccessFailedCount,UserName")] ApplicationUser applicationUser)
        {
            if (ModelState.IsValid)
            {
                _db.Users.Add(applicationUser);
                _db.SaveChanges();
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
            ApplicationUser applicationUser = _db.Users.Find(id);
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
                _db.Entry(applicationUser).State = System.Data.Entity.EntityState.Modified;
                _db.SaveChanges();
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
            ApplicationUser applicationUser = _db.Users.Find(id);
            if (applicationUser == null)
            {
                return HttpNotFound();
            }
            ViewBag.RoleDictionary = _db.RoleDictionary;
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

            ApplicationUser user = _db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            List<string> newRoles = null;
            if (roles == null)
            {
                newRoles = new List<string>();
            }
            else
            {
                newRoles = new List<string>(roles);
            }

            var ustore = new UserStore<ApplicationUser>(_db);
            var umanager = new UserManager<ApplicationUser>(ustore);

            foreach (var role in _db.RoleDictionary.Values)
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

            ViewBag.RoleDictionary = _db.RoleDictionary;
            return View("Details", user);
        }


        // GET: ApplicationUsers/Delete/5
        public ActionResult Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ApplicationUser applicationUser = _db.Users.Find(id);
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
            ApplicationUser applicationUser = _db.Users.Find(id);
            _db.Users.Remove(applicationUser);
            _db.SaveChanges();
            return RedirectToAction("Index");
        }        

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
