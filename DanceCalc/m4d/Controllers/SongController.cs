using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using DanceLibrary;
using m4d.Utilities;
using m4d.ViewModels;
using m4dModels;
using PagedList;

namespace m4d.Controllers
{
    public class SimpleDance
    {
        public string Id { get; set; }
        public string Name { get; set; }
    };

    public class SongController : ContentController
    {
        public SongController()
        {
            HelpPage = "song-list";
        }
        public override string DefaultTheme => MusicTheme;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            object o;
            if (filterContext.ActionParameters.TryGetValue("filter",out o) && o == null)
            {
                o =  SongFilter.Default;
                filterContext.ActionParameters["filter"] = o;
            }

            if (o != null)
            {
                ViewBag.SongFilter = o;
            }

            base.OnActionExecuting(filterContext);
        }

        #region Commands

        [AllowAnonymous]
        public ActionResult Sample()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Search(string searchString, string dances, SongFilter filter)
        {
            if (string.IsNullOrWhiteSpace(searchString))
            {
                searchString = null;
            }
            if (!string.Equals(searchString, filter.SearchString))
            {
                filter.SearchString = searchString;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(dances))
            {
                dances = null;
            }

            if (!string.Equals(dances, filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                filter.Dances = dances;
                filter.Page = 1;

                if (string.IsNullOrWhiteSpace(filter.SortOrder))
                {
                    filter.SortOrder = "Dances";
                }
            }

            filter.Purchase = null;
            filter.TempoMin = null;
            filter.TempoMax = null;

            return DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult AzureSearch(string searchString, int page=1, string dances=null, SongFilter filter=null)
        {
            if (filter == null || filter.IsEmpty)
            {
                filter = SongFilter.AzureSimple;
            }

            if (string.IsNullOrWhiteSpace(dances))
            {
                dances = null;
            }

            if (dances != null && !string.Equals(dances, filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                filter.Dances = dances;
                filter.Page = 1;

                if (string.IsNullOrWhiteSpace(filter.SortOrder))
                {
                    filter.SortOrder = "Dances";
                }
            }

            filter.SearchString = searchString;

            if (page != 0)
                filter.Page = page;

            return DoAzureSearch(filter);
        }

        private ActionResult DoAzureSearch(SongFilter filter)
        {
            HelpPage = filter.IsSimple ? "simple-search" : "full-search";

            if (!filter.IsEmptyPaged && SpiderManager.CheckAnySpiders(Request.UserAgent))
            {
                return View("BotFilter", filter);
            }

            var results = Database.AzureSearch(filter, 25);
            BuildDanceList();
            ViewBag.SongFilter = filter;

            var songs = new StaticPagedList<SongBase>(results.Songs, results.CurrentPage, results.PageSize, (int)results.TotalCount);

            var dances = filter.DanceQuery.DanceIds.ToList();
            SetupLikes(results.Songs, dances.Count == 1 ? dances[0] : null);

            ReportSearch(filter);

            return View("azuresearch",songs);
        }

        //
        // GET: /Song/AdvancedSearchForm
        [AllowAnonymous]
        public ActionResult AdvancedSearchForm(SongFilter filter = null)
        {
            HelpPage = "advanced-search";
            BuildDanceList();
            return View();
        }

        //
        // Get: /Song/AdvancedSearch
        [AllowAnonymous]
        public ActionResult AdvancedSearch(string searchString = null, string dances = null, string tags = null, ICollection<string> services = null, decimal? tempoMin = null, decimal? tempoMax = null, string user=null, string sortOrder = null, string sortDirection = null, SongFilter filter = null)
        {
            if (filter == null)
            {
                filter = SongFilter.Default;
            }

            if (!filter.IsAdvanced)
            {
                filter.Action = "Advanced";
            }

            if (string.IsNullOrWhiteSpace(searchString))
            {
                searchString = null;
            }
            if (!string.Equals(searchString, filter.SearchString))
            {
                filter.SearchString = searchString;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(dances))
            {
                dances = null;
            }
            if (!string.Equals(dances, filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                filter.Dances = dances;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(tags))
            {
                tags = null;
            }

            if (!string.Equals(tags, filter.Tags, StringComparison.OrdinalIgnoreCase))
            {
                filter.Tags = tags;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(user))
            {
                user = null;
            }

            if (!string.Equals(user, filter.User, StringComparison.OrdinalIgnoreCase))
            {
                filter.User = user;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(sortOrder))
            {
                sortOrder = null;
            }
            else if (string.Equals(sortDirection,"Descending",StringComparison.OrdinalIgnoreCase))
            {
                sortOrder = sortOrder + "_desc";
            }

            if (!string.Equals(sortOrder, filter.SortOrder, StringComparison.OrdinalIgnoreCase))
            {
                filter.SortOrder = sortOrder;
                filter.Page = 1;
            }

            var purchase = string.Empty;
            if (services != null)
            {
                purchase = string.Concat(services);
            }

            if (filter.Purchase != purchase)
            {
                filter.Purchase = purchase;
                filter.Page = 1;
            }

            if (filter.TempoMin != tempoMin || filter.TempoMax != tempoMax)
            {
                filter.TempoMin = tempoMin;
                filter.TempoMax = tempoMax;
                filter.Page = 1;
            }

            var uq = filter.UserQuery;
            if (!uq.IsEmpty && uq.IsAnonymous)
            {
                return RedirectToAction("SignIn", "Account", new { ReturnUrl = "/song/advancedsearchform?filter="+filter });
            }

            return filter.IsAzure ? DoAzureSearch(filter) : DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult Sort(string sortOrder, SongFilter filter)
        {
            filter.SortOrder = SongSort.DoSort(sortOrder, filter.SortOrder);

            return filter.IsAzure ? DoAzureSearch(filter) : DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult FilterUser(string user, SongFilter filter)
        {
            filter.User = string.IsNullOrWhiteSpace(user) ? null : user;
            return filter.IsAzure ? DoAzureSearch(filter) : DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult FilterService(ICollection<string> services, SongFilter filter)
        {
            var purchase = string.Empty;
            if (services != null)
            {
                purchase = string.Concat(services);
            }

            if (filter.Purchase == purchase) return DoIndex(filter);

            filter.Purchase = purchase;
            filter.Page = 1;
            return filter.IsAzure ? DoAzureSearch(filter) : DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult FilterTempo(decimal? tempoMin, decimal? tempoMax, SongFilter filter)
        {
            if (filter.TempoMin == tempoMin && filter.TempoMax == tempoMax)
                return DoIndex(filter);

            filter.TempoMin = tempoMin;
            filter.TempoMax = tempoMax;
            filter.Page = 1;

            return filter.IsAzure ? DoAzureSearch(filter) : DoIndex(filter);
        }

        //
        // GET: /Index/
        [AllowAnonymous]
        public ActionResult Index(int? page, string purchase, SongFilter filter)
        {
            if (User.Identity.IsAuthenticated && filter.IsEmpty)
            {
                filter.User = new UserQuery(User.Identity.Name, false, false).Query;
            }

            if (page.HasValue)
            {
                filter.Page = page;
            }

            if (!string.IsNullOrWhiteSpace(purchase))
            {
                filter.Purchase = purchase;
            }

            return DoIndex(filter);
        }

        //
        // GET: /AdvancedIndex/
        [AllowAnonymous]
        public ActionResult Advanced(int? page, string purchase, SongFilter filter)
        {
            return Index(page, purchase, filter);
        }

        [AllowAnonymous]
        public ActionResult Tags(string tags, SongFilter filter)
        {
            filter.Tags = null;
            filter.Page = null;

            if (string.IsNullOrWhiteSpace(tags))
                return DoIndex(filter);

            var list = new TagList(tags).AddMissingQualifier('+');
            filter.Tags = list.ToString();

            return filter.IsAzure ? DoAzureSearch(filter) : DoIndex(filter);
        }

        [AllowAnonymous]
        public ActionResult AddTags(string tags, SongFilter filter)
        {
            var add = new TagList(tags);
            var old = new TagList(filter.Tags);

            // First remove any tags from the old list
            old = old.Subtract(add);
            var ret = old.Add(add.AddMissingQualifier('+'));
            filter.Tags = ret.ToString();
            filter.Page = null;

            return filter.IsAzure ? DoAzureSearch(filter) : DoIndex( filter);
        }

        [AllowAnonymous]
        public ActionResult RemoveTags(string tags, SongFilter filter)
        {
            var sub = new TagList(tags);
            var old = new TagList(filter.Tags);
            var ret = old.Subtract(sub);
            filter.Tags = ret.ToString();
            filter.Page = null;

            return filter.IsAzure ? DoAzureSearch(filter) : DoIndex(filter);
        }

        //
        // GET: /Song/Details/5

        [AllowAnonymous]
        public ActionResult Details(Guid? id = null, SongFilter filter = null)
        {
            var spider = CheckSpiders();
            if (spider != null) return spider;

            var gid = id ?? Guid.Empty;
            var song = Database.FindSongDetails(gid, User.Identity.Name, User.IsInRole("showDiagnostics"));
            if (song == null)
            {
                song = Database.FindMergedSong(gid, User.Identity.Name);
                return song != null ? 
                    RedirectToActionPermanent("details", new {id=song.SongId.ToString(), filter}) : 
                    ReturnError(HttpStatusCode.NotFound, $"The song with id = {gid} has been deleted.");
            }

            HelpPage = "song-details";
            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            ViewBag.DanceList = GetDancesSingle(Database);
            return View(song);
        }

        [AllowAnonymous]
        public ActionResult Album(string title)
        {
            var spider = CheckSpiders();
            if (spider != null) return spider;

            AlbumViewModel model = null;

            if (!string.IsNullOrWhiteSpace(title))
            {
                model = AlbumViewModel.Create(title, Database);
            }

            if (model == null)
                return ReturnError(HttpStatusCode.NotFound, $"Album '{title}' not found.");

            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            return View("Album", model);
        }

        [AllowAnonymous]
        public ActionResult Artist(string name)
        {
            var spider = CheckSpiders();
            if (spider != null) return spider;

            ArtistViewModel model = null;

            if (!string.IsNullOrWhiteSpace(name))
            {
                
                model = ArtistViewModel.Create(name, User.IsInRole(DanceMusicService.EditRole) ? DanceMusicService.CruftFilter.AllCruft : DanceMusicService.CruftFilter.NoCruft, Database);
            }

            if (model == null)
                return ReturnError(HttpStatusCode.NotFound, $"Album '{name}' not found.");

            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            return View("Artist", model);
        }


        //
        // GET: /Song/CreateI
        [Authorize(Roles = "canEdit")] 
        public ActionResult Create(string title=null, string artist=null, decimal? tempo = null, int? length=null, string album=null, int? track=null, string service = null, string purchase = null, SongFilter filter = null)
        {
            IList<AlbumDetails> adl = null;
            if (album != null)
            {
                var ad = new AlbumDetails
                {
                    Name = album,
                    Track = track
                };
                if (service != null)
                {
                    var ms = MusicService.GetService(service[0]);
                    ad.SetPurchaseInfo(PurchaseType.Song,ms.Id,purchase);
                }
                adl = new List<AlbumDetails> { ad };
            }

            var sd = new SongDetails(title,artist,tempo,length,adl);
            SetupEditViewBag(tempo);
            return View(sd);
        }

        //
        // POST: /Song/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult Create(SongDetails song, string userTags, SongFilter filter = null)
        {
            if (ModelState.IsValid)
            {
                var user = Database.FindUser(User.Identity.Name);
                var jt = JTags.FromJson(userTags);

                //song.UpdateDanceRatings(addDances, SongBase.DanceRatingCreate);
                //var tags = new TagList(editTags).Add(new TagList(SongBase.TagsFromDances(addDances)));
                //song.AddTags(tags.ToString(), user, Database, song);
                var newSong = Database.CreateSongDetails(user, song, jt.ToUserTags());

                if (newSong != null)
                {
                    Database.SaveChanges();
                }

                ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
                ViewBag.DanceList = GetDancesSingle(Database);
                return View("details", newSong);
            }

            ViewBag.DanceList = GetDancesSingle(Database);

            // Clean out empty albums
            for (var i = 0; i < song.Albums.Count; )
            {
                if (string.IsNullOrWhiteSpace(song.Albums[i].Name))
                {
                    song.Albums.RemoveAt(i);
                }
                else
                {
                    i += 1;
                }
            }

            return View(song);
        }

        //
        // GET: /Song/Edit/5
        [Authorize(Roles = "canEdit")] 
        public ActionResult Edit(Guid? id = null, decimal? tempo = null, SongFilter filter = null)
        {
            var song = Database.FindSongDetails(id ?? Guid.Empty, User.Identity.Name, User.IsInRole("showDiagnostics"));
            if (song == null)
            {
                return ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.");
            }

            SetupEditViewBag(tempo);

            return View(song);
        }

        private void SetupEditViewBag(decimal? tempo = null)
        {
            if (tempo.HasValue)
            {
                ViewBag.paramNewTempo = tempo.Value;
            }

            ViewBag.paramShowMPM = true;
            ViewBag.paramShowBPM = true;

            ViewBag.DanceList = GetDancesSingle(Database);

            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
        }
        //
        // POST: /Song/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        //public ActionResult Edit([ModelBinder(typeof(m4d.Utilities.SongBinder))]SongDetails song, List<string> addDances, List<string> remDances, string filter = null)
        public ActionResult Edit(SongDetails song, string userTags, SongFilter filter = null)
        {
            if (ModelState.IsValid)
            {
                var user = Database.FindUser(User.Identity.Name);
                var jt = JTags.FromJson(userTags);

                var edit = Database.EditSong(user, song, jt.ToUserTags());

                // ReSharper disable once InvertIf
                if (edit != null)
                {
                    Database.SaveChanges();

                    // TODO: Need to figure out a cleaner way to make editsong return a fully hydrated songdetails before save happens (for batch mode)
                    //  Possibly get SongDetails constructor to hydrate CurrentUser tags from properties collection rather than going to the db?
                    edit = Database.FindSongDetails(edit.SongId, user.UserName);

                    ViewBag.BackAction = "Index";
                    ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
                    ViewBag.DanceList = GetDancesSingle(Database);
                    return View("details", edit);
                }

                return RedirectToAction("Index", new {filter });
            }
            var errors =  ModelState.SelectMany(x => x.Value.Errors.Select(z => z.Exception));
            ViewBag.Errors = errors;

            if (TraceLevels.General.TraceError)
            {
                foreach (var error in errors)
                {
                    Trace.WriteLine(error.Message);
                }
            }

            // Add back in the danceratings
            // TODO: This almost certainly doesn't preserve edits...
            SetupEditViewBag();

            // Clean out empty albums
            for (var i = 0; i < song.Albums.Count;)
            {
                if (string.IsNullOrWhiteSpace(song.Albums[i].Name))
                {
                    song.Albums.RemoveAt(i);
                }
                else
                {
                    i += 1;
                }
            }

            return View(song);
        }

        //
        // GET: /Song/Delete/5
        [Authorize(Roles = "canEdit")] 
        public ActionResult Delete(Guid id, SongFilter filter = null)
        {
            var song = Database.Songs.Find(id);
            return song == null ? ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.") : View(song);
        }

        //
        // POST: /Song/Delete/5

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        public ActionResult DeleteConfirmed(Guid id, SongFilter filter = null)
        {
            var song = Database.Songs.Find(id);
            var userName = User.Identity.Name;
            var user = Database.FindUser(userName);
            Database.DeleteSong(user,song);
            return RedirectToAction("Index", new {filter });
        }


        //
        // POST: /Song/AdminEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult AdminEdit(Guid songId, string properties, SongFilter filter=null)
        {
            var song = Database.FindSong(songId);

            if (!ModelState.IsValid || !Database.AdminEditSong(song, properties))
                return RedirectToAction("Index", new {filter});

            Database.SaveChanges();

            var user = Database.FindUser(User.Identity.Name);
            var sd = Database.FindSongDetails(songId, user.UserName);

            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            ViewBag.DanceList = GetDancesSingle(Database);
            return View("details", sd);
        }

        public ActionResult CleanupAlbums(Guid id, SongFilter filter = null)
        {
            var user = Database.FindUser(User.Identity.Name);

            if (Database.CleanupAlbums(user, Database.FindSong(id)) != 0)
            {
                Database.SaveChanges();
            }

            return RedirectToAction("Details", new { id, filter });
        }


        //
        // POST: /Song/Delete/5

        [HttpPost, ActionName("UndoUserChanges")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult UndoUserChanges(Guid id, string userName = null, SongFilter filter = null)
        {
            if (userName == null)
            {
                userName = User.Identity.Name;
            }
            else if (!User.IsInRole("showDiagnostics"))
            {
                throw new HttpException((int)HttpStatusCode.Forbidden,"You don't have permission to modify other user's changes.");
            }
            
            var user = Database.FindUser(userName);
            Database.UndoUserChanges(user, id);
            return RedirectToAction("Details", new { id, filter });
        }

        //
        // Merge: /Song/MergeCandidates
        [Authorize(Roles = "canEdit")]
        public ActionResult MergeCandidates(int? page, int? level, bool? autoCommit, SongFilter filter)
        {
            filter.Action = "MergeCandidates";

            BuildDanceList();

            if (page.HasValue)
            {
                filter.Page = page;
            }

            if (level.HasValue)
            {
                filter.Level = level;
            }

            var songs = Database.FindMergeCandidates(autoCommit == true ? 10000 : 500, filter.Level ?? 1);

            if (autoCommit.HasValue && autoCommit.Value)
            {
                songs = AutoMerge(songs,filter.Level??1);
            }

            ViewBag.ShowLength = true;

            return View("Index", songs.ToPagedList(filter.Page ?? 1, 25));
        }

        //
        // GET: /Song/UpdateRatings/5
        [Authorize(Roles = "canEdit")]
        public ActionResult UpdateRatings(Guid id, SongFilter filter = null)
        {
            var song = Database.Songs.Find(id);
            if (song == null)
            {
                return ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.");
            }
            song.SetRatingsFromProperties();
            Database.SaveChanges();

            HelpPage = "song-details";
            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            ViewBag.DanceList = GetDancesSingle(Database);

            return View("Details", Database.FindSongDetails(song.SongId));
        }


        //
        // BulkEdit: /Song/BulkEdit
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult BulkEdit(Guid[] selectedSongs, string action, SongFilter filter = null)
        {
            var songs = from s in Database.Songs
                        where selectedSongs.Contains(s.SongId)
                        select s;

            switch (action)
            {
                case "Merge":
                    return Merge(songs);
                case "Delete":
                    return Delete(songs,filter);
                case "CleanupAlbums":
                    return CleanupAlbums(songs);
                default:
                    return View("Index");
            }

        }

        //
        // Merge: /Song/Merge
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult MergeResults(string songIds, SongFilter filter = null)
        {
            // See if we can do the actual merge and then return the song details page...
            var ids = songIds.Split(',').Select(Guid.Parse).ToList();

            var songs = from s in Database.Songs
                        where ids.Contains(s.SongId)
                        select s;
            var songList = songs.ToList();

            // Create a merged version of the song (and commit to DB)

            // Get the logged in user
            var userName = User.Identity.Name;
            var user = Database.FindUser(userName);

            var song = Database.MergeSongs(user, songList, 
                ResolveStringField(SongBase.TitleField, songList, Request.Form),
                ResolveStringField(SongBase.ArtistField, songList, Request.Form),
                ResolveDecimalField(SongBase.TempoField, songList, Request.Form),
                ResolveIntField(SongBase.LengthField, songList, Request.Form),
                Request.Form[SongBase.AlbumListField], new HashSet<string>(Request.Form.AllKeys));

            Database.SaveChanges();

            SongCounts.ClearCache();

            ViewBag.BackAction = "MergeCandidates";
            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            ViewBag.DanceList = GetDancesSingle(Database);
            return View("details",Database.FindSongDetails(song.SongId));
        }

        /// <summary>
        /// Batch up searching a music service
        /// </summary>
        /// <param name="type">Music service type (currently X=Groove,A=Amazon,S=Spotify,I=ITunes)</param>
        /// <param name="options">May be more complex in future - currently Rn where n is retyr level</param>
        /// <param name="filter">Standard filter for song list</param>
        /// <param name="count">Number of songs to try, 1 is special cased as a user verified single entry</param>
        /// <param name="pageSize">Number of song to process per query</param>
        /// <returns></returns>
        [Authorize(Roles = "canEdit")]
        public ActionResult BatchMusicService(string type= "X", string options = null, SongFilter filter=null, int count = 1, int pageSize = 1000)
        {
            try
            {
                StartAdminTask("BatchMusicService");
                AdminMonitor.UpdateTask("BatchMusicService");

                MusicService service = null;
                if (type != "-")
                {
                    service = MusicService.GetService(type);
                    // ReSharper disable once PossibleNullReferenceException
                    filter.Purchase = "!" + type;
                    if (service == null)
                    {
                        throw new ArgumentOutOfRangeException(nameof(type));
                    }
                }

                ViewBag.BatchName = "BatchMusicService";
                ViewBag.SearchType = type;
                ViewBag.Options = options;
                ViewBag.Error = false;

                var tried = 0;
                var skipped = 0;

                var retryLevel = -1;
                var cruftFilter = DanceMusicService.CruftFilter.AllCruft;
                var skipExisting = true;
                var skipVisited = false;

                // May do more options in future
                while (!string.IsNullOrWhiteSpace(options) && options.Length > 0)
                {
                    var o = options[0];
                    options = options.Substring(1);
                    switch (o)
                    {
                        case 'R':
                            if (int.TryParse(options.Substring(1), out retryLevel))
                                options = options.Substring(retryLevel.ToString().Length);
                            break;
                        case 'C':
                            cruftFilter = DanceMusicService.CruftFilter.NoPublishers;
                            break;
                        case 'E':
                            skipExisting = false;
                            break;
                        case 'V':
                            skipVisited = true;
                            break;
                    }

                }

                var users = new Dictionary<char, ApplicationUser>();

                var failed = new List<Song>();
                var succeeded = new List<SongDetails>();

                Context.TrackChanges(false);

                var page = 0;
                var done = false;

                while (!done)
                {
                    AdminMonitor.UpdateTask("BuildSongList", page);
                    var songsQ = Database.BuildSongList(filter, cruftFilter);
                    if (skipVisited)
                    {
                        songsQ = songsQ.Where(s => s.SongProperties.All(p => p.Name != SongBase.FailedLookup));
                    }
                    var songs = songsQ.Skip(page * pageSize).Take(pageSize).ToList();
                    var processed = 0;
                    foreach (var song in songs)
                    {
                        AdminMonitor.UpdateTask($"Processing ({succeeded.Count})", processed);

                        processed += 1;
                        // First check to see if we've already failed a search and at what level
                        //  failLeve is the LOWEST failure code or -1 if none

                        var failLevel = -1;
                        var fail =
                            song.OrderedProperties.FirstOrDefault(
                                p => p.Name == SongBase.FailedLookup && p.Value.StartsWith(type));
                        if (fail?.Value != null && fail.Value.Length > 2)
                        {
                            int.TryParse(fail.Value.Substring(2), out failLevel);
                        }

                        if (failLevel == retryLevel || count == 1)
                        {
                            var sd = new SongDetails(song);

                            // Something of a kludge - we're goint to make type ='-' && failcode == 0 mean that we've tried 
                            //  the multi-service lookup...
                            var failcode = service == null ? 0 : -1;

                            SongDetails add = null;
                            if (service == null)
                            {
                                foreach (
                                    var addT in
                                        MusicService.GetSearchableServices()
                                            .Where(st => !skipExisting || !(song.Purchase ?? "").Contains(st.CID))
                                            .Select(serviceT => UpdateSongAndService(sd, serviceT, users))
                                            .Where(addT => addT != null))
                                {
                                    add = addT;
                                }
                            }
                            else
                            {
                                add = UpdateSongAndService(sd, service, users);
                            }

                            if (add != null)
                            {
                                succeeded.Add(add);
                                tried += 1;
                            }
                            else if (failcode == -1)
                            {
                                failcode = 0;
                            }

                            //  Add all failures to the list and increment tried
                            if (failcode >= 0)
                            {
                                // Only add in a new failed code to the DB if the code
                                // is higher than the previous code
                                if (failcode > failLevel)
                                {
                                    var sp = Database.SongProperties.Create();
                                    sp.Name = SongBase.FailedLookup;
                                    sp.Value = type + ":" + failcode.ToString();
                                    song.SongProperties.Add(sp);
                                }
                                if (service != null)
                                {
                                    failed.Add(song);
                                    tried += 1;
                                }
                            }
                        }
                        else
                        {
                            skipped += 1;
                        }

                        if (tried > count)
                            break;

                        if ((tried + 1)%25 != 0) continue;

                        Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{tried} songs tried.");
                        Context.CheckpointChanges();
                    }

                    page += 1;
                    if (processed < pageSize)
                    {
                        done = true;
                    }
                    Context.CheckpointSongs();
                }

                if (failed.Count + succeeded.Count > 0)
                {
                    Context.TrackChanges(true);
                }

                ViewBag.Completed = tried <= count;
                ViewBag.Failed = failed;
                ViewBag.Succeeded = succeeded;
                ViewBag.Skipped = skipped;

                AdminMonitor.CompleteTask(true, $"BatchMusicService: Completed={tried<=count}, Succeeded={succeeded.Count} - ({string.Join(",",succeeded.Select(s => s.SongId))}), Failed={failed.Count} - ({string.Join(",", failed.Select(s => s.SongId))}), Skipped={skipped}");

                return View();
            }
            catch (Exception e)
            {
                return FailAdminTask($"BatchMusicService: {e.Message}", e);
            }
        }

        // GET: /Song/MusicServiceSearch/5?search=name
        [Authorize(Roles = "canEdit")]
        public ActionResult MusicServiceSearch(Guid? id = null, string type="X", string title = null, string artist = null, SongFilter filter=null)
        {
            var song = Database.FindSongDetails(id??Guid.Empty);
            if (song == null)
            {
                return ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.");
            }

            var service = MusicService.GetService(type);
            if (service == null)
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            var view = new ServiceSearchResults { ServiceType = type, Song = song };

            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            ViewBag.SongTitle = title;
            ViewBag.SongArtist = artist;
            ViewBag.Type = type;
            ViewBag.Error = false;

            view.Tracks = FindMusicServiceSong(song, service, false, title, artist);

            return View(view);
        }

        // ChooseMusicService: /Song/ChooseMusicService
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult ChooseMusicService(Guid songId, string type, string name, string album, string artist, string trackId, string collectionId, string alternateId, string duration, string genre, int? trackNum, SongFilter filter)
        {
            var service = MusicService.GetService(type);
            if (service == null)
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            var song = Database.FindSongDetails(songId, User.Identity.Name);
            if (song == null)
            {
                return ReturnError(HttpStatusCode.NotFound, $"The song with id = {songId} has been deleted.");
            }

            var user = Database.FindUser(User.Identity.Name);
            var alt = UpdateMusicService(song, service, name, album, artist, trackId, collectionId, alternateId, duration, trackNum);
            song.AddTags(Database.NormalizeTags(genre, "Music"), user, Database, song);

            ViewBag.OldSong = alt;

            return View("Edit", song);
        }

        // CleanMusicServices: /Song/CleanMusicServices
        [Authorize(Roles = "canEdit")]
        public ActionResult CleanMusicServices(Guid id, SongFilter filter = null)
        {
            var song = Database.FindSong(id, User.Identity.Name);
            if (song == null)
            {
                return ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.");
            }

            if (CleanMusicServiceSong(song))
            {
                Database.SaveChanges();
            }

            HelpPage = "song-details";
            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            ViewBag.DanceList = GetDancesSingle(Database);
            return View("Details", Database.FindSongDetails(id));
        }

        [Authorize(Roles = "canEdit")]
        public ActionResult BatchCleanService(SongFilter filter = null, int count = 1)
        {
            try
            {
                StartAdminTask("BatchCleanService");
                AdminMonitor.UpdateTask("BatchCleanService");

                var failed = new List<SongBase>();
                var succeeded = new List<SongBase>();

                Context.TrackChanges(false);

                var page = 0;
                var tried = 0;
                var done = false;

                while (!done)
                {
                    AdminMonitor.UpdateTask("BuildSongList", page);
                    var songs = Database.BuildSongList(filter).Skip(page * 1000).Take(1000).ToList();
                    var processed = 0;
                    var modified = false;
                    foreach (var song in songs)
                    {
                        AdminMonitor.UpdateTask("Processing", processed);

                        processed += 1;
                        tried += 1;
                        if (CleanMusicServiceSong(song))
                        {
                            succeeded.Add(Database.FindSongDetails(song.SongId));
                            modified = true;
                        }
                        else
                        {
                            failed.Add(song);
                        }

                        if (tried > count)
                            break;

                        if ((tried + 1) % 25 != 0) continue;

                        Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{tried} songs tried.");
                        Context.CheckpointChanges();
                    }

                    page += 1;
                    if (processed < 1000)
                    {
                        done = true;
                    }
                    if (!modified)
                    {
                        Context.CheckpointSongs();
                    }
                }

                if (failed.Count + succeeded.Count > 0)
                {
                    Context.TrackChanges(true);
                }

                ViewBag.Completed = tried <= count;
                ViewBag.Failed = failed;
                ViewBag.Succeeded = succeeded;

                AdminMonitor.CompleteTask(true, $"BatchMusicService: Completed={tried <= count}, Succeeded={succeeded.Count} - ({string.Join(",", succeeded.Select(s => s.SongId))}), Failed={failed.Count} - ({string.Join(",", failed.Select(s => s.SongId))})");

                return View("BatchMusicService");
            }
            catch (Exception e)
            {
                return FailAdminTask($"BatchMusicService: {e.Message}", e);
            }
        }


        [Authorize(Roles = "canEdit")]
        public ActionResult BatchSamples(string options = null, SongFilter filter = null, int count = 1, int pageSize = 1000)
        {
            try
            {
                StartAdminTask("BatchSamples");
                AdminMonitor.UpdateTask("BatchSamples");

                ViewBag.BatchName = "BatchSamples";
                ViewBag.Options = options;
                ViewBag.Error = false;

                var tried = 0;
                var skipped = 0;

                if (filter == null)
                {
                    filter = new SongFilter();
                }
                filter.Purchase = "IS";

                //var skipExisting = true;

                var failed = new List<Song>();
                var succeeded = new List<SongDetails>();

                Context.TrackChanges(false);

                var page = 0;
                var done = false;

                var spotify = MusicService.GetService(ServiceType.Spotify);
                var itunes = MusicService.GetService(ServiceType.ITunes);
                var user = Database.FindUser("batch-s");
                Debug.Assert(user != null);

                while (!done)
                {
                    AdminMonitor.UpdateTask("BuildPage", page);
                    var songsQ = Database.BuildSongList(filter);
                    //if (skipExisting)
                    //{
                        songsQ = songsQ.Where(s => s.Sample == null);
                    //}

                    if (TraceLevels.General.TraceVerbose)
                    {
                        var c = songsQ.Count();
                        Trace.WriteLine($"Candidates for Sample lookup = {c}");
                    }
                    var songs = songsQ.Skip(page * pageSize).Take(pageSize).ToList();
                    var processed = 0;
                    foreach (var song in songs)
                    {
                        AdminMonitor.UpdateTask($"Processing ({succeeded.Count})", processed);

                        processed += 1;

                        var sd = new SongDetails(song);

                        ServiceTrack track = null;
                        // First try Spotify
                        var ids = sd.GetPurchaseIds(spotify);
                        foreach (var id in ids)
                        {
                            string[] regions;
                            var idt = PurchaseRegion.ParseIdAndRegionInfo(id, out regions);
                            track = MusicServiceManager.GetMusicServiceTrack(idt, spotify);
                            if (track?.SampleUrl != null)
                                break;
                        }

                        if (track == null)
                        {
                            // If spotify failed, try itunes
                            ids = sd.GetPurchaseIds(itunes);
                            foreach (var id in ids)
                            {
                                track = MusicServiceManager.GetMusicServiceTrack(id, itunes);
                                if (track?.SampleUrl != null)
                                    break;
                            }
                        }
                        tried += 1;
                        if (track?.SampleUrl == null)
                        {
                            sd.Sample = ".";
                            sd = Database.EditSong(user, sd);
                            if (sd == null)
                            {
                                skipped += 1;
                            }
                            else
                            {
                                failed.Add(song);
                            }

                        }
                        else
                        {
                            sd.Sample = track.SampleUrl;
                            sd = Database.EditSong(user, sd);
                            if (sd == null)
                            {
                                skipped += 1;
                            }
                            else
                            {
                                succeeded.Add(sd);
                            }
                        }
                        if (tried > count)
                            break;

                        if ((tried + 1) % 100 != 0) continue;

                        Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{tried} songs tried.");
                        Context.CheckpointChanges();
                    }

                    page += 1;
                    if (processed < pageSize)
                    {
                        done = true;
                    }
                    Context.CheckpointSongs();
                }

                if (failed.Count + succeeded.Count > 0)
                {
                    Context.TrackChanges(true);
                }

                ViewBag.Completed = tried <= count;
                ViewBag.Failed = failed;
                ViewBag.Succeeded = succeeded;
                ViewBag.Skipped = skipped;

                AdminMonitor.CompleteTask(true, $"BatchSample: Completed={tried <= count}, Succeeded={succeeded.Count} - ({string.Join(",", succeeded.Select(s => s.SongId))}), Failed={failed.Count} - ({string.Join(",", failed.Select(s => s.SongId))}), Skipped={skipped}");

                return View("BatchMusicService");
            }
            catch (Exception e)
            {
                return FailAdminTask($"BatchSample: {e.Message}", e);
            }
        }

        [Authorize(Roles = "canEdit")]
        public ActionResult BatchEchoNest(SongFilter filter = null, string options = null, int count = 1, int pageSize = 1000)
        {
            try
            {
                StartAdminTask("BatchEchoNest");
                AdminMonitor.UpdateTask("BatchEchoNest");
                var tried = 0;
                var skipped = 0;

                var failed = new List<SongBase>();
                var succeeded = new List<SongBase>();

                Context.TrackChanges(false);

                var page = 0;
                var done = false;

                if (filter == null)
                {
                    filter = new SongFilter();
                }
                filter.Purchase = "S";

                var service = MusicService.GetService(ServiceType.Spotify);
                var user = Database.FindUser("batch-e");

                var skipTempo = options != null && options.Contains("T");

                while (!done)
                {
                    AdminMonitor.UpdateTask("BuildPage", page);

                    var sq = Database.BuildSongList(filter).Where(s => s.Danceability == null);
                    if (skipTempo)
                    {
                        sq = sq.Where(s => s.Tempo == null);
                    }

                    var songs = sq.Skip(page * pageSize).Take(pageSize).ToList();
                    var processed = 0;
                    foreach (var song in songs)
                    {
                        AdminMonitor.UpdateTask($"Processing ({succeeded.Count})", processed);

                        tried += 1;
                        processed += 1;

                        if (song.Purchase == null)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Bad Purchase: {song}");
                            skipped += 1;
                            continue;
                        }
                        if (song.Purchase == null || !song.Purchase.Contains('S'))
                        {
                            skipped += 1;
                            continue;
                        }

                        var sd = new SongDetails(song);
                        var ids = sd.GetPurchaseIds(service);

                        EchoTrack track = null;
                        foreach (var id in ids)
                        {
                            string[] regions;
                            var idt = PurchaseRegion.ParseIdAndRegionInfo(id, out regions);
                            track = MusicServiceManager.LookupEchoTrack(idt);
                            if (track != null)
                                break;
                        }

                        if (track == null)
                        {
                            song.Danceability = float.NaN;
                            if (Database.EditSong(user, sd, null, false) != null)
                            {
                                failed.Add(song);
                            }
                        }
                        else
                        {
                            if (track.BeatsPerMinute != null)
                            {
                                sd.Tempo = track.BeatsPerMinute;
                            }
                            if (track.Danceability != null)
                            {
                                sd.Danceability = track.Danceability;
                            }
                            if (track.Energy != null)
                            {
                                sd.Energy = track.Energy;
                            }
                            if (track.Valence != null)
                            {
                                sd.Valence = track.Valence;
                            }
                            UserTag[] tags = null;
                            var meter = track.Meter;
                            if (meter != null)
                            {
                                tags = new[]
                                {
                                new UserTag
                                {
                                    Id = string.Empty,
                                    Tags = new TagList($"{meter}:Tempo")
                                }
                            };
                            }

                            if (Database.EditSong(user, sd, tags, false) != null)
                            {
                                succeeded.Add(song);
                            }
                        }

                        // TODO: Decide if we want to tromp other tempi
                        //if (track?.BeatsPerMinute == null || (track.BeatsPerMinute == song.Tempo) ||
                        //    (sd.Tempo.HasValue && Math.Abs(track.BeatsPerMinute.Value - sd.Tempo.Value) > 5))
                        //{
                        //    skipped += 1;
                        //    continue;
                        //}


                        if (tried > count)
                            break;

                        if ((tried + 1) % 100 != 0) continue;

                        Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{tried} songs tried.");
                        Context.CheckpointChanges();
                    }

                    page += 1;
                    if (processed < pageSize)
                    {
                        done = true;
                    }
                    Context.CheckpointSongs();
                }

                if (failed.Count + succeeded.Count > 0)
                {
                    Context.TrackChanges(true);
                }

                ViewBag.BatchName = "BatchEchoNest";
                ViewBag.SearchType = null;
                ViewBag.Options = null;
                ViewBag.Completed = tried <= count;
                ViewBag.Failed = failed;
                ViewBag.Succeeded = succeeded;
                ViewBag.Skipped = skipped;

                AdminMonitor.CompleteTask(true, $"BatchEchonest: Completed={tried <= count}, Succeeded={succeeded.Count} - ({string.Join(",", succeeded.Select(s => s.SongId))}), Failed={failed.Count} - ({string.Join(",", failed.Select(s => s.SongId))}), Skipped={skipped}");

                return View("BatchMusicService");
            }
            catch (Exception e)
            {
                return FailAdminTask($"BatchEchoNext: {e.Message}", e);
            }

        }

        #endregion

        #region General Utilities
        static public IEnumerable<SelectListItem> GetDancesSingle(DanceMusicService dms)
        {
            var counts = SongCounts.GetFlatSongCounts(dms);

            var dances = new List<SelectListItem>(counts.Count)
            {
                new SelectListItem {Value = string.Empty, Text = string.Empty, Selected = true}
            };

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var cnt in counts.Where(c => c.SongCount > 0))
            {
                dances.Add(new SelectListItem { Value = cnt.DanceId, Text = cnt.DanceName, Selected = false });
            }
            return dances;
        }

        private ActionResult Delete(IEnumerable<Song> songs, SongFilter filter)
        {
            var user = Database.FindUser(User.Identity.Name);

            foreach (var song in songs)
            {
                Database.DeleteSong(user, song);
            }

            return RedirectToAction("Index", new { filter });
        }

        #endregion

        #region Index
        private ActionResult DoIndex(SongFilter filter)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceVerbose,
                $"Entering Song.Index: dances='{filter.Dances}',sortOrder='{filter.SortOrder}',searchString='{filter.SearchString}'");

            var spider = CheckSpiders();
            if (spider != null) return spider;

            if (!filter.IsEmptyPaged && SpiderManager.CheckAnySpiders(Request.UserAgent))
            {
                return View("BotFilter", filter);
            }


            var songs = Database.BuildSongList(filter, HttpContext.User.IsInRole(DanceMusicService.EditRole) ? DanceMusicService.CruftFilter.AllCruft : DanceMusicService.CruftFilter.NoCruft);
            BuildDanceList();

            var list = songs.ToPagedList(filter.Page ?? 1, 25);

            var dances = filter.DanceQuery.DanceIds.ToList();
            SetupLikes(list, dances.Count == 1 ? dances[0] : null);

            ViewBag.SongFilter = filter;

            ReportSearch(filter);

            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, "Exiting Song.Index");
            return View("Index", list);
        }

        private void ReportSearch(SongFilter filter)
        {
            var properties = new Dictionary<string, string>
            {
                {"Filter", filter.ToString()},
                {"User", User.Identity.Name}
            };
            var client = TelemetryClient;
            client.TrackEvent("SongIndex", properties);

            Database.UpdateSearches(User.Identity.IsAuthenticated ? Database.FindUser(User.Identity.Name) : null, filter);
        }

        private void BuildDanceList()
        {
            ViewBag.Dances = SongCounts.GetSongCounts(Database);
            ViewBag.DanceMap = SongCounts.GetDanceMap(Database);
            ViewBag.DanceList = GetDancesSingle(Database);
        }
        #endregion

        #region MusicService

        private string DefaultServiceSearch(SongDetails song, bool clean)
        {
            if (clean)
                return song.CleanTitle + " " + song.CleanArtist;

            return song.Title + " " + song.Artist;
        }

        private IList<ServiceTrack> FindMusicServiceSong(SongDetails song, MusicService service, bool clean = false, string title = null, string artist = null)
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
                Trace.WriteLine($"Failed '{we.Message}' on Song '{song}");
            }

            return tracks;
        }

        private void FixupTitleArtist(SongDetails song, bool clean, ref string title, ref string artist)
        {
            if (song != null && artist == null && title == null)
            {
                artist = clean?song.CleanArtist:song.Artist;
                title =  clean?song.CleanTitle:song.Title;
            }

            ViewBag.SongArtist = artist;
            ViewBag.SongTitle = title;
        }

        private SongDetails UpdateMusicService(SongDetails song, MusicService service, string name, string album, string artist, string trackId, string collectionId, string alternateId, string duration, int? trackNum)
        {
            // This is a very transitory object to hold the old values for a semi-automated edit
            var alt = new SongDetails();

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

        private SongDetails UpdateSongAndService(SongDetails sd, MusicService service, IDictionary<char, ApplicationUser> users)
        {
            var found = MatchSongAndService(sd, service);

            if (found.Count > 0)
            {
                var tags = new TagList();
                foreach (var foundTrack in found)
                {
                    UpdateMusicService(sd, MusicService.GetService(foundTrack.Service), foundTrack.Name, foundTrack.Album, foundTrack.Artist, foundTrack.TrackId, foundTrack.CollectionId, foundTrack.AltId, foundTrack.Duration.ToString(), foundTrack.TrackNumber);
                    tags = tags.Add(new TagList(Database.NormalizeTags(foundTrack.Genre, "Music")));
                }
                ApplicationUser user;
                // ReSharper disable once InvertIf
                if (!users.TryGetValue(service.CID, out user))
                {
                    user = Database.FindUser("batch-" + service.CID.ToString().ToLower());
                    users[service.CID] = user;
                }

                return Database.EditSong(user, sd, new[] { new UserTag { Id = string.Empty, Tags = tags } });
            }

            return null;
        }
        private IList<ServiceTrack> MatchSongAndService(SongDetails sd, MusicService service)
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
                foreach (var track in tracks.Where(track => sd.FindAlbum(track.Album,track.TrackNumber) != null))
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
                var track = SongDetails.FindDominantTrack(tracks);
                if (track.Duration != null) found = SongDetails.DurationFilter(tracks, track.Duration.Value, 6);
            }

            return found;
        }

        private bool CleanMusicServiceSong(SongBase song)
        {
            var del = new List<SongProperty>();

            SongProperty last = null;
            var lastPt = PurchaseType.None;
            var lastMs = ServiceType.None;
            var lastDel = false;

            foreach (var prop in song.OrderedProperties.Where(p => p.BaseName == SongBase.PurchaseField))
            {
                PurchaseType pt;
                ServiceType ms;

                if (!MusicService.TryParsePurchaseType(prop.Qualifier, out pt, out ms))
                    continue;

                if (pt != PurchaseType.Song)
                {
                    if (lastDel && pt == PurchaseType.Album && lastMs == ms)
                    {
                        del.Add(prop);
                        lastPt = PurchaseType.None;
                    }
                    else
                    {
                        last = prop;
                        lastPt = pt;
                        lastMs = ms;
                    }
                    lastDel = false;
                    continue;
                }

                lastDel = false;
                string[] regions;
                var purchase = PurchaseRegion.ParseIdAndRegionInfo(prop.Value, out regions);
                if (MusicServiceManager.GetMusicServiceTrack(purchase, MusicService.GetService(ms)) == null)
                {
                    del.Add(prop);
                    if (lastMs == ms && lastPt == PurchaseType.Album)
                    {
                        del.Add(last);
                    }

                    last = prop;
                    lastPt = pt;
                    lastMs = ms;
                    lastDel = true;
                    continue;
                }

                lastPt = PurchaseType.None;
            }

            // ReSharper disable once InvertIf
            if (del.Any())
            {
                del.AddRange(song.SongProperties.Where(p => p.Name == SongBase.FailedLookup));
                foreach (var prop in del)
                {
                    song.SongProperties.Remove(prop);
                    Database.SongProperties.Remove(prop);
                }
                Database.UpdatePurchaseInfo(song.SongId.ToString());
                return true;
            }

            return false;
        }

        #endregion

        #region Merge
        private IList<Song> AutoMerge(IList<Song> songs, int level)
        {
            // Get the logged in user
            var userName = User.Identity.Name;
            var user = Database.FindUser(userName);

            var ret = new List<Song>();
            List<Song> cluster = null;

            try
            {
                Context.Configuration.AutoDetectChangesEnabled = false;

                foreach (var song in songs)
                {
                    if (cluster == null)
                    {
                        cluster = new List<Song> {song};
                    }
                    else if ((level == 0 && song.Equivalent(cluster[0])) || ((level == 1 && song.WeakEquivalent(cluster[0])) || (level == 3) && song.TitleArtistEquivalent(cluster[0])))
                    {
                        cluster.Add(song);
                    }
                    else
                    {
                        if (cluster.Count > 1)
                        {
                            var s = AutoMerge(cluster, user);
                            ret.Add(s);
                        }
                        else if (cluster.Count == 1)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Bad Merge: {cluster[0].Signature}");
                        }

                        cluster = new List<Song> {song};
                    }
                }
            }
            finally
            {
                Context.Configuration.AutoDetectChangesEnabled = true;
                Database.SaveChanges();
                SongCounts.ClearCache();
            }

            return ret;
        }

