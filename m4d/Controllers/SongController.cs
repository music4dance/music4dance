using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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
        private DanceMusicService.CruftFilter DefaultCruftFilter => User.IsInRole(DanceMusicService.EditRole)
            ? DanceMusicService.CruftFilter.AllCruft
            : DanceMusicService.CruftFilter.NoCruft;

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

            if (filter.Dances != null)
            {
                var stats = Database.DanceStats;
                var dq = filter.DanceQuery;
                foreach (var d in dq.DanceIds)
                {
                    var ds = stats.FromId(d);
                    if (ds == null)
                    {
                        return ReturnError(HttpStatusCode.NotFound, $"Dance id = {d} is not defined.");
                    }
                    if (ds.SongCount == 0)
                    {
                        return RedirectToAction("index", "dances", new {dance = ds.SeoName});
                    }
                }
            }

            filter.Purchase = null;
            filter.TempoMin = null;
            filter.TempoMax = null;

            return DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public ActionResult AzureSearch(string searchString, int page=1, string dances=null, SongFilter filter=null)
        {
            if (filter == null || filter.IsEmpty)
            {
                filter = SongFilter.AzureSimple;
            }

            if (User.Identity.IsAuthenticated && filter.IsEmpty)
            {
                filter.User = new UserQuery(User.Identity.Name, false, false).Query;
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

            if (searchString != null)
            {
                filter.SearchString = searchString;
            }

            if (page != 0)
                filter.Page = page;

            return DoAzureSearch(filter);
        }

        private ActionResult DoAzureSearch(SongFilter filter)
        {
            HelpPage = filter.IsSimple ? "song-list" : "advanced-search";

            if (!filter.IsEmptyPaged && SpiderManager.CheckAnySpiders(Request.UserAgent))
            {
                return View("BotFilter", filter);
            }

            var p = Database.AzureParmsFromFilter(filter, 25);
            p.IncludeTotalResultCount = true;

            ViewBag.RawSearch = p;

            var results = Database.AzureSearch(filter.SearchString, p, DefaultCruftFilter, "default", Database.DanceStats);
            BuildDanceList();
            ViewBag.SongFilter = filter;

            var songs = new StaticPagedList<Song>(results.Songs, results.CurrentPage, results.PageSize, (int)results.TotalCount);

            var dances = filter.DanceQuery.DanceIds.ToList();
            SetupLikes(results.Songs, dances.Count == 1 ? dances[0] : null);

            ReportSearch(filter);

            return View("azuresearch",songs);
        }

        //
        // GET: /Song/RawSearchForm
        [AllowAnonymous]
        public ActionResult RawSearchForm(SongFilter filter = null)
        {
            HelpPage = "advanced-search";

            ViewBag.AzureIndexInfo = Song.GetIndex(Database);
            return View(new RawSearch(filter));
        }

        //
        // GET: /Song/RawSearch
        [AllowAnonymous]
        public ActionResult RawSearch([Bind(Include = "SearchText,ODataFilter,Sort,IsLucene")] RawSearch rawSearch)
        {
            HelpPage = "advanced-search";

            ViewBag.AzureIndexInfo = Song.GetIndex(Database);
            return ModelState.IsValid ? DoAzureSearch(new SongFilter(rawSearch)) : View("RawSearchForm",rawSearch);
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
                filter.Action = filter.IsAzure ? "azure+advanced" : "Advanced";
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

            if (string.IsNullOrWhiteSpace(sortOrder) || string.Equals(sortOrder,"Closest Match",StringComparison.OrdinalIgnoreCase))
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

            return DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public ActionResult Sort(string sortOrder, SongFilter filter)
        {
            filter.SortOrder = SongSort.DoSort(sortOrder, filter.SortOrder);

            return DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public ActionResult FilterUser(string user, SongFilter filter)
        {
            filter.User = string.IsNullOrWhiteSpace(user) ? null : user;
            return DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public ActionResult FilterService(ICollection<string> services, SongFilter filter)
        {
            var purchase = string.Empty;
            if (services != null)
            {
                purchase = string.Concat(services);
            }

            if (filter.Purchase == purchase) return DoAzureSearch(filter);

            filter.Purchase = purchase;
            filter.Page = 1;
            return DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public ActionResult FilterTempo(decimal? tempoMin, decimal? tempoMax, SongFilter filter)
        {
            if (filter.TempoMin == tempoMin && filter.TempoMax == tempoMax)
                return DoAzureSearch(filter);

            filter.TempoMin = tempoMin;
            filter.TempoMax = tempoMax;
            filter.Page = 1;

            return DoAzureSearch(filter);
        }

        //
        // GET: /Index/
        [AllowAnonymous]
        public ActionResult Index(string id = null, int? page = null, string purchase = null, SongFilter filter = null)
        {
            if (filter == null)
            {
                filter = new SongFilter();
            }

            if (id != null && Database.DanceStats.Map.ContainsKey(id.ToUpper()))
            {
                filter.Dances = id.ToUpper();
            }

            if (page.HasValue)
            {
                filter.Page = page;
            }

            if (filter.IsAzure)
            {
                return DoAzureSearch(filter);
            }

            if (User.Identity.IsAuthenticated && filter.IsEmpty)
            {
                filter.User = new UserQuery(User.Identity.Name, false, false).Query;
            }

            if (!string.IsNullOrWhiteSpace(purchase))
            {
                filter.Purchase = purchase;
            }

            return DoAzureSearch(filter);
        }

        //
        // GET: /AdvancedIndex/
        [AllowAnonymous]
        public ActionResult Advanced(int? page, string purchase, SongFilter filter)
        {
            return Index(null, page, purchase, filter);
        }

        [AllowAnonymous]
        public ActionResult Tags(string tags, SongFilter filter)
        {
            filter.Tags = null;
            filter.Page = null;

            if (string.IsNullOrWhiteSpace(tags))
                return DoAzureSearch(filter);

            var list = new TagList(tags).AddMissingQualifier('+');
            filter.Tags = list.ToString();

            return DoAzureSearch(filter);
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

            return DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public ActionResult RemoveTags(string tags, SongFilter filter)
        {
            var sub = new TagList(tags);
            var old = new TagList(filter.Tags);
            var ret = old.Subtract(sub);
            filter.Tags = ret.ToString();
            filter.Page = null;

            return DoAzureSearch(filter);
        }

        //
        // GET: /Song/Details/5

        [AllowAnonymous]
        public ActionResult Details(Guid? id = null, SongFilter filter = null)
        {
            var spider = CheckSpiders();
            if (spider != null) return spider;

            var gid = id ?? Guid.Empty;
            var song = id.HasValue ? Database.FindSong(id.Value, User.Identity.Name) : null;
            if (song == null)
            {
                song = Database.FindMergedSong(gid, User.Identity.Name);
                return song != null ? 
                    RedirectToActionPermanent("details", new {id=song.SongId.ToString(), filter}) : 
                    ReturnError(HttpStatusCode.NotFound, $"The song with id = {gid} has been deleted.");
            }

            HelpPage = "song-details";
            BuildDanceList(DanceBags.Stats | DanceBags.Single);
            return View(song);
        }

        [AllowAnonymous]
        public ActionResult Album(string title)
        {
            var spider = CheckSpiders();
            if (spider != null) return spider;

            AlbumViewModel model;

            if (!string.IsNullOrWhiteSpace(title))
            {
                model = AlbumViewModel.Create(title, DefaultCruftFilter, Database);
            }
            else
            {
                return ReturnError(HttpStatusCode.NotFound, @"Empty album title not valid.");
            }

            BuildDanceList(DanceBags.Stats);
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
                
                model = ArtistViewModel.Create(name, DefaultCruftFilter, Database);
            }

            if (model == null)
                return ReturnError(HttpStatusCode.NotFound, $"Album '{name}' not found.");

            BuildDanceList(DanceBags.Stats);
            return View("Artist", model);
        }

        //
        // GET: /Song/Add
        public ActionResult Augment(SongFilter filter = null)
        {
            return View();
        }

        //
        // GET: /Song/Create
        public ActionResult Create(string title=null, string artist=null, decimal? tempo = null, int? length=null, string album=null, int? track=null, string service = null, string purchase = null, SongFilter filter = null)
        {
            Song sd;
            if (title == null && service != null && purchase != null)
            {
                var user = User.Identity.Name;
                var strack = MusicServiceManager.GetMusicServiceTrack(purchase, MusicService.GetService(service[0]));
                sd = Song.CreateFromTrack(user,strack,null,null,null,Database.DanceStats);
                sd.EditLike(user, true);
                UpdateSongAndServices(sd,user);
                GetEchoData(sd);
                GetSampleData(sd);
                sd.SetupSerialization(user, Database.DanceStats);
            }
            else
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
                        ad.SetPurchaseInfo(PurchaseType.Song, ms.Id, purchase);
                    }
                    adl = new List<AlbumDetails> { ad };
                }

                sd = new Song(title, artist, tempo, length, adl);
            }
            SetupEditViewBag(tempo);
            return View(sd);
        }

        // TODONEXT: 
        //  Add double/half time controls (or possibly edit of tempo)?
        //  Clean up the actual "add" page
        //      Give some reasonable feedback between search and start of load
        //      Make the errors red and review them
        //      Put some instructions on the page
        //      Can we persist the service?
        //  Verify that batch mode still works

        //
        // POST: /Song/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Song song, string userTags, SongFilter filter = null)
        {
            if (ModelState.IsValid)
            {
                var user = Database.FindUser(User.Identity.Name);
                var jt = JTags.FromJson(userTags);

                var newSong = Database.CreateSong(user, song, jt.ToUserTags());
                //newSong.AddTags(new TagList(userTags), user.UserName, Database.DanceStats, song);
                SaveSong(newSong);

                BuildDanceList(DanceBags.Stats | DanceBags.Single);
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
        public ActionResult Edit(Guid? id = null, decimal? tempo = null, SongFilter filter = null)
        {
            var song = Database.FindSong(id ?? Guid.Empty, User.Identity.Name);
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

            BuildDanceList(DanceBags.Stats | DanceBags.Single);
        }
        //
        // POST: /Song/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Song song, string userTags, SongFilter filter = null)
        {
            if (ModelState.IsValid)
            {
                var user = Database.FindUser(User.Identity.Name);
                var jt = JTags.FromJson(userTags);

                var edit = Database.EditSong(user, song, jt.ToUserTags());

                // ReSharper disable once InvertIf
                if (edit != null)
                {
                    SaveSong(edit);

                    // This should be a quick round-trip to hydrate with the user details
                    edit = Database.FindSong(edit.SongId, user.UserName);

                    ViewBag.BackAction = "Index";
                    BuildDanceList(DanceBags.Stats | DanceBags.Single);
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
            var song = Database.FindSong(id);
            return song == null ? ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.") : View(song);
        }

        //
        // POST: /Song/Delete/5

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")] 
        public ActionResult DeleteConfirmed(Guid id, SongFilter filter = null)
        {
            var song = Database.FindSong(id);
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

            song.Modified = DateTime.Now;
            SaveSong(song);

            var user = Database.FindUser(User.Identity.Name);
            var sd = Database.FindSong(songId, user.UserName);

            BuildDanceList(DanceBags.Stats | DanceBags.Single);
            return View("details", sd);
        }

        public ActionResult CleanupAlbums(Guid id, SongFilter filter = null)
        {
            var user = Database.FindUser(User.Identity.Name);

            var song = Database.FindSong(id);
            if (Database.CleanupAlbums(user, song) != 0)
            {
                SaveSong(song);
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
        // Merge: /Song/ClearMergeCache
        [Authorize(Roles = "canEdit")]
        public ActionResult ClearMergeCache()
        {
            Database.ClearMergeCandidates();
            ViewBag.Success = true;
            ViewBag.Message = "Merge Cache Cleared";

            return View("Results");
        }

        //
        // GET: /Song/UpdateRatings/5
        [Authorize(Roles = "canEdit")]
        public ActionResult UpdateRatings(Guid id, SongFilter filter = null)
        {
            var song = Database.FindSong(id);
            if (song == null)
            {
                return ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.");
            }
            song.SetRatingsFromProperties();
            SaveSong(song);

            HelpPage = "song-details";
            BuildDanceList(DanceBags.Stats | DanceBags.Single);

            return View("Details", song);
        }


        //
        // BulkEdit: /Song/BulkEdit
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult BulkEdit(Guid[] selectedSongs, string action, SongFilter filter = null)
        {
            var songs = Database.FindSongs(selectedSongs);

            switch (action)
            {
                case "Merge":
                    return Merge(songs);
                case "Delete":
                    return Delete(songs, filter);
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
            var songs = Database.FindSongs(songIds.Split(',').Select(Guid.Parse)).ToList();

            // Create a merged version of the song (and commit to DB)

            // Get the logged in user
            var userName = User.Identity.Name;
            var user = Database.FindUser(userName);

            var song = Database.MergeSongs(user, songs, 
                ResolveStringField(Song.TitleField, songs, Request.Form),
                ResolveStringField(Song.ArtistField, songs, Request.Form),
                ResolveDecimalField(Song.TempoField, songs, Request.Form),
                ResolveIntField(Song.LengthField, songs, Request.Form),
                Request.Form[Song.AlbumListField], new HashSet<string>(Request.Form.AllKeys));

            Database.RemoveMergeCandidates(songs);

            DanceStatsManager.ClearCache();

            ViewBag.BackAction = "MergeCandidates";
            BuildDanceList(DanceBags.Stats | DanceBags.Single);
            return View("details",Database.FindSong(song.SongId));
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
                if (filter == null) filter = SongFilter.Default;

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

                var sucIds = new List<Guid>();
                var failIds = new List<Guid>();

                var retry = false;
                var spotRetry = false;
                var cruftFilter = DanceMusicService.CruftFilter.AllCruft;

                // May do more options in future
                while (!string.IsNullOrWhiteSpace(options) && options.Length > 0)
                {
                    var o = options[0];
                    options = options.Substring(1);
                    switch (o)
                    {
                        case 'C':
                            cruftFilter = DanceMusicService.CruftFilter.NoPublishers;
                            break;
                        case 'R':
                            retry = true;
                            break;
                        case 'S':
                            spotRetry = true;
                            break;
                    }
                }

                var page = 0;
                var done = false;

                var parameters = DanceMusicService.AddCruftInfo(Database.AzureParmsFromFilter(filter),cruftFilter);
                parameters.Top = null;
                if (spotRetry)
                {
                    parameters.Filter = ((parameters.Filter == null) ? "" : parameters.Filter + " and ") + "(Purchase/all(t: t ne 'Groove') and Purchase/all(t: t ne 'Amazon') or Purchase/all(t: t ne 'ITunes'))";
                }
                else if (!retry)
                {
                    parameters.Filter = ((parameters.Filter == null) ? "" : parameters.Filter + " and ") + "(LookupStatus ne true)";
                }

                while (!done)
                {
                    AdminMonitor.UpdateTask("BuildSongList", page);
                    var songs = Database.TakeTail(parameters, Math.Min(count,pageSize));
                    var succeeded = new List<Song>();
                    var failed = new List<Song>();

                    var processed = 0;
                    foreach (var song in songs)
                    {
                        AdminMonitor.UpdateTask($"Processing ({succeeded.Count})", processed);
                        var edit = new Song(song,Database.DanceStats);

                        processed += 1;

                        var changed = service == null ? 
                            UpdateSongAndServices(edit) : 
                            UpdateSongAndService(song, service);

                        if (changed)
                        {
                            succeeded.Add(song);
                            tried += 1;
                        }
                        else if (service == null && song.AddLookupFail())
                        {
                            failed.Add(song);
                        }

                    }

                    sucIds.AddRange(succeeded.Select(s => s.SongId));
                    failIds.AddRange(failed.Select(s => s.SongId));

                    Database.UpdateAzureIndex(succeeded.Concat(failed));

                    if (tried > count)
                        break;

                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{tried} songs tried.");

                    page += 1;
                    if (processed < pageSize || retry)
                    {
                        done = true;
                    }
                }

                ViewBag.Completed = failIds.Count + sucIds.Count < count;
                ViewBag.Failed = failIds;
                ViewBag.Succeeded = sucIds;
                ViewBag.Skipped = 0;

                AdminMonitor.CompleteTask(true, $"BatchMusicService: Completed={tried <= count}, Succeeded={sucIds.Count} - ({string.Join(",", sucIds)}), Failed={failIds.Count} - ({string.Join(",", failIds)}), Skipped={0}");

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
            var song = Database.FindSong(id??Guid.Empty);
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

            BuildDanceList(DanceBags.Stats);
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

            var song = Database.FindSong(songId, User.Identity.Name);
            if (song == null)
            {
                return ReturnError(HttpStatusCode.NotFound, $"The song with id = {songId} has been deleted.");
            }

            var user = Database.FindUser(User.Identity.Name);
            var alt = UpdateMusicService(song, service, name, album, artist, trackId, collectionId, alternateId, duration, trackNum);
            song.AddTags(Database.NormalizeTags(genre, "Music"), user.UserName, Database.DanceStats, song);

            ViewBag.OldSong = alt;

            return View("Edit", song);
        }

        [Authorize(Roles = "canEdit")]
        public ActionResult BatchClearUpdate(SongFilter filter = null, int count = 100)
        {
            try
            {
                StartAdminTask("BatchClearUpdate");
                AdminMonitor.UpdateTask("BatchClearUpdate");

                if (filter == null)
                {
                    filter = new SongFilter();
                }
                filter.Page = 1;

                Task.Run(() =>
                {
                    var dms = DanceMusicService.GetService();
                    while (true)
                    {
                        var prms = Database.AzureParmsFromFilter(filter, 500);
                        prms.IncludeTotalResultCount = false;
                        prms.Filter = ((prms.Filter == null) ? "" : prms.Filter + " and ") + "Purchase/any(t: t eq '---')";
                        AdminMonitor.UpdateTask("BuildSongList", ((filter.Page ?? 1) - 1) * 500);
                        var res = dms.AzureSearch(filter.SearchString, prms, DanceMusicService.CruftFilter.AllCruft);
                        if (!res.Songs.Any()) break;

                        dms.SaveSongsImmediate(res.Songs);

                        filter.Page = filter.Page + 1;
                    }

                    AdminMonitor.CompleteTask(true, "BatchClearUpdate: Completed");
                });
                return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
            }
            catch (Exception e)
            {
                return FailAdminTask($"BatchClearUpdate: {e.Message}", e);
            }
        }


        // CleanMusicServices: /Song/CleanMusicServices
        [Authorize(Roles = "canEdit")]
        public ActionResult CleanMusicServices(Guid id, SongFilter filter = null)
        {
            var song = Database.FindSong(id);
            if (song == null)
            {
                return ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.");
            }

            var newSong = CleanMusicServiceSong(song);
            if (newSong != null)
            {
                Database.SaveSong(newSong);
            }

            HelpPage = "song-details";
            BuildDanceList(DanceBags.Stats | DanceBags.Single);
            return View("Details", newSong??song);
        }

        // B= Broken
        // S= Spotify Region
        [Authorize(Roles = "canEdit")]
        public ActionResult BatchCleanService(SongFilter filter = null, string type="BS",int count = 100)
        {
            try
            {
                StartAdminTask("BatchCleanService");
                AdminMonitor.UpdateTask("BatchCleanService");

                var changed = new List<Guid>();

                var tried = 0;
                var done = false;

                if (filter == null)
                {
                    filter = new SongFilter();
                }
                filter.Page = 1;

                Task.Run(() =>
                {
                    var dms = DanceMusicService.GetService();
                    while (!done)
                    {
                        AdminMonitor.UpdateTask("BuildSongList", ((filter.Page ?? 1) - 1) * 500);

                        var prms = dms.AzureParmsFromFilter(filter, 500);
                        prms.IncludeTotalResultCount = false;
                        prms.Filter = ((prms.Filter == null) ? "" : prms.Filter + " and ") + "Purchase/all(t: t ne '---')";
                        var res = dms.AzureSearch(filter.SearchString, prms, DanceMusicService.CruftFilter.AllCruft);
                        if (!res.Songs.Any()) break;
                        var save = new List<Song>();

                        var processed = 0;
                        foreach (var song in res.Songs)
                        {
                            try
                            {
                                AdminMonitor.UpdateTask("Processing", ((filter.Page ?? 1) - 1) * 500 +  processed);

                                processed += 1;
                                tried += 1;
                                var songT = CleanMusicServiceSong(song,type);
                                if (songT != null)
                                {
                                    changed.Add(songT.SongId);
                                }
                                else
                                {
                                    songT = song;
                                }

                                songT.BatchProcessed = true;
                                save.Add(songT);

                                if (count > 0 && tried > count)
                                    break;

                                if ((tried + 1)%25 != 0) continue;

                                Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{tried} songs tried.");
                            }
                            catch (Exception e)
                            {
                                Trace.WriteLine($"{song.Title} by {song.Artist} failed with: {e.Message}");
                            }
                        }

                        if (save.Count > 0)
                        {
                            dms.SaveSongsImmediate(save);
                        }

                        filter.Page += 1;
                        if (processed < 500)
                        {
                            done = true;
                        }
                    }

                    AdminMonitor.CompleteTask(true,
                        $"BatchCleanService: Completed={tried <= count}, Succeeded={changed.Count} - ({string.Join(",", changed)})");
                });
                return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
            }
            catch (Exception e)
            {
                return FailAdminTask($"BatchCleanService: {e.Message}", e);
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
                //var skipped = 0;

                if (filter == null)
                {
                    filter = new SongFilter();
                }
                filter.Purchase = "IS";

                var page = 0;
                var done = false;

                var user = Database.FindUser("batch-s");
                Debug.Assert(user != null);

                var parameters = Database.AzureParmsFromFilter(filter);
                parameters.Filter = ((parameters.Filter == null) ? "" : parameters.Filter + " and ") + "(Sample eq null or Sample eq '.')";

                var sucIds = new List<Guid>();
                var failIds = new List<Guid>();

                Task.Run(() =>
                {
                    var dms = DanceMusicService.GetService();
                    while (!done)
                    {
                        AdminMonitor.UpdateTask("BuildPage", page);
                        var songs = dms.TakeTail(parameters, pageSize);

                        var failed = new List<Song>();
                        var succeeded = new List<Song>();

                        var processed = 0;
                        foreach (var song in songs)
                        {
                            AdminMonitor.UpdateTask($"Processing ({succeeded.Count})", processed);

                            processed += 1;

                            tried += 1;
                            if (GetSampleData(song, dms))
                            {
                                succeeded.Add(song);
                            }
                            else
                            {
                                failed.Add(song);
                            }

                            if (tried > count)
                                break;

                            if ((tried + 1)%100 != 0) continue;

                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{tried} songs tried.");
                        }

                        sucIds.AddRange(succeeded.Select(s => s.SongId));
                        failIds.AddRange(failed.Select(s => s.SongId));

                        dms.UpdateAzureIndex(succeeded.Concat(failed));

                        page += 1;
                        if (processed < pageSize)
                        {
                            done = true;
                        }
                    }
                    AdminMonitor.CompleteTask(true,
                        $"BatchSample: Completed={tried <= count}, Succeeded={sucIds.Count} - ({string.Join(",", sucIds)}), Failed={failIds.Count} - ({string.Join(",", failIds)}), Skipped={0}");
                });

                return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
            }
            catch (Exception e)
            {
                return FailAdminTask($"BatchSample: {e.Message}", e);
            }
        }

        private bool GetSampleData(Song song, DanceMusicService dms = null)
        {
            var spotify = MusicService.GetService(ServiceType.Spotify);
            if (dms == null) dms = Database;
            var edit = new Song(song, dms.DanceStats);

            ServiceTrack track = null;
            // First try Spotify
            var ids = edit.GetPurchaseIds(spotify);
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
                var itunes = MusicService.GetService(ServiceType.ITunes);
                // If spotify failed, try itunes
                ids = edit.GetPurchaseIds(itunes);
                foreach (var id in ids)
                {
                    track = MusicServiceManager.GetMusicServiceTrack(id, itunes);
                    if (track?.SampleUrl != null)
                        break;
                }
            }

            edit.Sample = (track?.SampleUrl == null) ? @"." : track.SampleUrl;
            return dms.EditSong(User.Identity.Name, song, edit);
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

                var failed = new List<Guid>();
                var succeeded = new List<Guid>();

                var page = 0;
                var done = false;

                if (filter == null)
                {
                    filter = new SongFilter();
                }
                filter.Purchase = "S";

                var parameters = DanceMusicService.AddCruftInfo(Database.AzureParmsFromFilter(filter),DanceMusicService.CruftFilter.NoCruft);

                // SkipTempo
                if (options != null && options.Contains("T"))
                {
                    // TODO: Consider if we really want to do this based on performance issues against null checking
                    // https://social.msdn.microsoft.com/Forums/azure/en-US/977f5a45-6013-45b4-ae9d-285ada22d071/performance-of-queries-and-filters-with-null-values?forum=azuresearch
                    parameters.Filter = ((parameters.Filter == null) ? "" : parameters.Filter + " and ") + "(Tempo eq null)";
                }

                // Retry???
                if (options == null || options.Contains("R"))
                {
                    parameters.Filter = ((parameters.Filter == null) ? "" : parameters.Filter + " and ") + "(Beat eq null)";
                }

                Task.Run(() =>
                {
                    var dms = DanceMusicService.GetService();
                    while (!done)
                    {
                        AdminMonitor.UpdateTask("BuildPage", page);

                        var songs = dms.TakeTail(parameters, pageSize);
                        var processed = 0;

                        var stemp = new List<Song>();
                        var ftemp = new List<Song>();
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

                            // TODO: Decide if we want to tromp other tempi
                            //if (track?.BeatsPerMinute == null || (track.BeatsPerMinute == song.Tempo) ||
                            //    (sd.Tempo.HasValue && Math.Abs(track.BeatsPerMinute.Value - sd.Tempo.Value) > 5))
                            //{
                            //    skipped += 1;
                            //    continue;
                            //}

                            if (GetEchoData(song, dms))
                            {
                                stemp.Add(song);
                            }
                            else
                            {
                                ftemp.Add(song);
                            }

                            if (tried > count)
                                break;

                            if ((tried + 1)%100 != 0) continue;

                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{tried} songs tried.");
                        }

                        dms.UpdateAzureIndex(stemp.Concat(ftemp));
                        succeeded.AddRange(stemp.Select(s => s.SongId));
                        failed.AddRange(ftemp.Select(s => s.SongId));

                        page += 1;
                        if (processed < pageSize)
                        {
                            done = true;
                        }
                    }

                    AdminMonitor.CompleteTask(true,
                        $"BatchEchonest: Completed={tried <= count}, Succeeded={succeeded.Count} - ({string.Join(",", succeeded)}), Failed={failed.Count} - ({string.Join(",", failed)}), Skipped={skipped}");
                });

                return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
            }
            catch (Exception e)
            {
                return FailAdminTask($"BatchEchoNest: {e.Message}", e);
            }
        }

        private bool GetEchoData(Song song, DanceMusicService dms = null)
        {
            var service = MusicService.GetService(ServiceType.Spotify);
            var ids = song.GetPurchaseIds(service);
            if (dms == null) dms = Database;
            var edit = new Song(song, dms.DanceStats);

            EchoTrack track = null;
            foreach (var id in ids)
            {
                string[] regions;
                var idt = PurchaseRegion.ParseIdAndRegionInfo(id, out regions);
                track = MusicServiceManager.LookupEchoTrack(idt, service);
                if (track != null)
                    break;
            }

            if (track == null)
            {
                edit.Danceability = float.NaN;
                return dms.EditSong(User.Identity.Name, song, edit);
            }

            if (track.BeatsPerMinute != null)
            {
                edit.Tempo = track.BeatsPerMinute;
            }
            if (track.Danceability != null)
            {
                edit.Danceability = track.Danceability;
            }
            if (track.Energy != null)
            {
                edit.Energy = track.Energy;
            }
            if (track.Valence != null)
            {
                edit.Valence = track.Valence;
            }
            var tags = edit.GetUserTags(User.Identity.Name);
            var meter = track.Meter;
            if (meter != null)
            {
                tags = tags.Add($"{meter}:Tempo");
            }

            return  dms.EditSong(User.Identity.Name, song, edit, new [] { new UserTag { Id = string.Empty, Tags = tags}});
        }
        #endregion

        #region General Utilities
        static public IEnumerable<SelectListItem> GetDancesSingle(DanceMusicService dms)
        {
            var counts = DanceStatsManager.GetFlatDanceStats(dms);

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

        [Flags]
        enum DanceBags
        {
            //None = 0x00,
            List = 0x01,
            Stats = 0x02,
            Single = 0x04,
            All = List|Stats|Single
        }

        private void BuildDanceList(DanceBags bags = DanceBags.All)
        {
            if ((bags & DanceBags.List) == DanceBags.List)
                ViewBag.Dances = DanceStatsManager.GetDanceStats(Database);

            if ((bags & DanceBags.Stats) == DanceBags.Stats)
                ViewBag.DanceStats = DanceStatsManager.GetInstance(Database);

            if ((bags & DanceBags.Single) == DanceBags.Single)
                ViewBag.DanceList = GetDancesSingle(Database);
        }
        #endregion

        #region MusicService

        private string DefaultServiceSearch(Song song, bool clean)
        {
            if (clean)
                return song.CleanTitle + " " + song.CleanArtist;

            return song.Title + " " + song.Artist;
        }

        private IList<ServiceTrack> FindMusicServiceSong(Song song, MusicService service, bool clean = false, string title = null, string artist = null)
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

        private void FixupTitleArtist(Song song, bool clean, ref string title, ref string artist)
        {
            if (song != null && artist == null && title == null)
            {
                artist = clean?song.CleanArtist:song.Artist;
                title =  clean?song.CleanTitle:song.Title;
            }

            ViewBag.SongArtist = artist;
            ViewBag.SongTitle = title;
        }

        private static Song UpdateMusicService(Song song, MusicService service, string name, string album, string artist, string trackId, string collectionId, string alternateId, string duration, int? trackNum)
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

        private bool UpdateSongAndServices(Song sd, string user = null)
        {
            var changed = false;
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var service in MusicService.GetSearchableServices())
            {
                changed |= UpdateSongAndService(sd, service, user);
            }
            return changed;
        }

        private bool UpdateSongAndService(Song sd, MusicService service, string user = null)
        {
            var found = MatchSongAndService(sd, service);
            if (found.Count <= 0) return false;

            var edit = new Song(sd,Database.DanceStats);

            var tags = new TagList();
            foreach (var foundTrack in found)
            {
                var trackId = PurchaseRegion.FormatIdAndRegionInfo(foundTrack.TrackId, foundTrack.AvailableMarkets);
                UpdateMusicService(edit, MusicService.GetService(foundTrack.Service), foundTrack.Name, foundTrack.Album, foundTrack.Artist, trackId, foundTrack.CollectionId, foundTrack.AltId, foundTrack.Duration.ToString(), foundTrack.TrackNumber);
                tags = tags.Add(new TagList(Database.NormalizeTags(foundTrack.Genre, "Music",true)));
            }

            if (user != null)
            {
                tags = tags.Add(sd.GetUserTags(user));
            }
            else
            {
                user = $"batch-{service.CID.ToString().ToLower()}";
            }

            return Database.EditSong(user, sd, edit, new[] { new UserTag { Id = string.Empty, Tags = tags } });
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
                var track = Song.FindDominantTrack(tracks);
                if (track.Duration != null) found = Song.DurationFilter(tracks, track.Duration.Value, 6);
            }

            return found;
        }

        private Song CleanMusicServiceSong(Song song, string type="BS", string region = "US")
        {
            var props = new List<SongProperty>(song.SongProperties);

            var changed = false;
            if (type.IndexOf('B') != -1) { changed |= CleanBrokenServices(props);}
            if (type.IndexOf('S') != -1) { changed |= CleanSpotify(props, region);}

            Song newSong = null;

            if (changed)
            {
                newSong = new Song(song.SongId,props,Database.DanceStats);
            }

            return newSong;
        }

        private bool CleanSpotify(IEnumerable<SongProperty> props, string region = "US")
        {
            var spotify = MusicService.GetService(ServiceType.Spotify);
            var changed = 0;
            var updated = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var prop in props.Where(p => p.Name.StartsWith("Purchase") && p.Name.EndsWith(":SS")))
            {
                string[] regions;
                var id = PurchaseRegion.ParseIdAndRegionInfo(prop.Value, out regions);

                if (null == regions)
                {
                    // Not sure how we are getting duplicate region info, but this will fix it
                    var fix = PurchaseRegion.FixRegionInfo(prop.Value);
                    if (fix != null)
                    {
                        prop.Value = fix;
                        changed += 1;
                    }
                    else
                    {
                        try
                        {
                            var track = MusicServiceManager.GetMusicServiceTrack(prop.Value, spotify);
                            if (track.AvailableMarkets == null)
                            {
                                failed += 1;
                            }
                            else
                            {
                                prop.Value = PurchaseRegion.FormatIdAndRegionInfo(track.TrackId, track.AvailableMarkets);
                                regions = track.AvailableMarkets;
                                changed += 1;
                            }
                        }
                        catch (WebException)
                        {
                            failed += 1;
                        }
                    }
                }

                if (regions == null || string.IsNullOrWhiteSpace(region) || regions.Contains(region))
                {
                    skipped += 1;
                }
                else if (!string.IsNullOrWhiteSpace(region))
                {
                    var track = MusicServiceManager.CoerceTrackRegion(id, spotify, region);
                    if (track != null)
                    {
                        prop.Value = PurchaseRegion.FormatIdAndRegionInfo(track.TrackId,
                            PurchaseRegion.MergeRegions(regions, track.AvailableMarkets));
                        updated += 1;

                    }
                    else
                    {
                        failed += 1;
                    }
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Spotify: Changed = {changed}, Failed = {failed}, Updated = {updated}, Skipped = {skipped}");

            return updated > 0;
        }


        private bool CleanBrokenServices(ICollection<SongProperty> props, string region = "US")
        {
            var del = new List<SongProperty>();
            // Check every purchase link and make sure it's still valid
            foreach (var prop in props.Where(p => p.Name.StartsWith("Purchase") && p.Name.EndsWith("S")))
            {
                PurchaseType pt;
                ServiceType ms;

                if (!MusicService.TryParsePurchaseType(prop.Qualifier, out pt, out ms)) continue;

                var service = MusicService.GetService(ms);
                if (!service.IsSearchable) continue;

                string[] regions;
                var id = PurchaseRegion.ParseIdAndRegionInfo(prop.Value, out regions);
                ServiceTrack track = null;
                try
                {
                    track = MusicServiceManager.GetMusicServiceTrack(id, MusicService.GetService(ms));
                }
                catch (WebException)
                {
                }
                if (track != null) continue;

                del.Add(prop);
            }

            if (del.Count == 0) return false;

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Removed: {del.Count}");

            foreach (var prop in del)
            {
                props.Remove(prop);
            }

            return true;
        }


        #endregion

        #region Merge
        private IList<Song> AutoMerge(IEnumerable<Song> songs, int level)
        {
            // Get the logged in user
            var userName = User.Identity.Name;
            var user = Database.FindUser(userName);

            var ret = new List<Song>();
            List<Song> cluster = null;

            try
            {
                foreach (var song in new List<Song>(songs))
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
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Bad Merge: {cluster[0].Title}");
                        }

                        cluster = new List<Song> {song};
                    }
                }
            }
            finally
            {
                DanceStatsManager.ClearCache();
            }

            return ret;
        }

        private Song AutoMerge(List<Song> songs, ApplicationUser user)
        {
            // These songs are coming from "light loading", so need to reload the full songs before merging
            songs = Database.FindSongs(songs.Select(s => s.SongId)).ToList();

            var song = Database.MergeSongs(user, songs,
                ResolveStringField(Song.TitleField, songs),
                ResolveStringField(Song.ArtistField, songs),
                ResolveDecimalField(Song.TempoField, songs),
                ResolveIntField(Song.LengthField, songs),
                Song.BuildAlbumInfo(songs)
                );

            Database.RemoveMergeCandidates(songs);

            return song;
        }
        private ActionResult Merge(IEnumerable<Song> songs)
        {
            var sm = new SongMerge(songs.ToList(),Database.DanceStats);

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