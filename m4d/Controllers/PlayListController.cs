using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading.Tasks;
using AmazonCommerce;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace m4d.Controllers
{
    public class PlayListController : DanceMusicController
    {
        public PlayListController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager, IConfiguration configuration) :
                base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

        // GET: PlayLists
        [Authorize(Roles = "dbAdmin")]
        public IActionResult Index(PlayListType type = PlayListType.SongsFromSpotify)
        {
            return View(GetIndex(type));
        }

        // GET: PlayLists/Details/5
        [Authorize(Roles = "dbAdmin")]
        public IActionResult Details(string id)
        {
            var result = GetPlaylist(id, out var playList);
            if (HttpStatusCode.OK == result)
            {
                return View(playList);
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
        public ActionResult Create([Bind("Id,User,Type,Tags,Name,Description")] PlayList playList)
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
            var result = GetPlaylist(id, out var playList);
            if (HttpStatusCode.OK == result)
            {
                return View(playList);
            }

            return View(playList);
        }

        // POST: PlayLists/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult Edit([Bind("Id,User,Type,Data1,Data2,Name,Description")] PlayList playList)
        {
            if (ModelState.IsValid)
            {
                Database.Context.Entry(playList).State = EntityState.Modified;
                Database.SaveChanges();
                return RedirectToAction("Index", new { playList.Type });
            }
            return View(playList);
        }

        // GET: PlayLists/Delete/5
        [Authorize(Roles = "dbAdmin")]
        public ActionResult Delete(string id)
        {
            var result = GetPlaylist(id, out var playList);
            if (HttpStatusCode.OK == result)
            {
                return View(playList);
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
        public async Task<ActionResult> Update(string id)
        {
            if (!AdminMonitor.StartTask("UpdatePlayList"))
            {
                throw new AdminTaskException(
                    "UpdatePlaylist failed to start because there is already an admin task running");
            }

            var principal = User;

            var playlist = SafeLoadPlaylist(id, Database);
            var user = Database.FindUser(playlist.User);
            var email = user.Email;

            await SpotifyAuthorization();

            var dms = new DanceMusicCoreService(Database.Context.CreateTransientContext(), SearchService, DanceStatsManager);

            // Match songs & update
            Task.Run(() =>
            {
                try
                {
                    var result = DoUpdate(id, email, dms, principal);
                    AdminMonitor.CompleteTask(result != null && result.IndexOf("Succeeded") != -1, result);
                }
                catch (Exception e)
                {
                    AdminMonitor.CompleteTask(false, $"UpdatePlayList  failed: {e.Message}");
                }
                finally
                {
                    dms.Dispose();
                }
            });

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        // GET: UpdateAll
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> UpdateAll(PlayListType type = PlayListType.SongsFromSpotify)
        {
            if (!AdminMonitor.StartTask("UpdateAllPlayLists"))
            {
                throw new AdminTaskException(
                    "UpdateAllPlayLists failed to start because there is already an admin task running");
            }

            await SpotifyAuthorization();

            UpdateAllBase(type, User);

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        [AllowAnonymous]
        public ContentResult UpdateBatch(PlayListType type = PlayListType.SongsFromSpotify)
        {
            if (!TokenRequirement.Authorize(Request, Configuration))
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
        public async  Task<ActionResult> BulkCreate([FromServices] IFileProvider fileProvider, PlayListType type, string flavor = "TopN")
        {
            await SpotifyAuthorization();

            var spotify = MusicService.GetService(ServiceType.Spotify);
            var oldS = MusicServiceManager.GetPlaylists(spotify, User).ToDictionary(p => p.Name, p => new PlaylistMetadata{Id = p.Id, Name= p.Name});
            var oldM = Database.PlayLists.Where(p => p.Type == PlayListType.SpotifyFromSearch).Where(p => p.Name != null).ToDictionary(p => p.Name, p => p);

            switch (flavor)
            {
                case "TopN":
                    BulkCreateTopN(oldS, oldM, fileProvider);
                    break;
                case "Holiday":
                    BulkCreateHoliday(oldS, oldM, fileProvider);
                    break;
            }

            Database.SaveChanges();

            return View("Index", GetIndex(PlayListType.SpotifyFromSearch));
        }

        private void UpdateAllBase(PlayListType type, IPrincipal user = null)
        {
            var playlists = Database.PlayLists.Where(p => p.Type == type).ToList();
            var emailMap = UserEmail(playlists);

            var dms = Database.GetTransientService();
            Task.Run(() =>
            {
                try
                {
                    var results = new List<string>();
                    var failures = new List<string>();

                    var i = 0;
                    foreach (var playlist in playlists)
                    {
                        var id = playlist.Id;
                        if (emailMap.TryGetValue(playlist.User, out var email))
                        {
                            var result = DoUpdate(id, email, dms, user);
                            AdminMonitor.UpdateTask($"Playlist {id}", i);
                            results.Add(result);
                        }
                        else
                        {
                            failures.Add(id);
                        }
                        i += 1;
                    }
                    AdminMonitor.CompleteTask(true, $"Results: {string.Join("\t", results)}, Failures: {string.Join("\t", failures)}");
                }
                catch (Exception e)
                {
                    AdminMonitor.CompleteTask(false, $"UpdateAll Playlists failed: {e.Message}");
                }
                finally
                {
                    dms.Dispose();
                }
            });
        }

        private void BulkCreateTopN(IReadOnlyDictionary<string, PlaylistMetadata> oldS, IReadOnlyDictionary<string, PlayList> oldM, IFileProvider fileProvider)
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
                    metadata = MusicServiceManager.CreatePlaylist(MusicService.GetService(ServiceType.Spotify), User, name, description, fileProvider);
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
                    playlist = new PlayList();
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

        private void BulkCreateHoliday(IReadOnlyDictionary<string, PlaylistMetadata> oldS, IReadOnlyDictionary<string, PlayList> oldM, IFileProvider fileProvider)
        {
            foreach (var ds in Database.DanceStats.List)
            {
                var dt = ds.DanceType;
                var dg = ds.DanceGroup;
                if (dt == null && dg == null) continue;

                if (dt != null && ds.SongCountExplicit < 25) continue;
                if (dg != null && ds.SongCountImplicit < 25) continue;

                var name = $"Holiday {ds.DanceName}";

                oldS.TryGetValue(name, out var metadata);
                var m4dExists = oldM.ContainsKey(name);

                if (metadata != null && m4dExists) continue;

                var description = $"{ds.DanceName} Dance Holiday songs from music4dance.net";

                var search = SongFilter.CreateHolidayFilter(ds.DanceName);

                Trace.WriteLineIf(TraceLevels.General.TraceInfo,$"BulkCreateHoliday: {name}, {description}, {search}");

                if (metadata == null)
                {
                    metadata = MusicServiceManager.CreatePlaylist(MusicService.GetService(ServiceType.Spotify), User, name, description, fileProvider);
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
                    playlist = new PlayList();
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

                if (!exists)
                {
                    Database.PlayLists.Add(playlist);
                }
            }
        }


        private string DoUpdate(string id, string email, DanceMusicCoreService dms, IPrincipal principal)
        {
            var playlist = SafeLoadPlaylist(id, dms);
            switch (playlist.Type)
            {
                case PlayListType.SongsFromSpotify:
                    return UpdateSongsFromSpotify(playlist, email, dms);
                case PlayListType.SpotifyFromSearch:
                    return UpdateSpotifyFromSearch(playlist, dms, principal);
                default:
                    return $"Playlist {id} unsupported type - {playlist.Type}";
            }
        }

        private string UpdateSongsFromSpotify(PlayList playlist, string email, DanceMusicCoreService dms)
        {
            try
            {
                var spl = LoadServicePlaylist(playlist, email, dms, MusicServiceManager);
                if (spl == null)
                {
                    return $"UpdateSongsFromSpotify {playlist.Id}: Unable to load playlist";
                }

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
                    return $"UpdateSongsFromSpotify {playlist.Id}: Empty Playlist";
                }
                var tags = playlist.Tags.Split(new[] { "|||" }, 2, StringSplitOptions.None);
                var newSongs = dms.SongsFromTracks(playlist.User, tracks, tags[0], tags.Length > 1 ? tags[1] : string.Empty);

                AdminMonitor.UpdateTask("Starting Merge");
                var results = dms.MatchSongs(newSongs, DanceMusicCoreService.MatchMethod.Merge);
                var succeeded = CommitCatalog(dms, new Review { PlayList = playlist.Id, Merge = results }, playlist.User);
                return $"UpdateSongsFromSpotify {playlist.Id}: Succeeded with {succeeded} songs.";
            }
            catch (Exception e)
            {
                return $"UpdateSongsFromSpotify {playlist.Id}: Failed={e.Message}";
            }
        }

        private string UpdateSpotifyFromSearch(PlayList playlist, DanceMusicCoreService dms, 
            IPrincipal principal)
        {
            try
            {
                var spotify = MusicService.GetService(ServiceType.Spotify);
                var filter = new SongFilter(playlist.Search)
                {
                    Purchase = spotify.CID.ToString()
                };
                var sr = dms.AzureSearch(filter, playlist.Count == -1 ? 100 : playlist.Count);
                if (sr.Count == 0)
                {
                    return $"UpdateSpotifyFromSearch {playlist.Id}: Empty Playlist";
                }

                var tracks = sr.Songs.Select(s => s.GetPurchaseId(ServiceType.Spotify));
                if (MusicServiceManager.SetPlaylistTracks(spotify, principal, playlist.Id, tracks))
                {
                    playlist.Updated = DateTime.Now;
                    dms.SaveChanges();
                    return $"UpdateSpotifyFromSearch {playlist.Id}: Succeeded";
                }
                return $"UpdateSpotifyFromSearch {playlist.Id}: Failed to set playlist";
            }
            catch (Exception e)
            {
                return $"UpdateSpotifyFromSearch ({playlist.Id}: Failed={e.Message}";
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

            var playlist = SafeLoadPlaylist(id, Database);
            var user = Database.FindUser(playlist.User);
            var email = user.Email;

            // Match songs & update
            var dms = Database.GetTransientService();
            Task.Run(() =>
            {
                try
                {
                    var success = DoRestore(id, email, dms, MusicServiceManager, out var result);
                    AdminMonitor.CompleteTask(success, result);
                }
                catch (Exception e)
                {
                    AdminMonitor.CompleteTask(false, $"BatchMusicService: Failed={e.Message}");
                }
                finally
                {
                    dms.Dispose();
                }
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

            // TODO:  This code is identical to the code in updateallbase except for the actual DoUpdate/DoRestore call, should be able to do better..
            var playlists = Database.PlayLists.Where(p => string.IsNullOrEmpty(p.Data2) && p.Updated != null).ToList();
            var emailMap = UserEmail(playlists);

            var dms = Database.GetTransientService();
            Task.Run(() =>
            {
                try
                {
                    var results = new List<string>();
                    var failures = new List<string>();

                    var i = 0;
                    foreach (var playlist in playlists)
                    {
                        var id = playlist.Id;
                        if (emailMap.TryGetValue(playlist.User, out var email))
                        {
                            DoRestore(id, email, dms, MusicServiceManager, out var result);
                            AdminMonitor.UpdateTask($"Playlist {id}", i);
                            results.Add(result);
                        }
                        else
                        {
                            failures.Add(id);
                        }
                        i += 1;
                    }
                    AdminMonitor.CompleteTask(true, $"Results: {string.Join("\t", results)}, Failures: {string.Join("\t", failures)}" );
                }
                catch (Exception e)
                {
                    AdminMonitor.CompleteTask(false, $"RestoreAll Playlists failed: {e.Message}");
                }
                {
                    dms.Dispose();
                }
            });

            return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
        }

        private Dictionary<string, string> UserEmail(IEnumerable<PlayList> playlists)
        {
            var map = new Dictionary<string,string>();
            foreach (var playlist in playlists)
            {
                if (!map.ContainsKey(playlist.User))
                {
                    var user = Database.FindUser(playlist.User);
                    if (user != null)
                    {
                        map[playlist.User] = user.Email;
                    }
                }
            }

            return map;
        }

        private PlayListIndex GetIndex(PlayListType type)
        {
            return new PlayListIndex
            {
                Type = type,
                PlayLists = Database.PlayLists.Where(p => p.Type == type).OrderBy(p => p.User).ToList()
            };
        }

        private static bool DoRestore(string id, string email, DanceMusicCoreService dms, MusicServiceManager serviceManager, out string result)
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

                var spl = LoadServicePlaylist(playlist, email, dms, serviceManager);
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

        private static PlayList SafeLoadPlaylist(string id, DanceMusicCoreService dms)
        {
            // Load the playlist object
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


        private static GenericPlaylist LoadServicePlaylist(PlayList playList, string email, DanceMusicCoreService dms, MusicServiceManager serviceManager)
        {
            var service = MusicService.GetService(ServiceType.Spotify);

            var url = service?.BuildPlayListLink(playList, playList.User, email);

            if (url == null)
            {
                throw new ArgumentOutOfRangeException(nameof(playList.Type), $@"Playlists of type ${playList.Type} not not yet supported.");
            }

            return serviceManager.LookupPlaylist(service, url);
        }

        private HttpStatusCode GetPlaylist(string id, out PlayList playList)
        {
            playList = null;

            if (id == null)
            {
                return HttpStatusCode.BadRequest;
            }

            playList = Database.PlayLists.Find(id);
            if (playList == null)
            {
                return HttpStatusCode.NotFound;
            }

            return HttpStatusCode.OK;
        }


        private async Task SpotifyAuthorization()
        {
            var authResult = await HttpContext.AuthenticateAsync();
            AdmAuthentication.GetServiceAuthorization(Configuration, ServiceType.Spotify, User, authResult);
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
