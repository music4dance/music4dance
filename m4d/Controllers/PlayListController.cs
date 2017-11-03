using System;
using System.Collections.Generic;
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
            return View(Database.PlayLists.OrderBy(p => p.User).ToList());
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
            var url = service?.BuildPlayListLink(playList, user);
            if (url == null)
            {
                ViewBag.errorMessage = $"Playlists of type ${playList.Type} not not yet supported.";
                return View("Error");
            }

            if (!AdminMonitor.StartTask("UpdatePlayList"))
            {
                throw new AdminTaskException(
                    "UpdatePlaylist failed to start because there is already an admin task running");
            }

            IList<Song> newSongs;
            try
            {
                var tracks = MusicServiceManager.LookupServiceTracks(service, url, User);

                var oldTrackIds = playList.SongIds;
                if (oldTrackIds != null)
                {
                    tracks = tracks.Where(t => !oldTrackIds.Contains(t.TrackId)).ToList();
                }

                if (tracks.Count == 0)
                {
                    ViewBag.Title = "Update Playlist";
                    ViewBag.Message = $"No new tracks for playlist {playList.Id}";

                    return View("Info");
                }

                var tags = playList.Tags.Split(new [] { "|||" },2,StringSplitOptions.None);
                newSongs = Database.SongsFromTracks(playList.User, tracks, tags[0], tags.Length > 1 ? tags[1] : string.Empty);
            }
            catch (Exception e)
            {
                AdminMonitor.CompleteTask(false,$"Update Playlist failed: {e.Message}");
                return View("Error", new HandleErrorInfo(e,"PlayListController", "Update"));
            }


            // Match songs & update
            Task.Run(() =>
            {
                try
                {
                    var dms = DanceMusicService.GetService();
                    AdminMonitor.UpdateTask("Starting Merge");
                    var results = DanceMusicService.GetService().MatchSongs(newSongs, DanceMusicService.MatchMethod.Merge);
                    var succeeded = CommitCatalog(dms, new Review {PlayList = playList.Id, Merge = results}, playList.User);
                    AdminMonitor.CompleteTask(true, $"Updated PlayList {playList.Id} with {succeeded} songs.");
                }
                catch (Exception e)
                {
                    AdminMonitor.CompleteTask(false, $"Playlist Merge: Failed={e.Message}");
                }
            });

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
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