        private Song AutoMerge(List<Song> songs, ApplicationUser user)
        {
            var song = Database.MergeSongs(user, songs,
                ResolveStringField(SongBase.TitleField, songs),
                ResolveStringField(SongBase.ArtistField, songs),
                ResolveDecimalField(SongBase.TempoField, songs),
                ResolveIntField(SongBase.LengthField, songs),
                SongDetails.BuildAlbumInfo(songs)
                );

            return song;
        }
        private ActionResult Merge(IEnumerable<Song> songs)
        {
            var sm = new SongMerge(songs.ToList());

            return View("Merge", sm);
        }

        private ActionResult CleanupAlbums(IEnumerable<Song> songs)
        {
            var user = Database.FindUser(User.Identity.Name);
            var scanned = 0;
            var changed = 0;
            var albums = 0;
            foreach (var song in songs)
            {
                var delta = Database.CleanupAlbums(user, song);
                if (delta > 0)
                {
                    changed += 1;
                    albums += delta;
                }
                scanned += 1;
            }

            ViewBag.Title = "Cleanup Albums";
            ViewBag.Message = $"Of {scanned} songs scanned, {changed} where changed.  {albums} were removed.";

            return View("Info");
        }

        private string ResolveStringField(string fieldName, IList<Song> songs, NameValueCollection form = null)
        {
            return ResolveMergeField(fieldName, songs, form) as string;
        }


