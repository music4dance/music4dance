/* TODONEXT: 
 *  Get Name and Description for SongsFromSpotify
 *  Add in the SpotifyFromSearch subclass
 *  Make Load/Backup handle both types
 *  Make the M4D spotify account an admin
 *  (semi) Manually create the dances playlists
 *  Implement Update for SpotifyFromSearch
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using AutoMapper;
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
        public ActionResult Create([Bind(Include = "Id,User,Type,Tags,Name,Description")] PlayList playList)
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
        public ActionResult Edit([Bind(Include = "Id,User,Type,Tags,SongIds,Name,Description")] PlayList playList)
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
            Task.Run(() =>
            {
                var success =  DoUpdate(id, DanceMusicService.GetService(), out var result);
                AdminMonitor.CompleteTask(success, result);
            });

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        // GET: UpdateAll
        public ActionResult UpdateAll()
        {
            if (!AdminMonitor.StartTask("UpdateAllPlayLists"))
            {
                throw new AdminTaskException(
                    "UpdateAllPlayLists failed to start because there is already an admin task running");
            }
            Task.Run(() =>
            {
                try
                {
                    var dms = DanceMusicService.GetService();
                    var results = new List<string>();
                    var i = 0;
                    foreach (var id in dms.PlayLists.Where(p => p.Updated != null)
                        .Select(p => p.Id).ToList())
                    {
                        DoUpdate(id, dms, out var result);
                        AdminMonitor.UpdateTask($"Playlist {id}", i);
                        results.Add(result);
                        i += 1;
                    }
                    AdminMonitor.CompleteTask(true, string.Join("\t", results));
                }
                catch (Exception e)
                {
                    AdminMonitor.CompleteTask(false, $"UpdateAll Playlists failed: {e.Message}");
                }
            });

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        private bool DoUpdate(string id, DanceMusicService dms, out string result)
        {
            var playlist = SafeLoadPlaylist(id, dms);
            switch (playlist.Type)
            {
                case PlayListType.SongsFromSpotify:
                    return DoUpdate(Mapper.Map<SongsFromSpotify>(playlist), dms, out result);
                default:
                    result = $"Playlist {id} unsupport type - {playlist.Type}";
                    return false;
            }
        }

        private bool DoUpdate(SongsFromSpotify playlist, DanceMusicService dms, out string result)
        {
            try
            {
                var tracks = LoadTracks(playlist, dms);

                var oldTrackIds = playlist.SongIds;
                if (oldTrackIds != null)
                {
                    tracks = tracks.Where(t => t.TrackId != null && !oldTrackIds.Contains(t.TrackId)).ToList();
                }

                if (tracks.Count == 0)
                {
                    result =  $"No new tracks for playlist {playlist.Id}";
                    return true;
                }
                var tags = playlist.Tags.Split(new[] { "|||" }, 2, StringSplitOptions.None);
                var newSongs = dms.SongsFromTracks(playlist.User, tracks, tags[0], tags.Length > 1 ? tags[1] : string.Empty);

                AdminMonitor.UpdateTask("Starting Merge");
                var results = DanceMusicService.GetService().MatchSongs(newSongs, DanceMusicService.MatchMethod.Merge);
                var succeeded = CommitCatalog(dms, new Review { PlayList = playlist.Id, Merge = results }, playlist.User);
                result =  $"Updated PlayList {playlist.Id} with {succeeded} songs.";
                return true;
            }
            catch (Exception e)
            {
                result = $"Restore Playlist: Failed={e.Message}";
                return false;
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
            Task.Run(() =>
            {
                var success =  DoRestore(id, DanceMusicService.GetService(), out var result);
                AdminMonitor.CompleteTask(success,result);
            });

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        // GET: RestoreAll
        public ActionResult RestoreAll()
        {
            if (!AdminMonitor.StartTask("RestoreAllPlayLists"))
            {
                throw new AdminTaskException(
                    "RestoreAllPlayLists failed to start because there is already an admin task running");
            }
            Task.Run(() =>
            {
                try
                {
                    var dms = DanceMusicService.GetService();
                    var results = new List<string>();
                    var i = 0;
                    foreach (var id in dms.PlayLists.Where(p => string.IsNullOrEmpty(p.Data2) && p.Updated != null).Select(p => p.Id).ToList())
                    {
                        DoRestore(id, dms, out var result);
                        AdminMonitor.UpdateTask($"Playlist {id}", i);
                        results.Add(result);
                        i += 1;
                    }
                    AdminMonitor.CompleteTask(true, string.Join("\t", results));
                }
                catch (Exception e)
                {
                    AdminMonitor.CompleteTask(false, $"RestoreAll Playlists failed: {e.Message}");
                }
            });

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        private static bool DoRestore(string id, DanceMusicService dms, out string result)
        {
            try
            {
                var playlistT = SafeLoadPlaylist(id, dms);
                if (playlistT.Type != PlayListType.SongsFromSpotify)
                {
                    result = $"Playlist {id} not restored: Unsupported type {playlistT.Type}";
                    return false;
                }
                var playlist = Mapper.Map<SongsFromSpotify>(playlistT);

                var tracks = LoadTracks(playlist, dms);

                if (tracks.Count == 0)
                {
                    result = $"No new tracks for playlist {playlist.Id}";
                    return true;
                }

                var songs = new List<Song>();
                var service = MusicService.GetService(ServiceType.Spotify);
                foreach (var track in tracks)
                {
                    var song = dms.GetSongFromService(service, track.TrackId);
                    if (song?.FindModified(playlist.User) != null)
                    {
                        songs.Add(song);
                    }
                }

                playlistT = dms.PlayLists.Find(id);
                if (playlistT == null)
                {
                    throw new Exception($"Playlist {id} disappeared!");
                }
                if (playlistT.Type != PlayListType.SongsFromSpotify)
                {
                    throw new Exception($"Playlist {id} change to unsupported type {playlistT.Type}");
                }
                playlist = Mapper.Map<SongsFromSpotify>(playlistT);
                playlist.AddSongs(songs.Select(s => s.GetPurchaseId(service.Id)));
                dms.SaveChanges();

                result = $"Restore PlayList {playlist.Id} with {songs.Count} songs.  ";
                return true;
            }
            catch (Exception e)
            {
                result = $"Restore Playlist {id}: Failed={e.Message}";
                return false;
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

        private static IList<ServiceTrack> LoadTracks(SongsFromSpotify playList, DanceMusicService dms)
        {
            var service = MusicService.GetService(ServiceType.Spotify);
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
