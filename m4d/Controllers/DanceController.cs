using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using m4dModels;

namespace m4d.Controllers
{
    public class DanceController : ContentController
    {
        public override string DefaultTheme => MusicTheme;

        // GET: Dances/{dance}
        [AllowAnonymous]
        public ActionResult Index(string dance)
        {
            if (string.IsNullOrWhiteSpace(dance))
            {
                var data = DanceStatsManager.GetDanceStats(Database);

                HelpPage = "dance-styles";
                return View(data);
            }

            var categories = DanceStatsManager.GetDanceCategories(Database);
            if (categories != null)
            {
                HelpPage = "dance-category";
                var category = categories?.FromName(dance);

                if (category != null)
                {
                    return View("category", category);
                }

                if (string.Equals(dance, "ballroom-competition-categories", StringComparison.OrdinalIgnoreCase))
                {
                    return View("BallroomCompetitionCategories", categories.GetGroup(DanceCategories.Ballroom));
                }

                if (string.Equals(dance, "wedding-music", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: Wedding dance help page?
                    HelpPage = "dance-styles";
                    return View("weddingdancemusic", DanceStatsManager.GetDanceStats(Database));
                }
            }

            HelpPage = "dance-details";
            ViewBag.DanceMap = DanceStatsManager.GetDanceMap(Database);
            var ds = DanceStatsManager.FromName(dance, Database);

            if (ds == null) return ReturnError(HttpStatusCode.NotFound, $"The dance with the name = {dance} isn't defined.");

            SetupLikes(ds.TopSongs,ds.DanceId);

            return View("details", ds);
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

            return dance != null ? View(dance) : ReturnError(HttpStatusCode.NotFound,$"The dance with id = {id} isn't defined.");
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
                //TODO: This is a kludge - should figure out a better way to load this while preserving the case of the ID
                dance.Id = dance.Id.ToUpper();
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

                DanceStatsManager.ReloadDances(Database);

                return RedirectToAction("Index", new { dance = dance.Name });
            }

            var errors = ModelState.SelectMany(x => x.Value.Errors.Select(z => z.Exception));
            foreach (var error in errors.Where(error => error != null))
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError,error.ToString());
            }

            return View(dance);
        }
    }
}