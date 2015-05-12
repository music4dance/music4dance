using System.Linq;
using System.Web.Mvc;
using m4dModels;

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
        public ActionResult Home()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Credits()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public ActionResult Dances()
        {
            return RedirectPermanent("/dances");
        }

        [AllowAnonymous]
        public ActionResult SiteMap()
        {
            ThemeName = BlogTheme;
            var data = SongCounts.GetFlatSongCounts(Database).OrderBy(sc => sc.DanceName);
            return View(data);
        }

        [AllowAnonymous]
        public ActionResult Tempi(bool detailed = false, int style = 0, int meter = 1, int type = 0, int org = 0)
        {
            ThemeName = ToolTheme;
            ViewBag.paramDetailed = detailed;
            ViewBag.paramStyle = style;
            ViewBag.paramMeter = meter;
            ViewBag.paramType = type;
            ViewBag.paramOrg = org;
            ViewBag.DanceStyles = Dance.DanceLibrary.AllDanceGroups;
            HelpPage = "dance-tempi";
            return View(Dance.DanceLibrary);
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
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public ActionResult Counter(bool showMPM=true, bool showBPM=false, bool showEpsilon=true, int? numerator=null, decimal? tempo= null)
        {
            ThemeName = ToolTheme;
            ViewBag.paramShowMPM = showMPM;
            ViewBag.paramShowBPM = showBPM;
            ViewBag.ShowEpsilon = showEpsilon;

            if (numerator.HasValue && numerator != 0)
            {
                ViewBag.paramNumerator = numerator.Value;
            }

            if (tempo.HasValue)
            {
                ViewBag.paramTempo = tempo.Value;
            }

            HelpPage = "tempo-counter";
            return View();
        }

        [AllowAnonymous]
        public ActionResult CounterHelp()
        {
            return RedirectPermanent("/blog/music4dance-help/tempo-counter/");
        }
    }
}