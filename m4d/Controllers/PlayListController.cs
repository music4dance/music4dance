using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using m4d.Utilities;
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
            if (!AdminMonitor.StartTask("UpdatePlayList"))
            {
                throw new AdminTaskException(
                    "UpdatePlaylist failed to start because there is already an admin task running");
            }

            // Match songs & update
            Task.Run(() => DoUpdate(id));

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        private static void DoUpdate(string id)
        {
            try
            {
                var dms = DanceMusicService.GetService();
                var playList = SafeLoadPlaylist(id, dms);

                var tracks = LoadTracks(playList, dms);

                var oldTrackIds = playList.SongIds;
                if (oldTrackIds != null)
                {
                    tracks = tracks.Where(t => !oldTrackIds.Contains(t.TrackId)).ToList();
                }

                if (tracks.Count == 0)
                {
                    AdminMonitor.CompleteTask(false, $"No new tracks for playlist {playList.Id}");
                }
                else
                {
                    var tags = playList.Tags.Split(new[] { "|||" }, 2, StringSplitOptions.None);
                    var newSongs = dms.SongsFromTracks(playList.User, tracks, tags[0], tags.Length > 1 ? tags[1] : string.Empty);

                    AdminMonitor.UpdateTask("Starting Merge");
                    var results = DanceMusicService.GetService().MatchSongs(newSongs, DanceMusicService.MatchMethod.Merge);
                    var succeeded = CommitCatalog(dms, new Review { PlayList = playList.Id, Merge = results }, playList.User);
                    AdminMonitor.CompleteTask(true, $"Updated PlayList {playList.Id} with {succeeded} songs.");
                }
            }
            catch (Exception e)
            {
                AdminMonitor.CompleteTask(false, $"Restore Playlist: Failed={e.Message}");
            }
        }

        // GET: Restore
        public ActionResult Restore(string id)
        {
            if (!AdminMonitor.StartTask("RestorePlayList"))
            {
                throw new AdminTaskException(
                    "RestorePlaylist failed to start because there is already an admin task running");
            }

            // Match songs & update
            Task.Run(() => DoRestore(id));

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        private static void DoRestore(string id)
        {
            try
            {
                var dms = DanceMusicService.GetService();
                var playList = SafeLoadPlaylist(id, dms);

                var tracks = LoadTracks(playList, dms);

                if (tracks.Count == 0)
                {
                    AdminMonitor.CompleteTask(true, $"No new tracks for playlist {playList.Id}");
                }
                else
                {
                    var songs = new List<Song>();
                    var service = MusicService.GetService(ServiceType.Spotify);
                    foreach (var track in tracks)
                    {
                        var song = dms.GetSongFromService(service, track.TrackId);
                        if (song?.FindModified(playList.User) != null)
                        {
                            songs.Add(song);
                        }
                    }

                    playList = dms.PlayLists.Find(id);
                    if (playList == null)
                    {
                        throw new Exception($"Playlist {id} disappeared!");
                    }
                    playList.AddSongs(songs.Select(s => s.GetPurchaseId(service.Id)));
                    dms.SaveChanges();

                    AdminMonitor.CompleteTask(true, $"Restore PlayList {playList.Id} with {songs.Count} songs.");
                }
            }
            catch (Exception e)
            {
                AdminMonitor.CompleteTask(false, $"Restore Playlist: Failed={e.Message}");
            }
        }

        private static PlayList SafeLoadPlaylist(string id, DanceMusicService dms)
        {
            // Load the playlist obect
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }
            var playList = dms.PlayLists.Find(id);
            if (playList == null)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }
            return playList;
        }

        private static IList<ServiceTrack> LoadTracks(PlayList playList, DanceMusicService dms)
        {
            var service = MusicService.FromPlayList(playList.Type);
            var user = dms.FindUser(playList.User);
            var url = service?.BuildPlayListLink(playList, user);

            if (url == null)
            {
                throw new ArgumentOutOfRangeException(nameof(playList.Type), $@"Playlists of type ${playList.Type} not not yet supported.");
            }

            return new MusicServiceManager().LookupServiceTracks(service, url);
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
