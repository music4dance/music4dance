using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using music4dance.ViewModels;
using DanceLibrary;
using SongDatabase.Models;

namespace music4dance.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Message = "Modify this template to jump-start your ASP.NET MVC application.";

            return View();
        }

        public ActionResult About()
        {
            using (DanceMusicContext db = new DanceMusicContext())
            {
                db.Dances.Load();

                var data = new List<SongCounts>();

                foreach (Dance d in db.Dances.Local)
                {
                    int count = d.Songs.Count;
                    if (count > 0)
                    {
                        var sc = new SongCounts()
                            {
                                DanceId = d.Id,
                                DanceName = d.Info.Name,
                                SongCount = d.Songs.Count
                            };

                        data.Add(sc);
                    }
                }

                data = data.OrderBy(s => s.DanceName).ToList();

                return View(data);
            }

        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        //protected override void Dispose(bool disposing)
        //{
        //    _db.Dispose();
        //    base.Dispose(disposing);
        //}
    }
}
