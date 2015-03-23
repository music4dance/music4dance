using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using m4dModels;

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
                    return ReturnError(HttpStatusCode.NotFound, string.Format("The dance with the name = {0} isn't defined.", dance));
                }
            }
        }

        // GET: GroupRedirect/group/dance
        [AllowAnonymous]
        public ActionResult GroupRedirect(string group, string dance)
        {
            return RedirectToActionPermanent("Index", new {dance});
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
                return ReturnError(HttpStatusCode.NotFound,string.Format("The dance with id = {0} isn't defined.",id));
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
                            Context.Entry(link).State = EntityState.Added;
                        }
                        else
                        {
                            Context.Entry(link).State = EntityState.Modified;
                        }
                    }
                }
                Context.Entry(dance).State = EntityState.Modified;
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