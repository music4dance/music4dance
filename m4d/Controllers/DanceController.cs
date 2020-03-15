using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using DanceLibrary;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.Controllers
{
    public class DanceController : ContentController
    {
        public DanceController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager) :
            base(context, userManager, roleManager, searchService, danceStatsManager)
        { }

        public override string DefaultTheme => MusicTheme;

        // GET: Dances/{dance}
        [AllowAnonymous]
        public ActionResult Index(string dance)
        {
            var stats = DanceStatsManager.GetInstance(Database);
            if (string.IsNullOrWhiteSpace(dance))
            {
                HelpPage = "dance-styles";
                return View(stats);
            }

            ViewBag.DanceStats = stats;
            if (string.Equals(dance, "ballroom-competition-categories", StringComparison.OrdinalIgnoreCase))
            {
                return View("BallroomCompetitionCategories", CompetitionCategory.GetGroup(CompetitionCategory.Ballroom));
            }

            if (string.Equals(dance, "wedding-music", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Wedding dance help page?
                HelpPage = "dance-styles";
                return View("weddingdancemusic", stats.List);
            }

            if (string.Equals(dance, "holiday-music", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToActionPermanent("HolidayMusic", "Song");
            }

            var category = CompetitionCategory.GetCategory(dance);
            if (category != null)
            {
                HelpPage = "dance-category";

                return View("category", category);
            }

            HelpPage = "dance-details";

            var ds = stats.FromName(dance, User.Identity.Name);

            if (ds == null) return ReturnError(HttpStatusCode.NotFound, $"The dance with the name = {dance} isn't defined.");

            if (ds.SongCount == 0)
            {
                return View("emptydance", ds);
            }

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
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "canEdit")]
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
                            // CORETODO: Does this just work? Context.Entry(link).State = EntityState.Added;
                        }
                        else
                        {
                            // CORETODO: Does this just work? //Context.Entry(link).State = EntityState.Modified;
                        }
                    }
                }

                // CORETODO: Does this just work? Context.Entry(dance).State = EntityState.Modified;
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