using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using m4d.ViewModels;
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

                HelpPage = "dance-styles";
                return View(data);
            }

            var category = DanceCategories.GetDanceCategories(Database).FromName(dance);

            if (category != null)
            {
                HelpPage = "dance-category";
                return View("category", category);
            }

            HelpPage = "dance-details";
            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            var sc = SongCounts.FromName(dance, Database);
            return sc != null ? View("details", sc) : ReturnError(HttpStatusCode.NotFound, string.Format("The dance with the name = {0} isn't defined.", dance));
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
            var dance = Database.Dances.Find(id);

            return dance != null ? View(dance) : ReturnError(HttpStatusCode.NotFound,string.Format("The dance with id = {0} isn't defined.",id));
        }

        //
        // POST: /Dances/Edit/5
        [HttpPost, ValidateInput(false)]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult Edit(Dance dance)
        {
            if (ModelState.IsValid && dance.Info != null)
            {
                if (dance.DanceLinks != null)
                {
                    foreach (var link in dance.DanceLinks)
                    {
                        link.DanceId = dance.Id.ToUpper();
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
                if (dance.SongTags == null)
                {
                    dance.SongTags = new TagSummary();
                }

                Context.Entry(dance).State = EntityState.Modified;
                Database.SaveChanges();

                SongCounts.ReloadDances(Database);

                return RedirectToAction("Index", new { dance = dance.Name });
            }

            var errors = ModelState.SelectMany(x => x.Value.Errors.Select(z => z.Exception));
            foreach (var error in errors.Where(error => error != null))
            {
                Trace.WriteLine(error.ToString());
            }

            return View(dance);
        }
    }
}