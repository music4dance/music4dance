using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SongDatabase.Models;
using PagedList;
using DanceLibrary;
using System.Diagnostics;

namespace music4dance.Controllers
{
    public class SongController : Controller
    {
        private DanceMusicContext db = new DanceMusicContext();

        //
        // GET: /Song/

        [AllowAnonymous]
        public ActionResult Index(string dances, string sortOrder, string currentFilter, string searchString, int? page)
        {
            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentDances = dances;

            ViewBag.TitleSort = String.IsNullOrEmpty(sortOrder) ? "Title_desc" : "";
            ViewBag.ArtistSort = sortOrder == "Artist" ? "Artist_desc" : "Artist";
            ViewBag.AlbumSort = sortOrder == "Album" ? "Album_desc" : "Album";

            ViewBag.TitleClass = string.Empty;
            ViewBag.ArtistClass = string.Empty;
            ViewBag.AlbumClass = string.Empty;

            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewBag.CurrentFilter = searchString;

            var songs = from s in db.Songs select s;

            if (!string.IsNullOrWhiteSpace(dances))
            {
                //string[] danceList = dances.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] danceList = Dances.Instance.ExpandDanceList(dances);

                songs = songs.Where(s => s.Dances.Any(d => danceList.Contains(d.Id)));
            }

            if (!String.IsNullOrEmpty(searchString))
            {
                songs = songs.Where(
                    s => s.Title.ToUpper().Contains(searchString.ToUpper()) || 
                    s.Album.ToUpper().Contains(searchString.ToUpper()) || 
                    s.Artist.ToUpper().Contains(searchString.ToUpper()));
            }

            switch (sortOrder)
            {
                case "Title_desc":
                    songs = songs.OrderByDescending(s => s.Title);
                    ViewBag.TitleClass = "class=desc";
                    break;

                case "Artist":
                    songs = songs.OrderBy(s => s.Artist);
                    ViewBag.ArtistClass = "class=asc";
                    break;

                case "Artist_desc":
                    songs = songs.OrderByDescending(s => s.Artist);
                    ViewBag.ArtistClass = "class=desc";
                    break;

                case "Album":
                    songs = songs.OrderBy(s => s.Album);
                    ViewBag.AlbumClass = "class=desc";
                    break;

                case "Album_desc":
                    songs = songs.OrderByDescending(s => s.Album);
                    ViewBag.AlbumClass = "class=asc";
                    break;

                default:
                    songs = songs.OrderBy(s => s.Title);
                    ViewBag.TitleClass = "class=asc";
                    break;

            }

            int pageSize = 25;
            int pageNumber = (page ?? 1);

            return View(songs.ToPagedList(pageNumber, pageSize));
        }

        //
        // GET: /Song/Details/5

        [AllowAnonymous]
        public ActionResult Details(int id = 0)
        {
            Song song = db.Songs.Find(id);
            if (song == null)
            {
                return HttpNotFound();
            }
            return View(song);
        }

        //
        // GET: /Song/Create
        [Authorize(Roles = "canEdit")] 
        public ActionResult Create()
        {
            return View();
        }

        //
        // POST: /Song/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        public ActionResult Create(Song song)
        {
            if (ModelState.IsValid)
            {
                db.Songs.Add(song);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(song);
        }

        //
        // GET: /Song/Edit/5
        [Authorize(Roles = "canEdit")] 
        public ActionResult Edit(int id = 0)
        {
            Song song = db.Songs.Find(id);
            if (song == null)
            {
                return HttpNotFound();
            }
            return View(song);
        }

        //
        // POST: /Song/Edit/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        public ActionResult Edit(Song song)
        {
            if (ModelState.IsValid)
            {
                //This seemed promising, but it appears that these property lists aren't updated when
                // we need them

                //System.Data.Entity.Infrastructure.DbPropertyValues oldValues = db.Entry(song).OriginalValues;
                //foreach (string name in oldValues.PropertyNames)
                //{
                //    Debug.WriteLine(string.Format("{0}={1}",name,oldValues[name]));
                //}

                // TODO: Get the user that is logged in stuffed into userProfile

                string userName = User.Identity.Name;
                UserProfile user = db.UserProfiles.FirstOrDefault(u => u.UserName == userName);
                db.EditSong(user, song);

                return RedirectToAction("Index");
            }
            return View(song);
        }

        //
        // GET: /Song/Delete/5
        [Authorize(Roles = "canEdit")] 
        public ActionResult Delete(int id = 0)
        {
            Song song = db.Songs.Find(id);
            if (song == null)
            {
                return HttpNotFound();
            }
            return View(song);
        }

        //
        // POST: /Song/Delete/5

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        public ActionResult DeleteConfirmed(int id)
        {
            Song song = db.Songs.Find(id);
            db.Songs.Remove(song);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }
    }
}