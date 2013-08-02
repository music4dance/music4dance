using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SongDatabase.Models;
using PagedList;

namespace music4dance.Controllers
{
    public class SongController : Controller
    {
        private DanceMusicContext db = new DanceMusicContext();

        //
        // GET: /Song/

        public ActionResult Index(string sortOrder, string currentFilter, string searchString, int? page)
        {
            ViewBag.CurrentSort = sortOrder;

            ViewBag.TitleSort = String.IsNullOrEmpty(sortOrder) ? "Title_desc" : "";
            ViewBag.ArtistSort = sortOrder == "Artist" ? "Artist_desc" : "Artist";
            ViewBag.AlbumSort = sortOrder == "Album" ? "Album_desc" : "Album";

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
                    break;

                case "Artist":
                    songs = songs.OrderBy(s => s.Artist);
                    break;

                case "Artist_desc":
                    songs = songs.OrderByDescending(s => s.Artist);
                    break;

                case "Album":
                    songs = songs.OrderBy(s => s.Album);
                    break;

                case "Album_desc":
                    songs = songs.OrderByDescending(s => s.Album);
                    break;

                default:
                    songs = songs.OrderBy(s => s.Title);
                    break;

            }

            int pageSize = 25;
            int pageNumber = (page ?? 1);

            return View(songs.ToPagedList(pageNumber, pageSize));
        }

        //
        // GET: /Song/Details/5

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

        public ActionResult Create()
        {
            return View();
        }

        //
        // POST: /Song/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
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
        public ActionResult Edit(Song song)
        {
            if (ModelState.IsValid)
            {
                db.Entry(song).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(song);
        }

        //
        // GET: /Song/Delete/5

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