        private int? ResolveIntField(string fieldName, IList<Song> songs, NameValueCollection form = null)
        {
            return ResolveMergeField(fieldName, songs, form) as int?;
        }

        private decimal? ResolveDecimalField(string fieldName, IList<Song> songs, NameValueCollection form = null)
        {
            return ResolveMergeField(fieldName, songs, form) as decimal?;
        }

        private object ResolveMergeField(string fieldName, IList<Song> songs, NameValueCollection form = null)
        {
            // If fieldName doesn't exist, this means that we didn't add a radio button for the field because all the
            //  values were the same.  So just return the value of the first song.

            // if form is != null we disambiguate based on form otherwise it's the first non-null field

            var idx = 0;
            if (form != null)
            {
                var s = form[fieldName];
                if (!string.IsNullOrWhiteSpace(s))
                {
                    int.TryParse(s, out idx);
                }
            }
            else
            {
                for (var i = 0; i < songs.Count; i++)
                {
                    var s = songs[i];

                    if (s.GetType().GetProperty(fieldName).GetValue(s) == null) continue;

                    idx = i;
                    break;
                }
            }

            var song = songs[idx];
            var type = song.GetType();
            var prop = type.GetProperty(fieldName);
            var o = prop.GetValue(song);
            return o;
        }
        #endregion
    }
}