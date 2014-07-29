using m4d.Context;
using m4d.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace m4d.Controllers
{
    public class HomeController : Controller
    {
        [AllowAnonymous]
        public ActionResult Index()
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
            return View();
        }

        [AllowAnonymous]
        public ActionResult TermsOfService()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult PrivacyPolicy()
        {
            return View();
        }


        [AllowAnonymous]
        public ActionResult Contact()
        {
            ViewBag.Message = ViewBag.Message = "As noted on the homepage, this is an Alpha site, feel free to email with comments and suggestions.";

            return View();
        }

        [AllowAnonymous]
        public ActionResult Counter()
        {

            return View();
        }

        [AllowAnonymous]
        public ActionResult CounterHelp()
        {

            return View();
        }
    }
}