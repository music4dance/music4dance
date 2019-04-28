using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web.Mvc;
using m4d.Utilities;
using m4dModels;
using EntityState = System.Data.Entity.EntityState;

namespace m4d.Controllers
{
    public class PlayListController : DMController
    {
        // GET: PlayLists
        [Authorize(Roles = "dbAdmin")]
        public ActionResult Index(PlayListType type = PlayListType.SongsFromSpotify)
        {
            return View(GetIndex(type));
        }

        // GET: PlayLists/Details/5
        [Authorize(Roles = "dbAdmin")]
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
        [Authorize(Roles = "dbAdmin")]
        public ActionResult Create()
        {
            return View();
        }

        // POST: PlayLists/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult Create([Bind(Include = "Id,User,Type,Tags,Name,Description")] PlayList playList)
        {
            if (ModelState.IsValid)
            {
                playList.Created = DateTime.Now;
                playList.Updated = null;
                playList.Deleted = false;
                Database.PlayLists.Add(playList);
                Database.SaveChanges();
                return RedirectToAction("Index",new {playList.Type});
            }

            return View(playList);
        }

        // GET: PlayLists/Edit/5
        [Authorize(Roles = "dbAdmin")]
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
        [Authorize(Roles = "dbAdmin")]
        public ActionResult Edit([Bind(Include = "Id,User,Type,Data1,Data2,Name,Description")] PlayList playList)
        {
            if (ModelState.IsValid)
            {
                Context.Entry(playList).State = EntityState.Modified;
                Database.SaveChanges();
                return RedirectToAction("Index", new { playList.Type });
            }
            return View(playList);
        }

        // GET: PlayLists/Delete/5
        [Authorize(Roles = "dbAdmin")]
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
        [Authorize(Roles = "dbAdmin")]
        public ActionResult DeleteConfirmed(string id)
        {
            var playList = Database.PlayLists.Find(id);
            if (playList != null)
            {
                Database.PlayLists.Remove(playList);
                Database.SaveChanges();
                return RedirectToAction("Index", new { playList.Type });
            }

            ViewBag.errorMessage = $"Playlist ${id} not found.";
            return View("Error");
        }

