using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using SongDatabase.ViewModels;
using DanceLibrary;
using SongDatabase.Models;
using System.Diagnostics;

namespace music4dance.Controllers
{
    public class HomeController : Controller
    {
        [AllowAnonymous]
        public ActionResult Index()
        {
            ViewBag.Message = "Please feel free to explore this site, however it is currently in very early Alpha so any registration or data entered will be lost at the next upgrade.";

            return View();
        }

        [AllowAnonymous]
        public ActionResult About()
        {
            using (DanceMusicContext db = new DanceMusicContext())
            {
                var data = SongCounts.GetSongCounts(db);

                return View(data);
            }
        }

        [AllowAnonymous]
        public ActionResult Contact()
        {
            ViewBag.Message = "As noted on the homepage, this is an Alpha site, feel free to email with comments and suggestions.";

            return View();
        }

        //protected override void Dispose(bool disposing)
        //{
        //    _db.Dispose();
        //    base.Dispose(disposing);
        //}
    }
}
