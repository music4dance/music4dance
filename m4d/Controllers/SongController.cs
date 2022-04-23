using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AutoMapper;
using Azure.Search.Documents;
using DanceLibrary;
using m4d.Utilities;
using m4d.ViewModels;
using m4dModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace m4d.Controllers
{
    public class SongController : ContentController
    {
        private static readonly HttpClient HttpClient = new();

        private readonly LinkGenerator _linkGenerator;
        private readonly IMapper _mapper;

        public SongController(DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, LinkGenerator linkGenerator,
            IConfiguration configuration, IMapper mapper) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            HelpPage = "song-list";
            UseVue = true;
            _linkGenerator = linkGenerator;
            _mapper = mapper;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var user = UserName;
            filterContext.ActionArguments.TryGetValue("filter", out var o);
            if (o == null)
            {
                o = SongFilter.GetDefault(user);
                filterContext.ActionArguments["filter"] = o;
            }

            ViewBag.SongFilter = o is SongFilter filter ? filter : SongFilter.GetDefault(user);

            base.OnActionExecuting(filterContext);
        }

        private DanceMusicCoreService.CruftFilter DefaultCruftFilter()
        {
            return User.IsInRole(DanceMusicCoreService.DiagRole) ||
                User.IsInRole(DanceMusicCoreService.PremiumRole) ||
                User.IsInRole(DanceMusicCoreService.TrialRole)
                    ? DanceMusicCoreService.CruftFilter.AllCruft
                    : DanceMusicCoreService.CruftFilter.NoCruft;
        } 

        #region Commands

        [AllowAnonymous]
        public async Task<ActionResult> Search(string searchString, string dances,
            SongFilter filter)
        {
            return await AzureSearch(searchString, filter, 0, dances);
        }

        [AllowAnonymous]
        public async Task<ActionResult> NewMusic(string type = null, int? page = null,
            SongFilter filter = null)
        {
            filter ??= new SongFilter();
            filter.Action = "newmusic";
            if (type != null)
            {
                filter.SortOrder = type;
            }

            if (string.IsNullOrWhiteSpace(filter.SortOrder))
            {
                filter.SortOrder = "Created";
            }

            if (page != null)
            {
                filter.Page = page;
            }

            if (Identity.IsAuthenticated && filter.IsEmpty)
            {
                filter.User = new UserQuery(UserName, false, false).Query;
            }

            return await DoAzureSearch(filter, true);
        }

        [AllowAnonymous]
        public async Task<ActionResult> HolidayMusic(string dance = null, int page = 1)
        {
            var filter = SongFilter.CreateHolidayFilter(dance, page);

            try
            {
                var results = await BuildAzureSearch(filter);

                string playListId = null;

                if (!string.IsNullOrWhiteSpace(dance))
                {
                    var ds = Database.DanceStats.FromName(dance);
                    var name = $"Holiday {ds.DanceName}";
                    var playlist = Database.PlayLists.FirstOrDefault(
                        p => p.Name == name && p.Type == PlayListType.SpotifyFromSearch);
                    playListId = playlist?.Id;
                }

                var dictionary = await UserMapper.GetUserNameDictionary(UserManager);
                var histories = results.Songs
                    .Select(s => UserMapper.AnonymizeHistory(s.GetHistory(_mapper), dictionary))
                    .ToList();
                return Vue(
                    "Holiday Dance Music", 
                    "Help finding holiday dance music for partner dancing - Foxtrot, Waltz, Swing and others.", 
                    "holiday-music",
                     new HolidaySongListModel
                    {
                        Histories = histories,
                        Filter = _mapper.Map<SongFilterSparse>(filter),
                        Count = (int)results.TotalCount,
                        Dance = dance,
                        PlayListId = playListId,
                    },
                    danceEnvironment:true);
            }
            catch (RedirectException ex)
            {
                return HandleRedirect(ex);
            }

        }

        [AllowAnonymous]
        public async Task<ActionResult> AzureSearch(string searchString, SongFilter filter,
            int page = 1,
            string dances = null)
        {
            if (Identity.IsAuthenticated && filter.IsEmpty)
            {
                filter.User = new UserQuery(UserName, false, false).Query;
            }

            if (string.IsNullOrWhiteSpace(dances))
            {
                dances = null;
            }

            if (dances != null &&
                !string.Equals(dances, filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                filter.Dances = dances;
                filter.Page = 1;

                if (string.IsNullOrWhiteSpace(filter.SortOrder) && filter.DanceQuery.Dances.Count() == 1)
                {
                    filter.SortOrder = "Dances";
                }
            }

            if (searchString != null)
            {
                filter.SearchString = searchString;
            }

            if (page != 0)
            {
                filter.Page = page;
            }

            filter.Purchase = null;
            filter.TempoMin = null;
            filter.TempoMax = null;

            return await DoAzureSearch(filter);
        }

        // TODO: Consider abstracting this (and maybe format) out into a builder/computer
        private async Task<ActionResult> DoAzureSearch(SongFilter filter, bool? hideSort = null)
        {
            try
            {
                var results = await BuildAzureSearch(filter);
                return await FormatResults(results, filter, hideSort);
            }
            catch (RedirectException ex)
            {
                return HandleRedirect(ex);
            }

        }

        private async Task<ActionResult> FormatResults(
            SearchResults results, SongFilter filter, bool? hideSort = null)
        {
            return await FormatSongList(
                results.Songs.ToList(), (int)results.TotalCount,
                filter, hideSort);
        }

        private async Task<ActionResult> FormatSongList(IReadOnlyCollection<Song> songs,
            int? totalSongs, SongFilter filter, bool? hideSort = null)
        {

            var user = UserName;
            if (user != null)
            {
                filter.Anonymize(user);
            }

            var dictionary = await UserMapper.GetUserNameDictionary(UserManager);
            var histories = songs.Select(
                s =>
                    UserMapper.AnonymizeHistory(s.GetHistory(_mapper), dictionary)).ToList();
                
            return Vue("Songs for Dancing", $"music4dance catalog: {filter.Description}", filter.VueName,
                new SongListModel
                {
                    Histories = histories,
                    Filter = _mapper.Map<SongFilterSparse>(filter),
                    Count = totalSongs ?? songs.Count,
                },
                danceEnvironment: true);
        }

        private async Task<SearchResults> BuildAzureSearch(SongFilter filter)
        {
            HelpPage = filter.IsSimple ? "song-list" : "advanced-search";

            if (!filter.IsEmptyBot &&
                SpiderManager.CheckAnySpiders(Request.Headers[HeaderNames.UserAgent]))
            {
                throw new RedirectException("BotFilter", filter);
            }

            if (filter.Level != null && filter.Level != 0 &&
                !(User.IsInRole(DanceMusicCoreService.PremiumRole) ||
                    User.IsInRole(DanceMusicCoreService.TrialRole) ||
                    User.IsInRole(DanceMusicCoreService.DiagRole)))
            {
                filter.Level = null;
                var redirectUrl =
                    _linkGenerator.GetUriByAction(
                        HttpContext, "AdvancedSearchForm", "Song",
                        new { filter });
                var premiumRedirect = new PremiumRedirect
                {
                    FeatureType = "search",
                    FeatureName = "bonus content",
                    InfoUrl = "https://music4dance.blog/?page_id=8217",
                    RedirectUrl = redirectUrl
                };
                throw new RedirectException("RequiresPremium", premiumRedirect);
            }

            var userQuery = filter.UserQuery;
            var currentUser = UserName;
            if (!userQuery.IsEmpty)
            {
                if (userQuery.IsIdentity)
                {
                    if (Identity.IsAuthenticated)
                    {
                        filter.User = new UserQuery(userQuery, currentUser).Query;
                    }
                    else
                    {
                        throw new RedirectException("Login", filter);
                    }
                }
                else if (!string.Equals(currentUser, userQuery.UserName))
                {
                    // In this case we want to intentionally overwrite the incoming filter
                    var temp = await UserMapper.AnonymizeFilter(filter, UserManager);
                    filter.User = temp.User;
                }
            }

            var p = await AzureParmsFromFilter(filter, 25);
            p.IncludeTotalCount = true;

            return await Database.Search(
                filter.SearchString, p, filter.CruftFilter);
        }

        //
        // GET: /Song/RawSearchForm
        [AllowAnonymous]
        public ActionResult RawSearchForm([FromServices]IDanceStatsManager danceStatsManager,
            SongFilter filter = null)
        {
            HelpPage = "advanced-search";

            ViewBag.AzureIndexInfo = Song.GetIndex(
                SearchService.GetInfo().Index, Database, danceStatsManager);
            UseVue = false;
            return View(new RawSearch(filter is { IsRaw: true } ? filter : null));
        }

        //
        // GET: /Song/RawSearch
        [AllowAnonymous]
        public async Task<ActionResult> RawSearch(
            [FromServices]IDanceStatsManager danceStatsManager,
            [Bind(
                "SearchText,ODataFilter,SortFields,SearchFields,Description,IsLucene,CruftFilter")]
            RawSearch rawSearch)
        {
            HelpPage = "advanced-search";

            ViewBag.AzureIndexInfo = Song.GetIndex(
                SearchService.GetInfo().Index, Database, danceStatsManager);
            return ModelState.IsValid
                ? await DoAzureSearch(new SongFilter(rawSearch))
                : View("RawSearchForm", rawSearch);
        }

        //
        // GET: /Song/AdvancedSearchForm
        [AllowAnonymous]
        public ActionResult AdvancedSearchForm(SongFilter filter)
        {
            return Vue(
                "Advanced Search",
                $"music4dance advanced song search form: {filter.Description}",
                "advanced-search", helpPage: "advanced-search",
                danceEnvironment: true, tagEnvironment: true);
        }

        [AllowAnonymous]
        public async Task<ActionResult> FilterSearch(SongFilter filter)
        {
            return await DoAzureSearch(filter);
        }

        // Get: /Song/AdvancedSearch
        [AllowAnonymous]
        public async Task<ActionResult> AdvancedSearch(string searchString = null,
            string dances = null,
            string tags = null, ICollection<string> services = null, decimal? tempoMin = null,
            decimal? tempoMax = null, string user = null, string sortOrder = null,
            string sortDirection = null, ICollection<int> bonusContent = null,
            SongFilter filter = null)
        {
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

            if (string.IsNullOrWhiteSpace(sortOrder) || string.Equals(
                sortOrder, "Closest Match",
                StringComparison.OrdinalIgnoreCase))
            {
                sortOrder = null;
            }
            else if (string.Equals(sortDirection, "Descending", StringComparison.OrdinalIgnoreCase))
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

            int? level = null;
            if (bonusContent != null)
            {
                level = 0;
                foreach (var x in bonusContent)
                {
                    level |= x;
                }
            }

            if (filter.Level != level)
            {
                filter.Level = level;
                filter.Page = 1;
            }

            return await DoAzureSearch(filter);
        }


        [AllowAnonymous]
        public async Task<ActionResult> Sort(string sortOrder, SongFilter filter)
        {
            filter.SortOrder = SongSort.DoSort(sortOrder, filter.SortOrder);

            return await DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public async Task<ActionResult> FilterUser(string user, SongFilter filter)
        {
            filter.User = string.IsNullOrWhiteSpace(user) ? null : user;
            return await DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public async Task<ActionResult> FilterService(ICollection<string> services,
            SongFilter filter)
        {
            var purchase = string.Empty;
            if (services != null)
            {
                purchase = string.Concat(services);
            }

            if (filter.Purchase == purchase)
            {
                return await DoAzureSearch(filter);
            }

            filter.Purchase = purchase;
            filter.Page = 1;
            return await DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public async Task<ActionResult> FilterTempo(decimal? tempoMin, decimal? tempoMax,
            SongFilter filter)
        {
            if (filter.TempoMin == tempoMin && filter.TempoMax == tempoMax)
            {
                return await DoAzureSearch(filter);
            }

            filter.TempoMin = tempoMin;
            filter.TempoMax = tempoMax;
            filter.Page = 1;

            return await DoAzureSearch(filter);
        }

        //
        // GET: /Index/
        [AllowAnonymous]
        public async Task<ActionResult> Index(SongFilter filter, string id = null, int? page = null,
            string purchase = null)
        {
            if (id != null && Dances.Instance.DanceFromId(id) != null)
            {
                filter.Dances = id.ToUpper();
            }

            if (page.HasValue)
            {
                filter.Page = page;
            }

            if (Identity.IsAuthenticated && filter.IsEmpty)
            {
                filter.User = new UserQuery(UserName, false, false).Query;
            }

            if (!string.IsNullOrWhiteSpace(purchase))
            {
                filter.Purchase = purchase;
            }

            return await DoAzureSearch(filter);
        }

        //
        // GET: /AdvancedIndex/
        // TODO: Figure out if this ever gets called
        [AllowAnonymous]
        public async Task<ActionResult> Advanced(int? page, string purchase, SongFilter filter)
        {
            return await Index(filter, null, page, purchase);
        }

        [AllowAnonymous]
        public async Task<ActionResult> Tags(string tags, SongFilter filter)
        {
            filter.Tags = null;
            filter.Page = null;

            if (string.IsNullOrWhiteSpace(tags))
            {
                return await DoAzureSearch(filter);
            }

            var list = new m4dModels.TagList(tags).AddMissingQualifier('+');
            filter.Tags = list.ToString();

            BuildEnvironment(tagDatabase: true);
            return await DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public async Task<ActionResult> AddTags(string tags, SongFilter filter)
        {
            var add = new m4dModels.TagList(tags);
            var old = new m4dModels.TagList(filter.Tags);

            // First remove any tags from the old list
            old = old.Subtract(add);
            var ret = old.Add(add.AddMissingQualifier('+'));
            filter.Tags = ret.ToString();
            filter.Page = null;

            return await DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public async Task<ActionResult> RemoveTags(string tags, SongFilter filter)
        {
            var sub = new m4dModels.TagList(tags);
            var old = new m4dModels.TagList(filter.Tags);
            var ret = old.Subtract(sub);
            filter.Tags = ret.ToString();
            filter.Page = null;

            return await DoAzureSearch(filter);
        }

        //
        // GET: /Song/Details/5

        [AllowAnonymous]
        public async Task<ActionResult> Details(SongFilter filter, Guid? id = null)
        {
            var spider = CheckSpiders();
            if (spider != null)
            {
                return spider;
            }

            var gid = id ?? Guid.Empty;
            var song = id.HasValue ? await Database.FindSong(id.Value) : null;
            if (song == null)
            {
                song = await Database.FindMergedSong(gid);
                return song != null
                    ? RedirectToActionPermanent(
                        "details",
                        new { id = song.SongId.ToString(), filter })
                    : ReturnError(
                        HttpStatusCode.NotFound,
                        $"The song with id = {gid} has been deleted.");
            }

            var details = await GetSongDetails(song, filter);
            return Vue(details.Title, $"music4dance catalog: {details.Title} dance information", "song",
                details, helpPage: "song-details");
        }

        private async Task<SongDetailsModel> GetSongDetails(Song song, SongFilter filter)
        {
            BuildEnvironment(danceEnvironment: true, tagDatabase: true);
            return new()
            {
                Title = song.Title,
                SongHistory = await UserMapper.AnonymizeHistory(
                    song.GetHistory(_mapper), UserManager),
                Filter = _mapper.Map<SongFilterSparse>(filter),
                UserName = UserName,
            };
        }

        [AllowAnonymous]
        public async Task<ActionResult> Album(string title)
        {
            var spider = CheckSpiders();
            if (spider != null)
            {
                return spider;
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                var model = await AlbumViewModel.Create(
                    title, _mapper, DefaultCruftFilter(), Database);
                return Vue(
                    $"Album: {title}", $"Songs for dancing on {title}", "album",
                    model, danceEnvironment: true);
            }

            return ReturnError(HttpStatusCode.NotFound, @"Empty album title not valid.");

        }

        [AllowAnonymous]
        public async Task<ActionResult> Artist(string name)
        {
            var spider = CheckSpiders();
            if (spider != null)
            {
                return spider;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                var model = await ArtistViewModel.Create(
                    name, _mapper, DefaultCruftFilter(), Database);
                return Vue(
                    $"Artist: {name}", $"Songs for dancing by {name}", "artist",
                    model, danceEnvironment:true);
            }

            return ReturnError(HttpStatusCode.NotFound, @"Empty artist name not valid.");
        }

        //
        // GET: /Song/Add
        [AllowAnonymous]
        public ActionResult Augment(string title = null, string artist = null, string id = null)
        {
            return Vue("Add Song", "Add a new song to the music4dance catalog", "augment", 
                new AugmentViewModel { Title = title, Artist = artist, Id = id},
                danceEnvironment: true, tagEnvironment:true, helpPage: "add-songs"
                );
        }

        //
        // GET: /Song/UpdateSongAndServices
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> UpdateSongAndServices(Guid id, SongFilter filter)
        {
            var song = await Database.FindSong(id);
            if (song == null)
            {
                ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.");
            }

            await MusicServiceManager.UpdateSongAndServices(Database, song);

            HelpPage = "song-details";
            return View("Details", GetSongDetails(song, filter));
        }

        // VUEDTODO: Do we still support this????
        //
        // GET: /Song/Delete/5
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var song = await Database.FindSong(id);
            UseVue = false;
            return song == null
                ? ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.")
                : View(song);
        }

        //
        // POST: /Song/Delete/5

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> DeleteConfirmed(Guid id, SongFilter filter)
        {
            var song = await Database.FindSong(id);
            var userName = User.Identity?.Name;
            var user = await Database.FindUser(userName);
            await Database.DeleteSong(user, song);

            return RedirectToAction("Index", new { filter });
        }

        //
        // POST: /Song/BatchCorrectTempo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> BatchCorrectTempo(SongFilter filter,
            decimal multiplier = 0.5M,
            string user = null, int max = 1000)
        {
            var applicationUser = user == null
                ? new ApplicationUser("tempo-bot", true)
                : await Database.FindOrAddUser(user);

            return BatchAdminExecute(
                filter,
                async (dms, song) =>
                    await dms.CorrectTempoSong(song, applicationUser, multiplier),
                "BatchCorrectTempo", max);
        }

        //
        // POST: /Song/BatchAdminEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> BatchAdminEdit(SongFilter filter, string properties,
            string user = null,
            int max = 1000)
        {
            Debug.Assert(User.Identity != null, "User.Identity != null");
            var applicationUser = await Database.FindUser(user ?? UserName);
            return BatchAdminExecute(
                filter, (dms, song) =>
                    dms.AdminAppendSong(song, applicationUser, properties), "BatchAdminEdit", max);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchAdminModify(SongFilter filter, string properties,
            int max = 10000)
        {
            return BatchAdminExecute(
                filter,
                (dms, song) => dms.AdminModifySong(song, properties),
                "BatchAdminModify", max);
        }


        private ActionResult BatchAdminExecute(SongFilter filter,
            Func<DanceMusicCoreService, Song, Task<bool>> act, string name, int max)
        {
            if (!ModelState.IsValid || filter.IsEmpty)
            {
                return RedirectToAction("Index", new { filter });
            }

            try
            {
                StartAdminTask(name);
                AdminMonitor.UpdateTask(name);
                var tried = 0;

                var dms = Database.GetTransientService();
                Task.Run(
                    async () =>
                    {
                        try
                        {
                            var results = await dms.Search(
                                filter, max, DanceMusicCoreService.CruftFilter.AllCruft);
                            var songs = results.Songs;

                            var processed = 0;

                            var succeeded = new List<Song>();
                            var failed = new List<Song>();
                            foreach (var song in songs)
                            {
                                AdminMonitor.UpdateTask(
                                    $"Processing ({succeeded.Count})", processed);

                                tried += 1;
                                processed += 1;

                                if (await act(dms, song))
                                {
                                    succeeded.Add(song);
                                }
                                else
                                {
                                    failed.Add(song);
                                }

                                if ((tried + 1) % 100 != 0)
                                {
                                    continue;
                                }

                                Trace.WriteLineIf(
                                    TraceLevels.General.TraceInfo,
                                    $"{tried} songs tried.");
                            }

                            await dms.UpdateAzureIndex(succeeded.Concat(failed));


                            AdminMonitor.CompleteTask(
                                true,
                                $"{name}: Completed={true}, Succeeded={succeeded.Count} - ({string.Join(",", succeeded.Select(s => s.SongId))}), Failed={failed.Count} - ({string.Join(",", failed.Select(s => s.SongId))})");
                        }
                        catch (Exception e)
                        {
                            AdminMonitor.CompleteTask(
                                false, $"BatchAdminExecute: Failed={e.Message}");
                        }
                        finally
                        {
                            dms.Dispose();
                        }
                    });

                return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
            }
            catch (Exception e)
            {
                return FailAdminTask($"{name}: {e.Message}", e);
            }
        }

        public async Task<ActionResult> CleanupAlbums(Guid id, SongFilter filter)
        {
            var user = await Database.FindUser(User.Identity?.Name);

            var song = await Database.FindSong(id);
            if (await Database.CleanupAlbums(user, song) != 0)
            {
                await SaveSong(song);
            }

            return RedirectToAction("Details", new { id, filter });
        }


        //
        // POST: /Song/Delete/5

        [HttpPost]
        [ActionName("UndoUserChanges")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<ActionResult> UndoUserChanges(SongFilter filter, Guid id,
            string userName = null)
        {
            if (userName == null)
            {
                userName = User.Identity?.Name;
            }
            else if (!User.IsInRole("showDiagnostics"))
            {
                return new StatusCodeResult((int)HttpStatusCode.Forbidden);
            }

            var user = await Database.FindUser(userName);
            await Database.UndoUserChanges(user, id);
            return RedirectToAction("Details", new { id, filter });
        }


        [HttpGet]
        public async Task<ActionResult> CreateSpotify(SongFilter filter)
        {
            HelpPage = "spotify-playlist";
            UseVue = false;

            var authResult = await HttpContext.AuthenticateAsync();
            var canSpotify = AdmAuthentication.HasAccess(
                Configuration, ServiceType.Spotify, User, authResult);

            return View(
                new PlaylistCreateInfo
                {
                    Title = string.IsNullOrWhiteSpace(filter.ShortDescription)
                        ? "music4dance playlist"
                        : filter.ShortDescription,
                    DescriptionPrefix =
                        "This playlist was created with information from music4dance.net: ",
                    Description = filter.Description,
                    Count = 25,
                    Filter = filter.ToString(),
                    IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                    IsPremium = User.IsInRole("premium") || User.IsInRole("trial"),
                    CanSpotify = canSpotify
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateSpotify([FromServices]IFileProvider fileProvider,
            [Bind("Title,DescriptionPrefix,Description,Count,Filter")]
            PlaylistCreateInfo info)
        {
            var authResult = await HttpContext.AuthenticateAsync();
            var canSpotify = (await AdmAuthentication.GetServiceAuthorization(
                Configuration, ServiceType.Spotify, User, authResult)) != null;

            info.IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
            info.IsPremium = User.IsInRole("premium") || User.IsInRole("trial");
            info.CanSpotify = canSpotify;

            if (!ModelState.IsValid)
            {
                return View(info);
            }

            if (info.Count > 100)
            {
                ViewBag.StatusMessage = "Please stop trying to hack the site.";
                return View("Error");
            }

            if (!canSpotify)
            {
                ViewBag.Title = "Connect your account to Spotify";
                ViewBag.Message =
                    "You must have a Spotify account associated with your music4dance account in order to use this feature. More instruction on adding an external account are available <a href='https://music4dance.blog/music4dance-help/account-management/#add-external-account'>here</a>.";
                return View("Info");
            }

            PlaylistMetadata metadata;
            var filter = new SongFilter(info.Filter);

            HelpPage = "spotify-playlist";

            try
            {
                filter.Purchase = "S";
                var p = await AzureParmsFromFilter(filter, info.Count);
                p.IncludeTotalCount = true;
                var results = await Database.Search(
                    filter.SearchString, p, filter.CruftFilter);
                var tracks = results.Songs.Select(s => s.GetPurchaseId(ServiceType.Spotify));

                var service = MusicService.GetService(ServiceType.Spotify);
                metadata = await MusicServiceManager.CreatePlaylist(
                    service, User, info.Title,
                    $"{info.DescriptionPrefix} {filter.Description}", fileProvider);

                if (!await MusicServiceManager.SetPlaylistTracks(service, User, metadata.Id, tracks))
                {
                    ViewBag.StatusMessage = "Unable to set the playlist tracks.";
                    return View("Error");
                }
            }
            catch (Exception e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, e.Message);
                ViewBag.StatusMessage =
                    "Unable to create a playlist at this time.  Please report the issue.";
                return View("Error");
            }

            ViewBag.SongFilter = filter;
            UseVue = false;

            var user = await Database.FindUser(User.Identity?.Name);
            Database.Context.ActivityLog.Add(new ActivityLog(
                "SpotifyExport", user, new SpotifyCreate { Id = metadata.Id, Info = info }));
            await Database.SaveChanges();
            return View("SpotifyCreated", metadata);
        }

        //
        // Merge: /Song/MergeCandidates
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> MergeCandidates(int? page, int? level, bool? autoCommit,
            SongFilter filter)
        {
            filter.Action = "MergeCandidates";

            if (page.HasValue)
            {
                filter.Page = page;
            }

            if (level.HasValue)
            {
                filter.Level = level;
            }

            var songs =
                await Database.FindMergeCandidates(
                    autoCommit == true ? 10000 : 500, filter.Level ?? 1);

            if (autoCommit.HasValue && autoCommit.Value)
            {
                songs = await AutoMerge(songs, filter.Level ?? 1);
            }

            return await FormatSongList(songs, null, filter);
        }

        //
        // Merge: /Song/ClearMergeCache
        [Authorize(Roles = "dbAdmin")]
        public ActionResult ClearMergeCache()
        {
            Database.ClearMergeCandidates();
            ViewBag.Success = true;
            ViewBag.Message = "Merge Cache Cleared";

            return View("Results");
        }

        //
        // GET: /Song/UpdateRatings/5
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> UpdateRatings(Guid id, SongFilter filter)
        {
            var song = await Database.FindSong(id);
            if (song == null)
            {
                return ReturnError(
                    HttpStatusCode.NotFound,
                    $"The song with id = {id} has been deleted.");
            }

            song.SetRatingsFromProperties();
            await SaveSong(song);

            HelpPage = "song-details";

            return View("Details", song);
        }


        //
        // BulkEdit: /Song/BulkEdit
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> BulkEdit(Guid[] selectedSongs, string action,
            SongFilter filter)
        {
            var songs = await Database.FindSongs(selectedSongs);

            switch (action)
            {
                case "Merge":
                    return Merge(songs);
                case "Delete":
                    return await Delete(songs, filter);
                case "CleanupAlbums":
                    return await CleanupAlbums(songs);
                default:
                    var list = songs.ToList().AsReadOnly();
                    return await FormatSongList(list, list.Count, new SongFilter());
            }
        }

        //
        // Merge: /Song/Merge
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> MergeResults(string songIds, SongFilter filter)
        {
            var songs = (await Database.FindSongs(songIds.Split(',').Select(Guid.Parse))).ToList();

            // Create a merged version of the song (and commit to DB)

            // Get the logged in user
            var user = await Database.FindUser(User.Identity?.Name);

            var song = await Database.MergeSongs(
                user, songs,
                ResolveStringField(Song.TitleField, songs, Request.Form),
                ResolveStringField(Song.ArtistField, songs, Request.Form),
                ResolveDecimalField(Song.TempoField, songs, Request.Form),
                ResolveIntField(Song.LengthField, songs, Request.Form),
                Request.Form[Song.AlbumListField], new HashSet<string>(Request.Form.Keys));

            Database.RemoveMergeCandidates(songs);

            await DanceStatsManager.ClearCache(Database, true);

            ViewBag.BackAction = "MergeCandidates";

            return View("details", await GetSongDetails(song, filter));
        }

        // CleanMusicServices: /Song/CleanMusicServices
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> CleanMusicServices(SongFilter filter, Guid id,
            string type = "S")
        {
            var song = await Database.FindSong(id);
            if (song == null)
            {
                return ReturnError(
                    HttpStatusCode.NotFound,
                    $"The song with id = {id} has been deleted.");
            }

            var newSong = await CleanMusicServiceSong(song, Database, type);
            if (newSong != null)
            {
                await Database.SaveSong(newSong);
            }

            HelpPage = "song-details";
            return View("Details", newSong ?? song);
        }


        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchUpdateService(SongFilter filter, string serviceType)
        {
            var service = MusicService.GetService(serviceType);
            var musicServiceManager = MusicServiceManager;
            return BatchProcess(
                filter,
                async (dms, song) =>
                    await musicServiceManager.ConditionalUpdateSongAndService(dms, song, service)
                        ? song
                        : null);
        }


        // A= Album
        // B= Broken
        // D= Deprecated Properties
        // S= Spotify Region
        // G= Spotify Genre
        // P= Batch Process
        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchCleanService(SongFilter filter, string type = "D", int count = -1)
        {
            return BatchProcess(filter, (dms, song) => CleanMusicServiceSong(song, dms, type));
        }

        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchCleanupProperties(SongFilter filter, string type = "X")
        {
            return BatchProcess(
                filter, (dms, song) =>
                    Task.FromResult(
                        song.CleanupProperties(Database, type)
                            ? song
                            : null));
        }

        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchReloadSongs(SongFilter filter)
        {
            return BatchProcess(
                filter, async (dms, song) =>
                    await dms.ReloadSong(song)
                        ? song
                        : null);
        }

        private ActionResult BatchProcess(SongFilter filter,
            Func<DanceMusicCoreService, Song, Task<Song>> act, int count = -1)
        {
            try
            {
                StartAdminTask("BatchProcess");
                AdminMonitor.UpdateTask("BatchProcess");

                var changed = new List<Guid>();

                var tried = 0;
                var done = false;

                filter.Page = 1;

                var dms = Database.GetTransientService();
                Task.Run(
                    async () => // Intentionally drop this async on the floor
                    {
                        try
                        {
                            while (!done)
                            {
                                AdminMonitor.UpdateTask(
                                    "BuildSongList",
                                    ((filter.Page ?? 1) - 1) * 500);

                                var parameters = dms.AzureParmsFromFilter(filter, 500);
                                parameters.IncludeTotalCount = false;
                                var res = await dms.Search(
                                    filter.SearchString, parameters,
                                    DanceMusicCoreService.CruftFilter.AllCruft);
                                if (!res.Songs.Any())
                                {
                                    break;
                                }

                                var save = new List<Song>();

                                var processed = 0;
                                foreach (var song in res.Songs)
                                {
                                    try
                                    {
                                        AdminMonitor.UpdateTask(
                                            "Processing",
                                            ((filter.Page ?? 1) - 1) * 500 + processed);

                                        processed += 1;
                                        tried += 1;
                                        var songT = await act(dms, song);
                                        if (songT != null)
                                        {
                                            changed.Add(songT.SongId);
                                        }

                                        if (songT != null)
                                        {
                                            save.Add(songT);
                                        }

                                        if (count > 0 && tried > count)
                                        {
                                            break;
                                        }

                                        if ((tried + 1) % 25 != 0)
                                        {
                                            continue;
                                        }

                                        Trace.WriteLineIf(
                                            TraceLevels.General.TraceInfo,
                                            $"{tried} songs tried.");
                                    }
                                    catch (AbortBatchException e)
                                    {
                                        Trace.WriteLine(
                                            $"Aborted Batch Process at {DateTime.Now}: {e.Message}");
                                        break;
                                    }
                                    catch (Exception e)
                                    {
                                        Trace.WriteLine(
                                            $"{song.Title} by {song.Artist} failed with: {e.Message}");
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

                            AdminMonitor.CompleteTask(
                                true,
                                $"BatchProcess: Completed={tried <= count}, Succeeded={changed.Count} - ({string.Join(",", changed)})");
                        }
                        catch (Exception e)
                        {
                            AdminMonitor.CompleteTask(false, $"BatchProcess: Failed={e.Message}");
                        }
                        finally
                        {
                            dms.Dispose();
                        }
                    });
                return RedirectToAction("AdminStatus", "Admin", AdminMonitor.Status);
            }
            catch (Exception e)
            {
                return FailAdminTask($"BatchProcess: {e.Message}", e);
            }
        }

        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchSamples(SongFilter filter)
        {
            return BatchProcess(
                filter, async (dms, song) =>
                    await MusicServiceManager.GetSampleData(dms, song)
                        ? song
                        : null);
        }

        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchEchoNest(SongFilter filter)
        {
            // TODO: Consider re-instating the option to only lookup songs w/o tempo or beat info
            return BatchProcess(
                filter, async (dms, song) =>
                    await MusicServiceManager.GetEchoData(dms, song)
                        ? song
                        : null);
        }

        #endregion

        #region General Utilities

        private async Task<SearchOptions> AzureParmsFromFilter(
            SongFilter filter, int? pageSize = null)
        {
            return Database.AzureParmsFromFilter(
                await UserMapper.DeanonymizeFilter(filter, UserManager), pageSize);
        }

        private ActionResult HandleRedirect(RedirectException redirect)
        {
            UseVue = false;
            if (redirect.View == "Login" && redirect.Model is SongFilter filter)
            {
                return Redirect(
                    $"/Identity/Account/Login/?ReturnUrl=/song/advancedsearchform?filter={filter}");
            }

            return View(redirect.View, redirect.Model);
        }

        private async Task<ActionResult> Delete(IEnumerable<Song> songs, SongFilter filter)
        {
            var user = await Database.FindUser(UserName);

            foreach (var song in songs)
            {
                await Database.DeleteSong(user, song);
            }

            return RedirectToAction("Index", new { filter });
        }

        #endregion

        #region MusicService

        private async Task<Song> CleanMusicServiceSong(Song song, DanceMusicCoreService dms,
            string type = "X")
        {
            var props = new List<SongProperty>(song.SongProperties);

            var changed = false;

            if (type.IndexOf('X') != -1)
            {
                changed |= CleanDeletedServices(song.SongId, props);
            }

            if (type.IndexOf('B') != -1)
            {
                changed |= await CleanBrokenServices(song, props);
            }

            if (type.IndexOf('A') != -1)
            {
                changed |= CleanOrphanedAlbums(props);
            }

            if (type.IndexOf('D') != -1)
            {
                changed |= CleanDeprecatedProperties(song.SongId, props);
            }

            var updateGenre = type.IndexOf('G') != -1;

            Song newSong = null;
            if (changed || updateGenre)
            {
                newSong = await Song.Create(song.SongId, props, dms);
            }

            if (!updateGenre)
            {
                return newSong;
            }

            return await UpdateSpotifyGenre(newSong, dms) || changed ? newSong : null;
        }

        private async Task<bool> UpdateSpotifyGenre(Song song, DanceMusicCoreService dms)
        {
            var spotify = MusicService.GetService(ServiceType.Spotify);
            var tags = song.GetUserTags(spotify.User);

            foreach (var prop in SpotifySongProperties(song.SongProperties))
            {
                var track = await MusicServiceManager.GetMusicServiceTrack(prop.Value, spotify);
                if (track.Genres is { Length: > 0 })
                {
                    tags = tags.Add(
                        new m4dModels.TagList(
                            dms.NormalizeTags(
                                string.Join("|", track.Genres.Select(m4dModels.TagList.Clean)),
                                "Music")));
                }
            }

            Trace.Write($"Tags={tags}\r\n");

            return song.EditSongTags(spotify.ApplicationUser, tags, dms.DanceStats);
        }

        private IEnumerable<SongProperty> SpotifySongProperties(IEnumerable<SongProperty> props)
        {
            foreach (var prop in props.Where(
                p =>
                    p.Name.StartsWith("Purchase") && p.Name.EndsWith(":SS")))
            {
                yield return prop;
            }
        }

        private async Task<bool> CleanBrokenServices(Song song, ICollection<SongProperty> props)
        {
            var del = new List<SongProperty>();

            foreach (var link in song.GetPurchaseLinks())
            {
                try
                {
                    using var response = await HttpClient.GetAsync(link.Link);
                    if (!response.IsSuccessStatusCode)
                    {
                        if (!string.IsNullOrWhiteSpace(link.SongId))
                        {
                            var t = props.FirstOrDefault(
                                p => p.Name.StartsWith("Purchase") && p.Name.EndsWith("S") &&
                                    p.Value.StartsWith(link.SongId));
                            if (t != null)
                            {
                                del.Add(t);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(link.AlbumId))
                        {
                            var t = props.FirstOrDefault(
                                p => p.Name.StartsWith("Purchase") && p.Name.EndsWith("A") &&
                                    p.Value.StartsWith(link.AlbumId));
                            if (t != null)
                            {
                                del.Add(t);
                            }
                        }

                        Trace.WriteLine($"Removing: {link.Link}");
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Link {link.Link} threw {e.Message}");
                }
            }

            if (del.Count == 0)
            {
                return false;
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Removed: {del.Count}");

            foreach (var prop in del)
            {
                props.Remove(prop);
            }

            return true;
        }

        [Authorize(Roles = "dbAdmin")]
        public async Task<IActionResult> DownloadJson(SongFilter filter, string type = "S",
            int count = 1)
        {
            var p = await AzureParmsFromFilter(filter, 1000);
            p.IncludeTotalCount = true;

            var results = await Database.Search(filter.SearchString, p, filter.CruftFilter);

            switch (type)
            {
                case "H":
                    return JsonCamelCase(
                        results.Songs.Select(
                                s =>
                                    UserMapper.AnonymizeHistory(s.GetHistory(_mapper), UserManager))
                            .ToList());
                default:
                    return JsonCamelCase(null);
            }
        }

        //private bool CleanDeletedServices(Guid songId, ICollection<SongProperty> props)
        //{
        //    var companions = new List<string> {Song.EditCommand, Song.UserField, Song.TimeField};

        //    var del = new List<SongProperty>();
        //    var trailing = new List<SongProperty>();
        //    var addedX = false;
        //    foreach (var prop in props)
        //    {
        //        if (prop.Name.StartsWith("Purchase:") && prop.Name.EndsWith(":XS"))
        //        {
        //            trailing.Add(prop);
        //            addedX = true;
        //        }
        //        else if (companions.Contains(prop.Name))
        //        {
        //            if (addedX)
        //            {
        //                addedX = false;
        //                del.AddRange(trailing);
        //                trailing.Clear();
        //            }
        //            trailing.Add(prop);
        //        }
        //        else
        //        {
        //            if (addedX)
        //            {
        //                Trace.WriteLine($"Song {songId} has unexpected purchase pattern.");
        //            }
        //            trailing.Clear();
        //        }
        //    }

        //    if (addedX)
        //    {
        //        del.AddRange(trailing);
        //    }

        //    if (del.Count == 0) return false;


        //    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Removed: {del.Count}");

        //    foreach (var prop in del)
        //    {
        //        props.Remove(prop);
        //    }

        //    return true;
        //}

        private bool CleanDeletedServices(Guid songId, ICollection<SongProperty> props)
        {
            var companions = new List<string> { Song.EditCommand, Song.UserField, Song.TimeField };

            var del = props
                .Where(prop => prop.Name.StartsWith("Purchase:") && prop.Name.EndsWith(":XS"))
                .ToList();


            if (del.Count == 0)
            {
                return false;
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Removed: {del.Count}");

            foreach (var prop in del)
            {
                props.Remove(prop);
            }

            return true;
        }

        private bool CleanDeprecatedProperties(Guid songId, ICollection<SongProperty> props)
        {
            var del = new List<SongProperty>();
            foreach (var prop in props)
            {
                if (prop.Name.StartsWith("PromoteAlbum:") || prop.Name.StartsWith("OrderAlbums:"))
                {
                    del.Add(prop);
                }
            }

            if (del.Count == 0)
            {
                return false;
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Removed: {del.Count}");

            foreach (var prop in del)
            {
                props.Remove(prop);
            }

            return true;
        }

        private static bool CleanOrphanedAlbums(ICollection<SongProperty> props)
        {
            var del = new List<SongProperty>();
            // Check every purchase link and make sure it's still valid
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var prop in props.Where(
                p =>
                    p.Name.StartsWith("Purchase") && p.Name.EndsWith("A")))
            {
                var s = prop.Name.Substring(0, prop.Name.Length - 1) + 'S';
                if (props.All(p => p.Name != s))
                {
                    del.Add(prop);
                }
            }

            if (del.Count == 0)
            {
                return false;
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Removed: {del.Count}");

            foreach (var prop in del)
            {
                props.Remove(prop);
            }

            return true;
        }

        #endregion

        #region Merge

        private async Task<IReadOnlyCollection<Song>> AutoMerge(IReadOnlyCollection<Song> songs,
            int level)
        {
            // Get the logged in user
            var userName = UserName;
            var user = await Database.FindUser(userName);

            var ret = new List<Song>();
            List<Song> cluster = null;

            try
            {
                foreach (var song in new List<Song>(songs))
                {
                    if (cluster == null)
                    {
                        cluster = new List<Song> { song };
                    }
                    else if (level == 0 && song.Equivalent(cluster[0])
                        || level == 1 && song.WeakEquivalent(cluster[0])
                        || level == 3 && song.TitleArtistEquivalent(cluster[0]))
                    {
                        cluster.Add(song);
                    }
                    else
                    {
                        if (cluster.Count > 1)
                        {
                            var s = await AutoMerge(cluster, user);
                            ret.Add(s);
                        }
                        else if (cluster.Count == 1)
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceInfo,
                                $"Bad Merge: {cluster[0].Title}");
                        }

                        cluster = new List<Song> { song };
                    }
                }
            }
            finally
            {
                await DanceStatsManager.ClearCache(Database, false);
            }

            return ret;
        }

        private async Task<Song> AutoMerge(List<Song> songs, ApplicationUser user)
        {
            // These songs are coming from "light loading", so need to reload the full songs before merging
            songs = (await Database.FindSongs(songs.Select(s => s.SongId))).ToList();

            var song = await Database.MergeSongs(
                user, songs,
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
            var sm = new SongMerge(songs.ToList(), Database.DanceStats);

            UseVue = false;
            return View("Merge", sm);
        }

        private async Task<ActionResult> CleanupAlbums(IEnumerable<Song> songs)
        {
            var user = await Database.FindUser(UserName);
            var scanned = 0;
            var changed = 0;
            var albums = 0;
            foreach (var song in songs)
            {
                var delta = await Database.CleanupAlbums(user, song);
                if (delta > 0)
                {
                    changed += 1;
                    albums += delta;
                }

                scanned += 1;
            }

            ViewBag.Title = "Cleanup Albums";
            ViewBag.Message =
                $"Of {scanned} songs scanned, {changed} where changed.  {albums} were removed.";

            return View("Info");
        }

        private string ResolveStringField(string fieldName, IList<Song> songs,
            IFormCollection form = null)
        {
            return ResolveMergeField(fieldName, songs, form) as string;
        }


        private int? ResolveIntField(string fieldName, IList<Song> songs,
            IFormCollection form = null)
        {
            return ResolveMergeField(fieldName, songs, form) as int?;
        }

        private decimal? ResolveDecimalField(string fieldName, IList<Song> songs,
            IFormCollection form = null)
        {
            return ResolveMergeField(fieldName, songs, form) as decimal?;
        }

        private static object ResolveMergeField(string fieldName, IList<Song> songs,
            IFormCollection form = null)
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

                    if (s.GetType().GetProperty(fieldName)?.GetValue(s) == null)
                    {
                        continue;
                    }

                    idx = i;
                    break;
                }
            }

            var song = songs[idx];
            var type = song.GetType();
            var prop = type.GetProperty(fieldName);
            if (prop != null)
            {
                var o = prop.GetValue(song);
                return o;
            }

            return null;
        }

        #endregion
    }
}
