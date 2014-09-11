using m4d.Context;
using m4d.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace m4d.Controllers
{
    public class HomeController : DMController
    {
        public override string DefaultTheme
        {
            get
            {
                return MusicTheme;
            }
        }

        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Dances()
        {
            using (DanceMusicContext db = new DanceMusicContext())
            {
                var data = SongCounts.GetSongCounts(db);

                return View(data);
            }
        }

        [AllowAnonymous]
        public ActionResult About()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public ActionResult TermsOfService()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public ActionResult PrivacyPolicy()
        {
            ThemeName = BlogTheme;
            return View();
        }


        [AllowAnonymous]
        public ActionResult Contact()
        {
            ViewBag.Message = ViewBag.Message = "As noted on the homepage, this is an Alpha site, feel free to email with comments and suggestions.";

            return View();
        }

        [AllowAnonymous]
        public ActionResult Counter(bool showMPM=true, bool showBPM=false, bool showEpsilon=true)
        {
            ThemeName = ToolTheme;
            ViewBag.paramShowMPM = showMPM;
            ViewBag.paramShowBPM = showBPM;
            ViewBag.ShowEpsilon = showEpsilon;

            return View();
        }

        [AllowAnonymous]
        public ActionResult CounterHelp()
        {
            ThemeName = ToolTheme;
            return View();
        }
    }
}