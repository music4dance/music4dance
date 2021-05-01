using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using DanceLibrary;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace m4d.Controllers
{
    public class DanceMusicController : Controller
    {
        protected readonly string MusicTheme = "music";
        protected readonly string ToolTheme = "tools";
        protected readonly string BlogTheme = "blog";
        protected readonly string AdminTheme = "admin";

        public DanceMusicController(
            DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, IConfiguration configuration)
        {
            Database = new DanceMusicService(context, userManager, searchService, danceStatsManager);
            SearchService = searchService;
            DanceStatsManager = danceStatsManager;
            Configuration = configuration;
        }

        public DanceMusicService Database { get; set; }

        protected MusicServiceManager MusicServiceManager => _musicServiceManager ??= new MusicServiceManager(Configuration);

        private MusicServiceManager _musicServiceManager;
        protected IConfiguration Configuration { get; }

        public ISearchServiceManager SearchService { get; }

        public IDanceStatsManager DanceStatsManager { get; }

        public UserManager<ApplicationUser> UserManager => Database.UserManager;

        public DanceMusicContext Context => Database.Context;

        public virtual string DefaultTheme => BlogTheme;
        public string ThemeName
        {
            get => _themeName ?? DefaultTheme;
            set => _themeName = value;
        }
        private string _themeName;

        public string HelpPage { get; set; }

        public ActionResult ReturnError(HttpStatusCode statusCode = HttpStatusCode.InternalServerError, string message = null, Exception exception = null)
        {
            var model = new ErrorModel { HttpStatusCode = (int)statusCode, Message = message, Exception = exception };

            Response.StatusCode = (int)statusCode;
            // Response.TrySkipIisCustomErrors = true;

            return View("HttpError", model);
        }

        protected IActionResult JsonCamelCase(object json)
        {
            return new JsonResult(json, JsonHelpers.CamelCaseSerializer);
        }

        public override ViewResult View(string viewName, object model)
        {
            ViewBag.Theme = ThemeName;
            ViewBag.Help = HelpPage;
            return base.View(viewName,  model);
        }

        public ActionResult CheckSpiders()
        {
            return SpiderManager.CheckBadSpiders(Request.Headers[HeaderNames.UserAgent]) ? View("BotWarning") : null;
        }

        protected void SaveSong(Song song)
        {
            Database.SaveSong(song);
        }

        protected static readonly JsonSerializerSettings CamelCaseSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            ContractResolver = new StatsContractResolver(true, true)
        };


        protected void SaveSongs(IEnumerable<Song> songs = null)
        {
            Database.SaveSongs(songs);
        }

        #region AdminTaskHelpers
        protected void StartAdminTask(string name)
        {
            ViewBag.Name = name;
            if (!AdminMonitor.StartTask(name))
            {
                throw new AdminTaskException(name + "failed to start because there is already an admin task running");
            }
        }

        protected ActionResult CompleteAdminTask(bool completed, string message)
        {
            ViewBag.Success = completed;
            ViewBag.Message = message;
            AdminMonitor.CompleteTask(completed, message);

            return View("Results");
        }

        protected ActionResult FailAdminTask(string message, Exception e)
        {
            ViewBag.Success = false;
            ViewBag.Message = message;

            if (!(e is AdminTaskException))
            {
                AdminMonitor.CompleteTask(false, message, e);
            }

            return View("Results");
        }

        protected ActionResult RestoreBatch()
        {
            ViewBag.Success = false;
            ViewBag.Message = "This functionality hasn't been re-implemented after the azure-search migration - do we really need it?";

            return View("Results");
        }
        #endregion

        protected int CommitCatalog(DanceMusicCoreService dms, Review review,
            ApplicationUser user, string danceIds = null)
        {
            List<string> dances = null;
            if (!string.IsNullOrWhiteSpace(danceIds))
            {
                dances = new List<string>(danceIds.Split(';'));
            }

            if (review.Merge.Count <= 0) return 0;

            var modified = dms.MergeCatalog(user, review.Merge, dances).ToList();

            var i = 0;

            foreach (var song in modified)
            {
                AdminMonitor.UpdateTask("UpdateService", i);
                UpdateSongAndServices(dms, song, crossRetry:true);
                i += 1;
            }

            dms.SaveSongs(modified);

            if (!string.IsNullOrEmpty(review.PlayList))
            {
                dms.UpdatePlayList(review.PlayList, review.Merge.Select(m => m.Left));
            }

            return modified.Count;
        }

        protected bool UpdateSongAndServices(DanceMusicCoreService dms, Song sd,
            ApplicationUser user = null, bool crossRetry = false)
        {
            // TODONEXT: Confirm that setting crossRetry to true prevents extra service lookup
            var changed = false;
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var service in MusicService.GetSearchableServices())
            {
                if (crossRetry && sd.Purchase != null && sd.Purchase.Contains(service.CID))
                {
                    break;
                }
                if (UpdateSongAndService(dms, sd, service, user))
                {
                    if (service.Id == ServiceType.Spotify)
                    {
                        MusicServiceManager.GetEchoData(dms, sd, user);
                        MusicServiceManager.GetSampleData(dms, sd, user);
                    }
                    changed = true;
                }
            }
            return changed;
        }

        protected bool UpdateSongAndService(
            DanceMusicCoreService dms, Song sd, MusicService service,
            ApplicationUser user = null)
        {
            var found = MatchSongAndService(sd, service);
            if (found.Count <= 0) return false;

            var edit = new Song(sd, dms);

            var tags = new TagList();
            foreach (var foundTrack in found)
            {
                var trackId = foundTrack.TrackId; //PurchaseRegion.FormatIdAndRegionInfo(foundTrack.TrackId, foundTrack.AvailableMarkets);
                UpdateMusicService(edit, MusicService.GetService(foundTrack.Service), foundTrack.Name, foundTrack.Album, foundTrack.Artist, trackId, foundTrack.CollectionId, foundTrack.AltId, foundTrack.Duration.ToString(), foundTrack.TrackNumber);
                if (foundTrack.Genres != null)
                {
                    tags = tags.Add(new TagList(dms.NormalizeTags(string.Join("|", foundTrack.Genres), "Music", true)));
                }
            }

            if (user != null)
            {
                tags = tags.Add(sd.GetUserTags(user.UserName));
            }
            else
            {
                user = service.ApplicationUser;
            }

            return dms.EditSong(user, sd, edit, new[] { new UserTag { Id = string.Empty, Tags = tags } });
        }


        protected IList<ServiceTrack> FindMusicServiceSong(Song song, MusicService service, bool clean = false, string title = null, string artist = null)
        {
            IList<ServiceTrack> tracks = null;
            try
            {
                FixupTitleArtist(song, clean, ref title, ref artist);
                tracks = MusicServiceManager.FindMusicServiceSong(song, service, clean, title, artist);

                if (tracks == null || tracks.Count == 0)
                {
                    ViewBag.Error = true;
                    ViewBag.Status = "No Matches Found";
                }
            }
            catch (WebException we)
            {
                ViewBag.Error = true;
                ViewBag.Status = we.Message;
                Trace.WriteLineIf(TraceLevels.General.TraceError, $"Failed '{we.Message}' on Song '{song}");
            }

            return tracks;
        }

        protected string DefaultServiceSearch(Song song, bool clean)
        {
            if (clean)
                return song.CleanTitle + " " + song.CleanArtist;

            return song.Title + " " + song.Artist;
        }

        private IList<ServiceTrack> MatchSongAndService(Song sd, MusicService service)
        {
            IList<ServiceTrack> found = new List<ServiceTrack>();
            var tracks = FindMusicServiceSong(sd, service);

            // First try the full title/artist
            if ((tracks == null || tracks.Count == 0) && !string.Equals(DefaultServiceSearch(sd, true), DefaultServiceSearch(sd, false)))
            {
                // Now try cleaned up title/artist (remove punctuation and stuff in parens/brackets)
                ViewBag.Status = null;
                ViewBag.Error = false;
                tracks = FindMusicServiceSong(sd, service, true);
            }

            if (tracks == null || tracks.Count <= 0) return found;

            // First filter out anything that's not a title-artist match (weak)
            tracks = sd.TitleArtistFilter(tracks);
            if (tracks.Count <= 0) return found;

            // Then check for exact album match if we don't have a tempo
            if (!sd.Length.HasValue)
            {
                foreach (var track in tracks.Where(track => sd.FindAlbum(track.Album, track.TrackNumber) != null))
                {
                    found.Add(track);
                    break;
                }
            }
            // If not exact album match and the song has a length, choose all albums with the same tempo (delta a few seconds)
            else
            {
                found = sd.DurationFilter(tracks, 6);
            }

            // If no album name or length match, choose the 'dominant' version of the title/artist match by clustering lengths
            //  Note that this degenerates to choosing a single album if that is what is available
            if (found.Count == 0 && !sd.HasRealAblums)
            {
                var track = Song.FindDominantTrack(tracks);
                if (track.Duration != null) found = Song.DurationFilter(tracks, track.Duration.Value, 6);
            }

            // Add back in any existing tracks for this service
            var existingIds = sd.GetPurchaseIds(service);
            foreach (var track in existingIds.Where(id => found.All(f => f.TrackId != id)))
            {
                var t = MusicServiceManager.GetMusicServiceTrack(track, service);
                if (t != null)
                {
                    found.Add(t);
                }
            }

            return found;
        }

        protected static Song UpdateMusicService(Song song, MusicService service, string name, string album, string artist, string trackId, string collectionId, string alternateId, string duration, int? trackNum)
        {
            // This is a very transitory object to hold the old values for a semi-automated edit
            var alt = new Song();

            if (!string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(song.Title))
            {
                alt.Title = song.Title;
                song.Title = name;
            }

            if (!string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(song.Artist))
            {
                alt.Artist = song.Artist;
                song.Artist = artist;
            }

            var ad = song.FindAlbum(album, trackNum);
            if (ad != null)
            {
                // If there is a match set up the new info next to the album
                var aidxM = song.Albums.IndexOf(ad);

                for (var aidx = 0; aidx < song.Albums.Count; aidx++)
                {
                    if (aidx == aidxM)
                    {
                        var adA = new AlbumDetails(ad);
                        if (string.IsNullOrWhiteSpace(ad.Name))
                        {
                            adA.Name = ad.Name;
                            ad.Name = album;
                        }

                        if (!ad.Track.HasValue || ad.Track.Value == 0)
                        {
                            adA.Track = ad.Track;
                            ad.Track = trackNum;
                        }
                        alt.Albums.Add(adA);
                    }
                    else
                    {
                        alt.Albums.Add(new AlbumDetails());
                    }
                }
            }
            else
            {
                // Otherwise just add an album
                ad = new AlbumDetails { Name = album, Track = trackNum, Index = song.GetNextAlbumIndex() };
                //song.Albums.Insert(0, ad);
                song.Albums.Add(ad);
            }
            UpdateMusicServicePurchase(ad, service, PurchaseType.Song, trackId, alternateId);
            if (collectionId != null)
            {
                UpdateMusicServicePurchase(ad, service, PurchaseType.Album, collectionId);
            }

            if ((!song.Length.HasValue || song.Length == 0) && !string.IsNullOrWhiteSpace(duration))
            {
                try
                {
                    var sd = new SongDuration(duration);

                    var length = decimal.ToInt32(sd.Length);
                    if (length > 9999)
                    {
                        length = 9999;
                    }

                    if (length != song.Length)
                    {
                        alt.Length = song.Length;
                        song.Length = length;
                    }
                }
                catch (ArgumentOutOfRangeException)
                {

                }
            }

            return alt;
        }

        private void FixupTitleArtist(Song song, bool clean, ref string title, ref string artist)
        {
            if (song != null && artist == null && title == null)
            {
                artist = clean ? song.CleanArtist : song.Artist;
                title = clean ? song.CleanTitle : song.Title;
            }

            ViewBag.SongArtist = artist;
            ViewBag.SongTitle = title;
        }

        private static void UpdateMusicServicePurchase(AlbumDetails ad, MusicService service, PurchaseType pt, string trackId, string alternateId = null)
        {
            // Don't update if there is alread a trackId
            var old = ad.GetPurchaseIdentifier(service.Id, pt);
            if (old != null && old.StartsWith(trackId))
            {
                return;
            }

            ad.SetPurchaseInfo(pt, service.Id, trackId);
            if (!string.IsNullOrWhiteSpace(alternateId))
            {
                ad.SetPurchaseInfo(pt, ServiceType.AMG, alternateId);
            }
        }

    }
}
