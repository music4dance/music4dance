using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using music4dance.ViewModels;
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

        public ActionResult About()
        {
            using (DanceMusicContext db = new DanceMusicContext())
            {
                db.Dances.Load();

                var data = new List<SongCounts>();

                HashSet<string> used = new HashSet<string>();

                // First handle dancegroups and types under dancegroups
                foreach (DanceGroup dg in Dances.Instance.AllDanceGroups)
                {
                    Dance d = db.Dances.FirstOrDefault(t => t.Id == dg.Id);

                    var scGroup = new SongCounts()
                        {
                            DanceId = dg.Id,
                            DanceName = dg.Name,
                            SongCount = d.Songs.Count,
                            Children = new List<SongCounts>()
                        };

                    data.Add(scGroup);

                    foreach (DanceObject dtypT in dg.Members)
                    {
                        DanceType dtyp = dtypT as DanceType;
                        Debug.Assert(dtyp != null);

                        HandleType(dtyp, db.Dances, scGroup);
                        used.Add(dtyp.Id);
                    }
                }

                // Then handle ungrouped types
                var scOther = new SongCounts()
                    {
                        DanceId = null,
                        DanceName = "Other",
                        SongCount = 0,
                        Children = new List<SongCounts>()
                    };
                data.Add(scOther);

                foreach (DanceType dt in Dances.Instance.AllDanceTypes)
                {
                    if (!used.Contains(dt.Id))
                    {
                        HandleType(dt, db.Dances, scOther);
                    }
                }

                return View(data);
            }
        }

        private void HandleType(DanceType dtyp, DbSet<Dance> dances, SongCounts scGroup)
        {
            Dance d = dances.FirstOrDefault(t => t.Id == dtyp.Id);

            var scType = new SongCounts()
            {
                DanceId = dtyp.Id,
                DanceName = dtyp.Name,
                SongCount = d.Songs.Count,
                Children = null
            };

            scGroup.Children.Add(scType);

            foreach (DanceObject dinst in dtyp.Instances)
            {
                d = dances.FirstOrDefault(t => t.Id == dinst.Id);
                int count = d.Songs.Count;

                if (count > 0)
                {
                    var scInstance = new SongCounts()
                    {
                        DanceId = dinst.Id,
                        DanceName = dinst.Name,
                        SongCount = count
                    };

                    if (scType.Children == null)
                        scType.Children = new List<SongCounts>();

                    scType.Children.Add(scInstance);
                    scType.SongCount += count;
                }
            }

            scGroup.SongCount += scType.SongCount;
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
