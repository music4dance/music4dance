using m4dModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DanceLibrary;
using System.Data;
using System.Diagnostics;

namespace m4d.Controllers
{
    public class DanceController : DMController
    {
        public override string DefaultTheme
        {
            get
            {
                return MusicTheme;
            }
        }

        // GET: Dances/{dance}
        [AllowAnonymous]
        public ActionResult Index(string dance)
        {
            if (string.IsNullOrWhiteSpace(dance))
            {
                var data = SongCounts.GetSongCounts(Database);

                return View(data);
            }
            else
            {
                ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
                SongCounts sc = SongCounts.FromName(dance, Database);
                if (sc != null)
                {
                    return View("Details", sc);
                }
                else
                {
                    return HttpNotFound();
                }
            }
        }

        // GET: GroupRedirect/group/dance
        [AllowAnonymous]
        public ActionResult GroupRedirect(string group, string dance)
        {
            return RedirectToActionPermanent("Index", new {dance=dance});
        }

        //
        // GET: /Dances/Edit/5
        [Authorize(Roles = "canEdit")]
        public ActionResult Edit(string id)
        {
            Dance dance = Database.Dances.Find(id);

            if (dance != null)
            {
                return View(dance);
            }
            else
            {
                return HttpNotFound();
            }
        }
        //
        // POST: /Dances/Edit/5
        [HttpPost, ValidateInput(false)]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult Edit(Dance dance)
        {
            if (ModelState.IsValid)
            {
                if (dance.DanceLinks != null)
                {
                    foreach (DanceLink link in dance.DanceLinks)
                    {
                        link.DanceId = dance.Id;
                        if (link.Id == Guid.Empty)
                        {
                            link.Id = Guid.NewGuid();
                            Context.Entry(link).State = System.Data.Entity.EntityState.Added;
                        }
                        else
                        {
                            Context.Entry(link).State = System.Data.Entity.EntityState.Modified;
                        }
                    }
                }
                Context.Entry(dance).State = System.Data.Entity.EntityState.Modified;
                Database.SaveChanges();

                SongCounts.ReloadDances(Database);

                return RedirectToAction("Index", new { dance = dance.Name });
            }
            else
            {
                var errors = ModelState.SelectMany(x => x.Value.Errors.Select(z => z.Exception));
                foreach (var error in errors)
                {
                    if (error != null)
                        Trace.WriteLine(error.ToString());
                }

                return View(dance);
            }
        }
    }
}