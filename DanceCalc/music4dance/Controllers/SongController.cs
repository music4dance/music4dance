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
using music4dance.ViewModels;

namespace music4dance.Controllers
{
    public class SongController : Controller
    {
        private DanceMusicContext _db = new DanceMusicContext();

        //
        // GET: /Song/

        [AllowAnonymous]
        public ActionResult Index(string dances, string sortOrder, string currentFilter, string searchString, int? page)
        {
            // Set up the viewbag
            ViewBag.CurrentSort = sortOrder;
            if (dances == "ALL")
                dances = null;
            ViewBag.CurrentDances = dances;

            ViewBag.TitleSort = String.IsNullOrEmpty(sortOrder) ? "Title_desc" : "";
            ViewBag.ArtistSort = sortOrder == "Artist" ? "Artist_desc" : "Artist";
            ViewBag.AlbumSort = sortOrder == "Album" ? "Album_desc" : "Album";

            ViewBag.TitleClass = string.Empty;
            ViewBag.ArtistClass = string.Empty;
            ViewBag.AlbumClass = string.Empty;

            // Set up search string
            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }
            ViewBag.CurrentFilter = searchString;

            IList<SongCounts> songCounts = SongCounts.GetFlatSongCounts(_db);
            var scq = songCounts.Select(s => new { s.DanceId, s.DanceNameAndCount });
            var scl = new SelectList(scq.AsEnumerable(), "DanceId", "DanceNameAndCount");
            ViewBag.Dances = scl;

            // Now setup the view
            // Start with all of the songs in the database
            var songs = from s in _db.Songs select s;

            // Now limit it down to the ones that are marked as a particular dance or dances
            if (!string.IsNullOrWhiteSpace(dances))
            {
                string[] danceList = Dances.Instance.ExpandDanceList(dances);

                songs = songs.Where(s => s.DanceRatings.Any(dr => danceList.Contains(dr.DanceId)));
            }

            // Now limit it by anything that has the serach string in the title, album or artist
            if (!String.IsNullOrEmpty(searchString))
            {
                songs = songs.Where(
                    s => s.Title.ToUpper().Contains(searchString.ToUpper()) || 
                    s.Album.ToUpper().Contains(searchString.ToUpper()) || 
                    s.Artist.ToUpper().Contains(searchString.ToUpper()));
            }

            // Now sort the list
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
            Song song = _db.Songs.Find(id);
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
                _db.Songs.Add(song);
                _db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(song);
        }

        //
        // GET: /Song/Edit/5
        [Authorize(Roles = "canEdit")] 
        public ActionResult Edit(int id = 0)
        {
            Song song = _db.Songs.Find(id);
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
#if DEBUG
                _db.Dump();
#endif

                string userName = User.Identity.Name;
                UserProfile user = _db.UserProfiles.FirstOrDefault(u => u.UserName == userName);
                _db.EditSong(user, song);

                return RedirectToAction("Index");
            }
            return View(song);
        }

        //
        // GET: /Song/Delete/5
        [Authorize(Roles = "canEdit")] 
        public ActionResult Delete(int id = 0)
        {
            Song song = _db.Songs.Find(id);
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
            Song song = _db.Songs.Find(id);
            _db.Songs.Remove(song);
            _db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            _db.Dispose();
            base.Dispose(disposing);
        }
    }
}