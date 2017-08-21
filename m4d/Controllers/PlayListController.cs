using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using m4dModels;
using EntityState = System.Data.Entity.EntityState;

namespace m4d.Controllers
{
    [Authorize(Roles="dbAdmin")]
    public class PlayListController : DMController
    {
        // GET: PlayLists
        public ActionResult Index()
        {
            return View(Database.PlayLists.ToList());
        }

        // GET: PlayLists/Details/5
        public ActionResult Details(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var playList = Database.PlayLists.Find(id);
            if (playList == null)
            {
                return HttpNotFound();
            }
            return View(playList);
        }

        // GET: PlayLists/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: PlayLists/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,User,Type,Tags")] PlayList playList)
        {
            if (ModelState.IsValid)
            {
                playList.Created = DateTime.Now;
                playList.Updated = null;
                playList.Deleted = false;
                Database.PlayLists.Add(playList);
                Database.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(playList);
        }

        // GET: PlayLists/Edit/5
        public ActionResult Edit(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var playList = Database.PlayLists.Find(id);
            if (playList == null)
            {
                return HttpNotFound();
            }
            return View(playList);
        }

        // POST: PlayLists/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,User,Type,Tags,SongIds")] PlayList playList)
        {
            if (ModelState.IsValid)
            {
                Context.Entry(playList).State = EntityState.Modified;
                Database.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(playList);
        }

        // GET: PlayLists/Delete/5
        public ActionResult Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var playList = Database.PlayLists.Find(id);
            if (playList == null)
            {
                return HttpNotFound();
            }
            return View(playList);
        }

        // POST: PlayLists/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(string id)
        {
            var playList = Database.PlayLists.Find(id);
            if (playList != null)
            {
                Database.PlayLists.Remove(playList);
                Database.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.errorMessage = $"Playlist ${id} not found.";
            return View("Error");
        }

        // GET: Update
        public ActionResult Update(string id)
        {
            // Load the playlist obect
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var playList = Database.PlayLists.Find(id);
            if (playList == null)
            {
                return HttpNotFound();
            }

            // Load the playlist from spotify
            var service = MusicService.FromPlayList(playList.Type);
            var user = Database.FindUser(playList.User);
            var url = service?.BuildPlayListLink(playList,user);
            if (url == null)
            {
                ViewBag.errorMessage = $"Playlists of type ${playList.Type} not not yet supported.";
                return View("Error");
            }

            if (!AdminMonitor.StartTask("UpdatePlayList"))
            {
                throw new AdminTaskException("UpdatePlaylist failed to start because there is already an admin task running");
            }

            var tracks = MusicServiceManager.LookupServiceTracks(service, url, User);

            var newSongs = Database.SongsFromTracks(playList.User, tracks, playList.Tags);

            // TODO: Diff with existing playlist

            // Match songs & update

            Task.Run(() =>
            {
                try
                {
                    AdminMonitor.UpdateTask("Starting Merge");
                    var results = DanceMusicService.GetService().MatchSongs(newSongs, DanceMusicService.MatchMethod.Merge);
                    var link =
                        $"/admin/reviewbatch?title=Scrape Spotify&commit=CommitUploadCatalog&fileId={AdminController.CacheReview(new Review {PlayList=playList.Id,Merge=results})}&user={playList.User}" +
                        playList.Tags;
                    AdminMonitor.CompleteTask(true, $"<a href='{link}'>{link}</a>");
                }
                catch (Exception e)
                {
                    AdminMonitor.CompleteTask(false, $"Playlist Merge: Failed={e.Message}");
                }
            });

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);

            // Add new songs

            // Kick off spotify playlist update
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Database.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
