using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using DanceLibrary;
using m4d.Context;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNet.Identity.Owin;

namespace m4d.Controllers
{
    /// <summary>
    /// Base controller for dance music
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class DMController : Controller
    {
        public readonly string MusicTheme = "music";
        public readonly string ToolTheme = "tools";
        public readonly string BlogTheme = "blog";
        public readonly string AdminTheme = "admin";

        protected override void OnActionExecuting(System.Web.Mvc.ActionExecutingContext filterContext)
        {
            filterContext.HttpContext.Session["Workaround"] = 0;
        }

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
            var model = new ErrorModel { HttpStatusCode = (int)statusCode, Message=message, Exception = exception };

            Response.StatusCode = (int)statusCode;
            Response.TrySkipIisCustomErrors = true;

            return View("HttpError",model);
        }
        protected override ViewResult View(string viewName, string masterName, object model)
        {
            ViewBag.Theme = ThemeName;
            ViewBag.Help = HelpPage;
            return base.View(viewName, masterName, model);
        }

        protected DanceMusicService Database => _database ??
                                                (_database =
                                                    new DanceMusicService(HttpContext.GetOwinContext().Get<DanceMusicContext>(),
                                                        HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>()));

        private DanceMusicService _database;

        protected MusicServiceManager MusicServiceManager => _musicServiceManager ?? (_musicServiceManager = new MusicServiceManager());

        private MusicServiceManager _musicServiceManager;

        protected void ResetContext()
        {
            var temp = _database;
            _database = null;
            temp.Dispose();
        }

        protected DanceMusicContext Context => Database.Context as DanceMusicContext;

        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            protected set => _userManager = value;
        }
        private ApplicationUserManager _userManager;

        public static bool VerboseTelemetry { get; set; } = false;

        //// Used for XSRF protection when adding external logins
        //protected const string XsrfKey = "XsrfId";

        //protected IAuthenticationManager AuthenticationManager
        //{
        //    get
        //    {
        //        return HttpContext.GetOwinContext().Authentication;
        //    }
        //}

        //protected async Task<ExternalLoginInfo> GetExternalLoginInfoAsync()
        //{
        //    var userId = User.Identity.GetUserId();
        //    var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, userId);
        //    if (loginInfo == null)
        //    {
        //        return null;
        //    }

        //    if (loginInfo.Email == null)
        //    {
        //        var authResult = await AuthenticationManager.AuthenticateAsync(DefaultAuthenticationTypes.ExternalCookie);

        //        if (authResult != null && authResult.Identity != null && authResult.Identity.IsAuthenticated)
        //        {
        //            var claimsIdentity = authResult.Identity;
        //            var providerKeyClaim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

        //            var providerKey = providerKeyClaim.Value;
        //            var issuer = providerKeyClaim.Issuer;
        //            var name = claimsIdentity.FindFirstValue(ClaimTypes.Name);
        //            var emailAddress = claimsIdentity.FindFirstValue(ClaimTypes.Email);

        //            Trace.WriteLineIf(TraceLevels.General.TraceError,string.Format("providerKey={0};issuer={1};name={2};emailAddress={3}", providerKey, issuer, name, emailAddress));

        //            loginInfo.Email = emailAddress;
        //        }
        //    }

        //    if (loginInfo.Email == null)
        //    {
        //        loginInfo.Email = await UserManager.GetEmailAsync(userId);
        //    }

        //    return loginInfo;
        //}

        //protected void AddErrors(IdentityResult result)
        //{
        //    foreach (var error in result.Errors)
        //    {
        //        ModelState.AddModelError("", error);
        //    }
        //}

        //protected ActionResult RedirectToLocal(string returnUrl)
        //{
        //    if (Url.IsLocalUrl(returnUrl))
        //    {
        //        return Redirect(returnUrl);
        //    }
        //    return RedirectToAction("Index", "Home");
        //}

        public ActionResult CheckSpiders()
        {
            return SpiderManager.CheckBadSpiders(Request.UserAgent) ? View("BotWarning") : null;
        }

        protected void SaveSong(Song song)
        {
            Database.SaveSong(song);
        }

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


        public int CommitCatalog(DanceMusicService dms, Review review, string userName, string danceIds=null)
        {
            List<string> dances = null;
            if (!string.IsNullOrWhiteSpace(danceIds))
            {
                dances = new List<string>(danceIds.Split(';'));
            }

            if (review.Merge.Count <= 0) return 0;

            var modified = dms.MergeCatalog(userName, review.Merge, dances).ToList();

            var i = 0;

            foreach (var song in modified)
            {
                AdminMonitor.UpdateTask("UpdateService", i);
                UpdateSongAndServices(dms, song);
                i += 1;
            }

            dms.SaveSongs(modified);

            if (!string.IsNullOrEmpty(review.PlayList))
            {
                dms.UpdatePlayList(review.PlayList, review.Merge.Select(m => m.Left));
            }

            return modified.Count;
        }

        protected bool UpdateSongAndServices(DanceMusicService dms, Song sd, string user = null, bool crossRetry = false)
        {
            // TODONEXT: Music service search doesn't handle patching the genre, also revisit if we can guarantee that the 
            //  service ID that comes in is added to the list...
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

        protected bool UpdateSongAndService(DanceMusicService dms, Song sd, MusicService service, string user = null)
        {
            var found = MatchSongAndService(sd, service);
            if (found.Count <= 0) return false;

            var edit = new Song(sd, dms.DanceStats);

            var tags = new TagList();
            foreach (var foundTrack in found)
            {
                var trackId = PurchaseRegion.FormatIdAndRegionInfo(foundTrack.TrackId, foundTrack.AvailableMarkets);
                UpdateMusicService(edit, MusicService.GetService(foundTrack.Service), foundTrack.Name, foundTrack.Album, foundTrack.Artist, trackId, foundTrack.CollectionId, foundTrack.AltId, foundTrack.Duration.ToString(), foundTrack.TrackNumber);
                if (foundTrack.Genres != null)
                {
                    tags.Add(new TagList(dms.NormalizeTags(string.Join("|", foundTrack.Genres), "Music", true)));
                }
            }

            if (user != null)
            {
                tags = tags.Add(sd.GetUserTags(user));
            }
            else
            {
                user = service.User;
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
                Trace.WriteLineIf(TraceLevels.General.TraceError,$"Failed '{we.Message}' on Song '{song}");
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
            //  Note that this degenerates to chosing a single album if that is what is available
            if (found.Count == 0 && !sd.HasRealAblums)
            {
                var track = Song.FindDominantTrack(tracks);
                if (track.Duration != null) found = Song.DurationFilter(tracks, track.Duration.Value, 6);
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

        protected override void Dispose(bool disposing)
        {
            _database?.Dispose();
            base.Dispose(disposing);
        }
    }
}