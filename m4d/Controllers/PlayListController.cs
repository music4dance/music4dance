﻿using System.Net;
using System.Security.Principal;

using m4d.Services;
using m4d.Utilities;

using m4dModels;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

public class PlayListController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
    IFeatureManagerSnapshot featureManager, ILogger<ActivityLogController> logger) : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backroundTaskQueue, featureManager, logger)
{

    // GET: PlayLists
    [Authorize(Roles = "dbAdmin")]
    public IActionResult Index(PlayListType type = PlayListType.SongsFromSpotify, string user = null)
    {
        return View(user == null ? GetIndex(type) : GetUserIndex(type, user));
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
    public async Task<ActionResult> Create([Bind("Id,User,Type,Tags,Name,Description")]
        PlayList playList)
    {
        if (ModelState.IsValid)
        {
            playList.Created = DateTime.Now;
            playList.Updated = null;
            playList.Deleted = false;
            Database.PlayLists.Add(playList);
            await Database.SaveChanges();
            return RedirectToAction("Index", new { playList.Type });
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
    public async Task<ActionResult> Edit([Bind("Id,User,Type,Data1,Data2,Name,Description")]
        PlayList playList)
    {
        if (ModelState.IsValid)
        {
            Database.Context.Entry(playList).State = EntityState.Modified;
            await Database.SaveChanges();
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
    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> DeleteConfirmed(string id)
    {
        var playList = await Database.PlayLists.FindAsync(id);
        if (playList != null)
        {
            Database.PlayLists.Remove(playList);
            await Database.SaveChanges();
            return RedirectToAction("Index", new { playList.Type });
        }

        ViewBag.errorMessage = $"Playlist ${id} not found.";
        return View("Error");
    }

    // GET: PlayLists
    [Authorize(Roles = "dbAdmin")]
    public async Task<IActionResult> Statistics()
    {
        return View(await GetStatistics());
    }

    private async Task<List<PlaylistMetadata>> GetStatistics()
    {
        await SpotifyAuthorization();

        var spotify = MusicService.GetService(ServiceType.Spotify);
        return await MusicServiceManager.GetPlaylists(spotify, User);
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
        var user = await Database.FindUser(playlist.User);
        var email = user.Email;

        await SpotifyAuthorization();

        var dms = new DanceMusicCoreService(
            Database.Context.CreateTransientContext(),
            SearchService, DanceStatsManager);

        // Match songs & update
        await Task.Run(
            async () =>
            {
                try
                {
                    var result = await DoUpdate(id, email, dms, principal);
                    AdminMonitor.CompleteTask(
                        result != null && result.Contains("Succeeded", StringComparison.OrdinalIgnoreCase),
                        result);
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

        await UpdateAllBase(type, User);

        return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
    }

    [AllowAnonymous]
    public async Task<ContentResult> UpdateBatch(
        PlayListType type = PlayListType.SongsFromSpotify)
    {
        if (!TokenRequirement.Authorize(Request, Configuration))
        {
            throw new Exception("Unauthorized access.");
        }

        if (!AdminMonitor.StartTask("UpdateAllPlayLists"))
        {
            return Content("{success:false, reason='Another Admin Task is already running'}");
        }

        await UpdateAllBase(type);

        return Content("{success:true, reason='Kicked off Update Batch'}");
    }

    // GET: BulkCreate
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> BulkCreate([FromServices] IFileProvider fileProvider,
        string flavor = "TopN")
    {
        await SpotifyAuthorization();

        var spotify = MusicService.GetService(ServiceType.Spotify);
        var oldS = (await MusicServiceManager.GetPlaylists(spotify, User)).ToDictionary(
            p => p.Name,
            p => new PlaylistMetadata { Id = p.Id, Name = p.Name });
        var oldM = Database.PlayLists.Where(p => p.Type == PlayListType.SpotifyFromSearch)
            .Where(p => p.Name != null).ToDictionary(p => p.Name, p => p);

        switch (flavor)
        {
            case "TopN":
                await BulkCreateTopN(oldS, oldM, fileProvider);
                break;
            case "Holiday":
            case "Halloween":
                await BulkCreateHoliday(oldS, oldM, fileProvider, flavor.ToLower());
                break;
        }

        await Database.SaveChanges();

        return View("Index", GetIndex(PlayListType.SpotifyFromSearch));
    }

    private async Task UpdateAllBase(PlayListType type, IPrincipal user = null)
    {
        var playlists = Database.PlayLists.Where(p => p.Type == type).ToList();
        var emailMap = await UserEmail(playlists);

        var dms = Database.GetTransientService();
        await Task.Run(
            async () =>
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
                            var result = await DoUpdate(id, email, dms, user);
                            AdminMonitor.UpdateTask($"Playlist {id}", i);
                            results.Add(result);
                        }
                        else
                        {
                            failures.Add(id);
                        }

                        i += 1;
                    }

                    AdminMonitor.CompleteTask(
                        true,
                        $"Results: {string.Join("\t", results)}, Failures: {string.Join("\t", failures)}");
                }
                catch (Exception e)
                {
                    AdminMonitor.CompleteTask(
                        false, $"UpdateAll Playlists failed: {e.Message}");
                }
                finally
                {
                    dms.Dispose();
                }
            });
    }

    private async Task BulkCreateTopN(IReadOnlyDictionary<string, PlaylistMetadata> oldS,
        IReadOnlyDictionary<string, PlayList> oldM, IFileProvider fileProvider)
    {
        foreach (var ds in Database.DanceStats.Dances)
        {
            var dt = ds.DanceType;
            if (dt == null || ds.SongCount < 25)
            {
                continue;
            }

            oldS.TryGetValue(ds.DanceName, out var metadata);
            var m4dExists = oldM.ContainsKey(ds.DanceName);

            if (metadata != null && m4dExists)
            {
                continue;
            }

            // Build name and description
            var name = ds.DanceName;
            var count = 100;

            if (ds.SongCount < 25)
            {
                count = 25;
            }
            else if (ds.SongCount < 50)
            {
                count = 50;
            }

            var description = $"{count} most popular {name} songs from music4dance.net";

            var search = Database.SearchService.GetSongFilter();
            search.Dances = ds.DanceId;
            search.SortOrder = "Dances";
            search.Tags = "-Fake:Tempo";

            Logger.LogInformation($"BulkCreateTopN: {name}, {description}, {search}");

            metadata ??= await MusicServiceManager.CreatePlaylist(
                    MusicService.GetService(ServiceType.Spotify), User,
                    await GetLoginKey("Spotify"), name, description,
                    fileProvider);

            if (metadata == null)
            {
                Logger.LogError($"BulkCreateTopN:Unable to create playlist {name}");
                continue;
            }

            if (m4dExists)
            {
                continue;
            }

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
            playlist.Count = count;
            playlist.Created = DateTime.Now;
            playlist.Updated = DateTime.Now;
            playlist.User = UserName;

            if (!exists)
            {
                Database.PlayLists.Add(playlist);
            }

        }
    }

    private async Task BulkCreateHoliday(IReadOnlyDictionary<string, PlaylistMetadata> oldS,
        IReadOnlyDictionary<string, PlayList> oldM, IFileProvider fileProvider, string occassion)
    {
        var title = char.ToUpper(occassion[0]) + occassion[1..];

        foreach (var ds in Database.DanceStats.Dances)
        {
            var dt = ds.DanceType;
            if (dt == null || ds.SongCount < 25)
            {
                continue;
            }

            var name = $"{title} {ds.DanceName}";

            oldS.TryGetValue(name, out var metadata);
            var m4dExists = oldM.ContainsKey(name);

            if (metadata != null && m4dExists)
            {
                continue;
            }

            var description = $"{ds.DanceName} Dance {title} songs from music4dance.net";

            var search = Database.SearchService.GetSongFilter().CreateCustomSearchFilter(occassion, ds.DanceName);

            Logger.LogInformation($"BulkCreate{title}: {name}, {description}, {search}");

            metadata ??= await MusicServiceManager.CreatePlaylist(
                MusicService.GetService(ServiceType.Spotify),
                User, await GetLoginKey("Spotify"), name, description,
                fileProvider);

            if (metadata == null)
            {
                Logger.LogError($"BulkCreate{title}:Unable to create playlist {name}");
                continue;
            }

            if (m4dExists)
            {
                continue;
            }

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
            playlist.User = UserName;

            if (!exists)
            {
                Database.PlayLists.Add(playlist);
            }
        }
    }

    private async Task<string> DoUpdate(string id, string email, DanceMusicCoreService dms,
        IPrincipal principal)
    {
        var playlist = SafeLoadPlaylist(id, dms);
        return playlist.Type switch
        {
            PlayListType.SongsFromSpotify => await UpdateSongsFromSpotify(playlist, email, dms),
            PlayListType.SpotifyFromSearch => await UpdateSpotifyFromSearch(playlist, dms, principal),
            _ => $"Playlist {id} unsupported type - {playlist.Type}",
        };
    }

    private async Task<string> UpdateSongsFromSpotify(PlayList playlist, string email,
        DanceMusicCoreService dms)
    {
        try
        {
            var spl = await LoadServicePlaylist(playlist, email, MusicServiceManager);
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
                tracks = [.. tracks.Where(t => t.TrackId != null && !oldTrackIds.Contains(t.TrackId))];
            }

            if (tracks.Count == 0)
            {
                return $"UpdateSongsFromSpotify {playlist.Id}: Empty Playlist";
            }

            var (danceTags, songTags) = GetTags(playlist);
            var newSongs = await dms.SongIndex.SongsFromTracks(
                await Database.FindUser(playlist.User), tracks, danceTags, songTags, playlist.Id);

            AdminMonitor.UpdateTask("Starting Merge");
            var results = await dms.SongIndex.MatchSongs(
                newSongs, DanceMusicCoreService.MatchMethod.Merge);
            var succeeded = await CommitCatalog(
                dms,
                new Review { PlayList = playlist.Id, Merge = results },
                await Database.FindUser(playlist.User));
            return $"UpdateSongsFromSpotify {playlist.Id}: Succeeded with {succeeded} songs.";
        }
        catch (Exception e)
        {
            return $"UpdateSongsFromSpotify {playlist.Id}: Failed={e.Message}";
        }
    }

    private static (string danceTags, string songTags) GetTags(PlayList playlist)
    {
        var tags = playlist.Tags.Split(["|||"], 2, StringSplitOptions.None);
        return (tags[0], tags.Length > 1 ? tags[1] : string.Empty);
    }

    private async Task<string> UpdateSpotifyFromSearch(PlayList playlist,
        DanceMusicCoreService dms,
        IPrincipal principal)
    {
        try
        {
            var spotify = MusicService.GetService(ServiceType.Spotify);
            var filter = Database.SearchService.GetSongFilter(playlist.Search);
            filter.Purchase = spotify.CID.ToString();

            var sr = await dms.SongIndex.Search(
                filter, playlist.Count == -1 ? 100 : playlist.Count);
            if (sr.Count == 0)
            {
                return $"UpdateSpotifyFromSearch {playlist.Id}: Empty Playlist";
            }

            var tracks = sr.Songs.Select(s => s.GetPurchaseId(ServiceType.Spotify));
            if (await MusicServiceManager.SetPlaylistTracks(spotify, principal, playlist.Id, tracks))
            {
                playlist.Updated = DateTime.Now;
                await dms.SaveChanges();
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
    public async Task<ActionResult> Restore(string id)
    {
        if (!AdminMonitor.StartTask("RestorePlayList"))
        {
            throw new AdminTaskException(
                "RestorePlaylist failed to start because there is already an admin task running");
        }

        var playlist = SafeLoadPlaylist(id, Database);
        var user = await Database.FindUser(playlist.User);
        var email = user.Email;

        // Match songs & update
        var dms = Database.GetTransientService();
        await Task.Run(
            async () =>
            {
                try
                {
                    var result = await DoRestore(
                        id, email, dms, MusicServiceManager);
                    AdminMonitor.CompleteTask(true, result);
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
    public async Task<ActionResult> RestoreAll()
    {
        if (!AdminMonitor.StartTask("RestoreAllPlayLists"))
        {
            throw new AdminTaskException(
                "RestoreAllPlayLists failed to start because there is already an admin task running");
        }

        // TODO:  This code is identical to the code in updateallbase except for the actual DoUpdate/DoRestore call, should be able to do better..
        var playlists = Database.PlayLists
            .Where(p => string.IsNullOrEmpty(p.Data2) && p.Updated != null).ToList();
        var emailMap = await UserEmail(playlists);

        var dms = Database.GetTransientService();
        await Task.Run(
            async () =>
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
                            try
                            {
                                results.Add(
                                    await DoRestore(id, email, dms, MusicServiceManager));
                                AdminMonitor.UpdateTask($"Playlist {id}", i);
                            }
                            catch (Exception)
                            {
                                failures.Add(id);
                            }
                        }
                        else
                        {
                            failures.Add(id);
                        }

                        i += 1;
                    }

                    AdminMonitor.CompleteTask(
                        true,
                        $"Results: {string.Join("\t", results)}, Failures: {string.Join("\t", failures)}");
                }
                catch (Exception e)
                {
                    AdminMonitor.CompleteTask(
                        false, $"RestoreAll Playlists failed: {e.Message}");
                }

                {
                    dms.Dispose();
                }
            });

        return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
    }

    private async Task<Dictionary<string, string>> UserEmail(IEnumerable<PlayList> playlists)
    {
        var map = new Dictionary<string, string>();
        foreach (var playlist in playlists)
        {
            if (!map.ContainsKey(playlist.User))
            {
                var user = await Database.FindUser(playlist.User);
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
        return new()
        {
            Type = type,
            PlayLists = [.. Database.PlayLists.Where(p => p.Type == type).OrderBy(p => p.User)]
        };
    }

    private PlayListIndex GetUserIndex(PlayListType type, string user)
    {
        return new()
        {
            Type = type,
            PlayLists = [.. Database.PlayLists.Where(p => p.Type == type && p.User == user).OrderBy(p => p.Id)]
        };
    }

    private static async Task<string> DoRestore(string id, string email,
        DanceMusicCoreService dms,
        MusicServiceManager serviceManager)
    {
        try
        {
            var playlist = SafeLoadPlaylist(id, dms);
            if (playlist.Type != PlayListType.SongsFromSpotify)
            {
                throw new Exception(
                    $"Playlist {id} not restored: Unsupported type {playlist.Type}");
            }

            var spl = await LoadServicePlaylist(playlist, email, serviceManager) ?? throw new Exception(
                    $"Playlist {id} not restored: LoadServicePlaylist returned NULL");
            var tracks = spl.Tracks;

            if (tracks.Count == 0)
            {
                throw new Exception($"No new tracks for playlist {playlist.Id}");
            }

            var songs = new List<Song>();
            var service = MusicService.GetService(ServiceType.Spotify);
            foreach (var track in tracks)
            {
                var song = await dms.SongIndex.GetSongFromService(service, track.TrackId);
                if (song?.FindModified(playlist.User) != null)
                {
                    songs.Add(song);
                }
            }

            playlist.AddSongs(songs.Select(s => s.GetPurchaseId(service.Id)));
            await dms.SaveChanges();

            return $"Restore PlayList {playlist.Id} with {songs.Count} songs.  ";
        }
        catch (Exception e)
        {
            throw new Exception($"Restore Playlist {id}: Failed={e.Message}", e);
        }
    }

    private static PlayList SafeLoadPlaylist(string id, DanceMusicCoreService dms)
    {
        // Load the playlist object
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        var playList = dms.PlayLists.Find(id) ?? throw new ArgumentOutOfRangeException(nameof(id));
        return playList;
    }


    private static async Task<GenericPlaylist> LoadServicePlaylist(PlayList playList, string email,
        MusicServiceManager serviceManager)
    {
        var service = MusicService.GetService(ServiceType.Spotify);

        var url = (service?.BuildPlayListLink(playList, playList.User, email)) ?? throw new ArgumentOutOfRangeException(
                nameof(playList.Type),
                $@"Playlists of type ${playList.Type} not not yet supported.");
        return await serviceManager.LookupPlaylist(service, url, playList.SongIdList);
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
        await AdmAuthentication.GetServiceAuthorization(
            Configuration, ServiceType.Spotify, User,
            authResult);
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