        // GET: Update
        [Authorize(Roles = "dbAdmin")]
        public ActionResult Update(string id)
        {
            if (!AdminMonitor.StartTask("UpdatePlayList"))
            {
                throw new AdminTaskException(
                    "UpdatePlaylist failed to start because there is already an admin task running");
            }

            var user = User;
            // Match songs & update
            Task.Run(() =>
            {
                var success =  DoUpdate(id, DanceMusicService.GetService(), user,  out var result);
                AdminMonitor.CompleteTask(success, result);
            });

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        // GET: UpdateAll
        [Authorize(Roles = "dbAdmin")]
        public ActionResult UpdateAll(PlayListType type = PlayListType.SongsFromSpotify)
        {
            if (!AdminMonitor.StartTask("UpdateAllPlayLists"))
            {
                throw new AdminTaskException(
                    "UpdateAllPlayLists failed to start because there is already an admin task running");
            }

            UpdateAllBase(type, User);

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        [AllowAnonymous]
        public ContentResult UpdateBatch(PlayListType type = PlayListType.SongsFromSpotify)
        {
            if (!TokenAuthorizeAttribute.Authorize(Request))
            {
                throw new Exception("Unauthorized access.");
            }

            if (!AdminMonitor.StartTask("UpdateAllPlayLists"))
            {
                return Content("{success:false, reason='Another Admin Task is already running'}");
            }

            UpdateAllBase(type);

            return Content("{success:true, reason='Kicked off Update Batch'}");
        }

        // GET: UpdateAll
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult BulkCreate(PlayListType type, string flavor = "TopN")
        {
            // Get a list of existing playlists so that we don't add duplicates?
            var spotify = MusicService.GetService(ServiceType.Spotify);
            var oldS = MusicServiceManager.GetPlaylists(spotify, User).ToDictionary(p => p.Name, p => new PlaylistMetadata{Id = p.Id, Name= p.Name});
            var oldM = Database.PlayLists.Where(p => p.Type == PlayListType.SpotifyFromSearch).Where(p => p.Name != null).ToDictionary(p => p.Name, p => p);

            switch (flavor)
            {
                case "TopN":
                    BulkCreateTopN(oldS, oldM);
                    break;
                case "Holiday":
                    BulkCreateHoliday(oldS, oldM);
                    break;
            }

            Database.SaveChanges();

            return View("Index", GetIndex(PlayListType.SpotifyFromSearch));
        }

        private void UpdateAllBase(PlayListType type, IPrincipal user = null)
        {
            Task.Run(() =>
            {
                try
                {
                    var dms = DanceMusicService.GetService();
                    var results = new List<string>();
                    var i = 0;
                    foreach (var id in dms.PlayLists.Where(p => p.Type == type)
                        .Select(p => p.Id).ToList())
                    {
                        DoUpdate(id, dms, user, out var result);
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
        }

        private void BulkCreateTopN(IReadOnlyDictionary<string, PlaylistMetadata> oldS, IReadOnlyDictionary<string, PlayList> oldM)
        {
            foreach (var ds in Database.DanceStats.List)
            {
                var dt = ds.DanceType;
                if (dt == null || ds.SongCountExplicit < 25) continue;
                oldS.TryGetValue(ds.DanceName, out var metadata);
                var m4dExists = oldM.ContainsKey(ds.DanceName);

                if (metadata != null && m4dExists) continue;

                // Build name and description
                var name = ds.DanceName;
                var count = 100;

                if (ds.SongCountExplicit < 25)
                    count = 25;
                else if (ds.SongCountExplicit < 50)
                    count = 50;

                var description = $"{count} most popular {name} songs from music4dance.net";

                var search = new SongFilter
                {
                    Dances = ds.DanceId,
                    SortOrder = "Dances",
                    Tags = "-Fake:Tempo"
                };

                Trace.WriteLineIf(TraceLevels.General.TraceInfo,$"BulkCreateTopN: {name}, {description}, {search}");

                if (metadata == null)
                {
                    metadata = MusicServiceManager.CreatePlaylist(MusicService.GetService(ServiceType.Spotify), User, name, description);
                }

                if (metadata == null)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceError,$"BulkCreateTopN:Unable to create playlist {name}");
                    continue;
                }

                if (m4dExists) continue;

                var playlist = Database.PlayLists.Find(metadata.Id);
                var exists = playlist != null;

                if (!exists)
                {
                    playlist = Database.PlayLists.Create();
                    Database.PlayLists.Add(playlist);
                }

                playlist.Type = PlayListType.SpotifyFromSearch;
                playlist.Id = metadata.Id;
                playlist.Name = name;
                playlist.Description = description;
                playlist.Search = search.ToString();
                playlist.Count = count;
                playlist.Created = DateTime.Now;
                playlist.Updated = DateTime.Now;
                playlist.User = User.Identity.Name;
            }
        }

        private void BulkCreateHoliday(IReadOnlyDictionary<string, PlaylistMetadata> oldS, IReadOnlyDictionary<string, PlayList> oldM)
        {
            foreach (var ds in Database.DanceStats.List)
            {
                var dt = ds.DanceType;
                if (dt == null || ds.SongCountExplicit < 25) continue;

                var name = $"Holiday {ds.DanceName}";

                oldS.TryGetValue(name, out var metadata);
                var m4dExists = oldM.ContainsKey(name);

                if (metadata != null && m4dExists) continue;

                var description = $"{ds.DanceName} Holiday songs from music4dance.net";

                var search = SongFilter.CreateHolidayFilter(ds.DanceName);

                Trace.WriteLineIf(TraceLevels.General.TraceInfo,$"BulkCreateHolidy: {name}, {description}, {search}");

                if (metadata == null)
                {
                    metadata = MusicServiceManager.CreatePlaylist(MusicService.GetService(ServiceType.Spotify), User, name, description);
                }

                if (metadata == null)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceError,$"BulkCreateHolidy:Unable to create playlist {name}");
                    continue;
                }

                if (m4dExists) continue;

                var playlist = Database.PlayLists.Find(metadata.Id);
                var exists = playlist != null;

                if (!exists)
                {
                    playlist = Database.PlayLists.Create();
                    Database.PlayLists.Add(playlist);
                }

                playlist.Type = PlayListType.SpotifyFromSearch;
                playlist.Id = metadata.Id;
                playlist.Name = name;
                playlist.Description = description;
                playlist.Search = search.ToString();
                playlist.Count = -1;
                playlist.Created = DateTime.Now;
                playlist.Updated = DateTime.Now;
                playlist.User = User.Identity.Name;
            }
        }


        private bool DoUpdate(string id, DanceMusicService dms, IPrincipal principal, out string result)
        {
            var playlist = SafeLoadPlaylist(id, dms);
            switch (playlist.Type)
            {
                case PlayListType.SongsFromSpotify:
                    return UpdateSongsFromSpotify(playlist, dms, out result);
                case PlayListType.SpotifyFromSearch:
                    return UpdateSpotifyFromSearch(playlist, dms, principal, out result);
                default:
                    result = $"Playlist {id} unsupport type - {playlist.Type}";
                    return false;
            }
        }

        private bool UpdateSongsFromSpotify(PlayList playlist, DanceMusicService dms, out string result)
        {
            result = string.Empty;
            try
            {
                var spl = LoadServicePlaylist(playlist, dms);
                if (spl == null) return false;

                var tracks = spl.Tracks;
                playlist.Name = spl.Name;
                playlist.Description = spl.Description;

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

        private bool UpdateSpotifyFromSearch(PlayList playlist, DanceMusicService dms, IPrincipal principal, out string result)
        {
            result = string.Empty;
            try
            {
                var spotify = MusicService.GetService(ServiceType.Spotify);
                var filter = new SongFilter(playlist.Search)
                {
                    Purchase = spotify.CID.ToString()
                };
                var sr = dms.AzureSearch(filter, playlist.Count == -1 ? 100 : playlist.Count);
                if (sr.Count == 0 || (playlist.Count != -1 && sr.Count != playlist.Count)) return false;

                var tracks = sr.Songs.Select(s => s.GetPurchaseId(ServiceType.Spotify));
                if (MusicServiceManager.SetPlaylistTracks(spotify, principal, playlist.Id, tracks))
                {
                    playlist.Updated = DateTime.Now;
                    dms.SaveChanges();
                    return true;
                }
            }
            catch (Exception e)
            {
                result = $"UpdateSpotifyFromSearch ({playlist.Id}: Failed={e.Message}";
            }
            return false;
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

        private PlayListIndex GetIndex(PlayListType type)
        {
            return new PlayListIndex
            {
                Type = type,
                PlayLists = Database.PlayLists.Where(p => p.Type == type).OrderBy(p => p.User).ToList()
            };
        }

        private static bool DoRestore(string id, DanceMusicService dms, out string result)
        {
            result = string.Empty;
            try
            {
                var playlist = SafeLoadPlaylist(id, dms);
                if (playlist.Type != PlayListType.SongsFromSpotify)
                {
                    result = $"Playlist {id} not restored: Unsupported type {playlist.Type}";
                    return false;
                }

                var spl = LoadServicePlaylist(playlist, dms);
                if (spl == null) return false;

                var tracks = spl.Tracks;

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


        private static GenericPlaylist LoadServicePlaylist(PlayList playList, DanceMusicService dms)
        {
            var service = MusicService.GetService(ServiceType.Spotify);
            var user = dms.FindUser(playList.User);
            if (user == null)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo,$"LoadServicePlaylist: Updating playlist:  User {playList.User} doesn't exist");
                return null;
            }

            var url = service?.BuildPlayListLink(playList, user);

            if (url == null)
            {
                throw new ArgumentOutOfRangeException(nameof(playList.Type), $@"Playlists of type ${playList.Type} not not yet supported.");
            }

            return new MusicServiceManager().LookupPlaylist(service, url);
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

    public abstract class PlayListFlavor
    {
        protected PlayListFlavor(DanceStats stats)
        {
            Stats = stats;
        }
        public abstract string Name { get; }
        public abstract SongFilter BuildSongFilter(string input);
        public abstract string BuildDescription(string input, int count);
        public virtual bool CanCreate => true;
        public virtual int Count => -1;

        protected DanceStats Stats { get; }
    }

    //public class PlayListTopN : PlayListFlavor
    //{
    //    public PlayListTopN(DanceStats stats) : base(stats)
    //    {
    //    }

    //    public override string Name => "TopN";

    //    public override SongFilter BuildSongFilter(string input)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public override string BuildDescription(string input, int count)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}
