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
using m4d.Services;
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
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace m4d.Controllers
{
    public class SongController : ContentController
    {
        private static readonly HttpClient HttpClient = new();

        private readonly LinkGenerator _linkGenerator;
        private readonly IMapper _mapper;
        private readonly IBackgroundTaskQueue _backgroundTaskQueue;
        private SongFilter Filter { get; set; }

        public SongController(DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, LinkGenerator linkGenerator,
            IConfiguration configuration, IMapper mapper, IBackgroundTaskQueue queue, ILogger<SongController> logger) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration, logger)
        {
            HelpPage = "song-list";
            UseVue = true;
            _linkGenerator = linkGenerator;
            _mapper = mapper;
            _backgroundTaskQueue = queue;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var user = UserName;
            var request = filterContext.HttpContext.Request;
            var filterString = request.Query["filter"];
            if (string.IsNullOrWhiteSpace(filterString) && request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                filterString = filterContext.HttpContext.Request.Form["filter"];
            }
            if (!string.IsNullOrEmpty(filterString))
            {
                Filter = new SongFilter(filterString);
            }
            else
            {
                Filter = SongFilter.GetDefault(user);
                filterContext.ActionArguments["filter"] = Filter;
            }

            ViewBag.SongFilter = Filter;
            base.OnActionExecuting(filterContext);
        }

        private CruftFilter DefaultCruftFilter()
        {
            return User.IsInRole(DanceMusicCoreService.DiagRole) ||
                User.IsInRole(DanceMusicCoreService.PremiumRole) ||
                User.IsInRole(DanceMusicCoreService.TrialRole)
                    ? CruftFilter.AllCruft
                    : CruftFilter.NoCruft;
        } 

        #region Commands

        [AllowAnonymous]
        public async Task<ActionResult> Search(string searchString, string dances)
        {
            return await AzureSearch(searchString, 0, dances);
        }

        [AllowAnonymous]
        public async Task<ActionResult> NewMusic(string type = null, int? page = null)
        {
            Filter.Action = "newmusic";
            if (type != null)
            {
                Filter.SortOrder = type;
            }

            if (string.IsNullOrWhiteSpace(Filter.SortOrder))
            {
                Filter.SortOrder = "Created";
            }

            if (page != null)
            {
                Filter.Page = page;
            }

            Filter.User = new UserQuery("dgsnure", false).Query;

            return await DoAzureSearch();
        }

        [AllowAnonymous]
        public async Task<ActionResult> HolidayMusic(string dance = null, int page = 1)
        {
            Filter = SongFilter.CreateHolidayFilter(dance, page);
            HelpPage = Filter.IsSimple ? "song-list" : "advanced-search";

            try
            {
                if (!Filter.IsEmptyBot &&
                    SpiderManager.CheckAnySpiders(Request.Headers[HeaderNames.UserAgent]))
                {
                    throw new RedirectException("BotFilter", Filter);
                }

                var results = await new SongSearch(
                    Filter, UserName, IsPremium(), SongIndex, UserManager, _backgroundTaskQueue).Search();

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
                        Filter = _mapper.Map<SongFilterSparse>(Filter),
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
        public async Task<ActionResult> AzureSearch(string searchString, int page = 1, string dances = null)
        {
            if (string.IsNullOrWhiteSpace(dances))
            {
                dances = null;
            }

            if (dances != null &&
                !string.Equals(dances, Filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                Filter.Dances = dances;
                Filter.Page = 1;

                if (string.IsNullOrWhiteSpace(Filter.SortOrder) && Filter.DanceQuery.Dances.Count() == 1)
                {
                    Filter.SortOrder = "Dances";
                }
            }

            if (searchString != null)
            {
                Filter.SearchString = searchString;
            }

            if (page != 0)
            {
                Filter.Page = page;
            }

            Filter.Purchase = null;
            Filter.TempoMin = null;
            Filter.TempoMax = null;

            return await DoAzureSearch();
        }

        // TODO: Consider abstracting this (and maybe format) out into a builder/computer
        private async Task<ActionResult> DoAzureSearch()
        {
            HelpPage = Filter.IsSimple ? "song-list" : "advanced-search";

            try
            {
                if (!Filter.IsEmptyBot &&
                    SpiderManager.CheckAnySpiders(Request.Headers[HeaderNames.UserAgent]))
                {
                    throw new RedirectException("BotFilter", Filter);
                }

                var results = await new SongSearch(Filter, UserName, IsPremium(), SongIndex, UserManager, _backgroundTaskQueue).Search();
                return await FormatResults(results);
            }
            catch (RedirectException ex)
            {
                return HandleRedirect(ex);
            }

        }

        private async Task<ActionResult> FormatResults(SearchResults results)
        {
            return await FormatSongList(results.Songs.ToList(), (int)results.TotalCount, (int) results.RawCount);
        }

        private async Task<ActionResult> FormatSongList(IReadOnlyCollection<Song> songs,
            int? totalSongs = null, int? rawCount = null, List<string> hiddenColumns = null)
        {

            var user = UserName;
            if (user != null)
            {
                Filter.Anonymize(user);
            }

            var dictionary = await UserMapper.GetUserNameDictionary(UserManager);
            var histories = songs.Select(
                s =>
                    UserMapper.AnonymizeHistory(s.GetHistory(_mapper), dictionary)).ToList();
                
            return Vue("Songs for Dancing", $"music4dance catalog: {Filter.Description}", Filter.VueName,
                new SongListModel
                {
                    Histories = histories,
                    Filter = _mapper.Map<SongFilterSparse>(Filter),
                    Count = totalSongs ?? songs.Count,
                    RawCount = rawCount ?? totalSongs ?? songs.Count,
                    HiddenColumns = hiddenColumns
                },
                danceEnvironment: true);
        }


        //
        // GET: /Song/RawSearchForm
        [AllowAnonymous]
        public ActionResult RawSearchForm()
        {
            HelpPage = "advanced-search";

            ViewBag.AzureIndexInfo = Database.SongIndex.GetIndex();
            UseVue = false;
            return View(new RawSearch(Filter is { IsRaw: true } ? Filter : null));
        }

        //
        // GET: /Song/RawSearch
        [AllowAnonymous]
        public async Task<ActionResult> RawSearch(
            [Bind(
                "SearchText,ODataFilter,SortFields,SearchFields,Description,IsLucene,CruftFilter")]
            RawSearch rawSearch)
        {
            HelpPage = "advanced-search";

            Filter = new SongFilter(rawSearch);
            ViewBag.AzureIndexInfo = Database.SongIndex.GetIndex();
            return ModelState.IsValid
                ? await DoAzureSearch()
                : View("RawSearchForm", rawSearch);
        }

        //
        // GET: /Song/AdvancedSearchForm
        [AllowAnonymous]
        public ActionResult AdvancedSearchForm()
        {
            return Vue(
                "Advanced Search",
                $"music4dance advanced song search form: {Filter.Description}",
                "advanced-search", helpPage: "advanced-search",
                danceEnvironment: true, tagEnvironment: true);
        }

        [AllowAnonymous]
        public async Task<ActionResult> FilterSearch()
        {
            return await DoAzureSearch();
        }

        // Get: /Song/AdvancedSearch
        [AllowAnonymous]
        public async Task<ActionResult> AdvancedSearch(string searchString = null,
            string dances = null,
            string tags = null, ICollection<string> services = null, decimal? tempoMin = null,
            decimal? tempoMax = null, string user = null, string sortOrder = null,
            string sortDirection = null, ICollection<int> bonusContent = null)
        {
            if (!Filter.IsAdvanced)
            {
                Filter.Action = Filter.IsAzure ? "azure+advanced" : "Advanced";
            }

            if (string.IsNullOrWhiteSpace(searchString))
            {
                searchString = null;
            }

            if (!string.Equals(searchString, Filter.SearchString))
            {
                Filter.SearchString = searchString;
                Filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(dances))
            {
                dances = null;
            }

            if (!string.Equals(dances, Filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                Filter.Dances = dances;
                Filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(tags))
            {
                tags = null;
            }

            if (!string.Equals(tags, Filter.Tags, StringComparison.OrdinalIgnoreCase))
            {
                Filter.Tags = tags;
                Filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(user))
            {
                user = null;
            }

            if (!string.Equals(user, Filter.User, StringComparison.OrdinalIgnoreCase))
            {
                Filter.User = user;
                Filter.Page = 1;
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

            if (!string.Equals(sortOrder, Filter.SortOrder, StringComparison.OrdinalIgnoreCase))
            {
                Filter.SortOrder = sortOrder;
                Filter.Page = 1;
            }

            var purchase = string.Empty;
            if (services != null)
            {
                purchase = string.Concat(services);
            }

            if (Filter.Purchase != purchase)
            {
                Filter.Purchase = purchase;
                Filter.Page = 1;
            }

            if (Filter.TempoMin != tempoMin || Filter.TempoMax != tempoMax)
            {
                Filter.TempoMin = tempoMin;
                Filter.TempoMax = tempoMax;
                Filter.Page = 1;
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

            if (Filter.Level != level)
            {
                Filter.Level = level;
                Filter.Page = 1;
            }

            return await DoAzureSearch();
        }


        [AllowAnonymous]
        public async Task<ActionResult> Sort(string sortOrder)
        {
            Filter.SortOrder = SongSort.DoSort(sortOrder, Filter.SortOrder);

            return await DoAzureSearch();
        }

        [AllowAnonymous]
        public async Task<ActionResult> FilterUser(string user)
        {
            Filter.User = string.IsNullOrWhiteSpace(user) ? null : user;
            return await DoAzureSearch();
        }

        [AllowAnonymous]
        public async Task<ActionResult> FilterService(ICollection<string> services)
        {
            var purchase = string.Empty;
            if (services != null)
            {
                purchase = string.Concat(services);
            }

            if (Filter.Purchase == purchase)
            {
                return await DoAzureSearch();
            }

            Filter.Purchase = purchase;
            Filter.Page = 1;
            return await DoAzureSearch();
        }

        [AllowAnonymous]
        public async Task<ActionResult> FilterTempo(decimal? tempoMin, decimal? tempoMax)
        {
            if (Filter.TempoMin == tempoMin && Filter.TempoMax == tempoMax)
            {
                return await DoAzureSearch();
            }

            Filter.TempoMin = tempoMin;
            Filter.TempoMax = tempoMax;
            Filter.Page = 1;

            return await DoAzureSearch();
        }

        //
        // GET: /Index/
        [AllowAnonymous]
        public async Task<ActionResult> Index(string id = null, int? page = null,
            string purchase = null)
        {
            if (id != null && Dances.Instance.DanceFromId(id) != null)
            {
                Filter.Dances = id.ToUpper();
            }

            if (page.HasValue)
            {
                Filter.Page = page;
            }

            if (Identity.IsAuthenticated && Filter.IsEmpty)
            {
                Filter.User = new UserQuery(UserName, false, false).Query;
            }

            if (!string.IsNullOrWhiteSpace(purchase))
            {
                Filter.Purchase = purchase;
            }

            return await DoAzureSearch();
        }

        //
        // GET: /AdvancedIndex/
        // TODO: Figure out if this ever gets called
        [AllowAnonymous]
        public async Task<ActionResult> Advanced(int? page, string purchase)
        {
            return await Index( null, page, purchase);
        }

        [AllowAnonymous]
        public async Task<ActionResult> Tags(string tags)
        {
            Filter.Tags = null;
            Filter.Page = null;

            if (string.IsNullOrWhiteSpace(tags))
            {
                return await DoAzureSearch();
            }

            var list = new m4dModels.TagList(tags).AddMissingQualifier('+');
            Filter.Tags = list.ToString();

            BuildEnvironment(tagDatabase: true);
            return await DoAzureSearch();
        }

        [AllowAnonymous]
        public async Task<ActionResult> AddTags(string tags)
        {
            var add = new m4dModels.TagList(tags);
            var old = new m4dModels.TagList(Filter.Tags);

            // First remove any tags from the old list
            old = old.Subtract(add);
            var ret = old.Add(add.AddMissingQualifier('+'));
            Filter.Tags = ret.ToString();
            Filter.Page = null;

            return await DoAzureSearch();
        }

        [AllowAnonymous]
        public async Task<ActionResult> RemoveTags(string tags)
        {
            var sub = new m4dModels.TagList(tags);
            var old = new m4dModels.TagList(Filter.Tags);
            var ret = old.Subtract(sub);
            Filter.Tags = ret.ToString();
            Filter.Page = null;

            return await DoAzureSearch();
        }

        //
        // GET: /Song/Details/5

        [AllowAnonymous]
        public async Task<ActionResult> Details(Guid? id = null)
        {
            var spider = CheckSpiders();
            if (spider != null)
            {
                return spider;
            }

            var gid = id ?? Guid.Empty;
            var song = id.HasValue ? await SongIndex.FindSong(id.Value) : null;
            if (song == null)
            {
                song = await SongIndex.FindMergedSong(gid);
                return song != null
                    ? RedirectToActionPermanent(
                        "details",
                        new { id = song.SongId.ToString(), Filter })
                    : ReturnError(
                        HttpStatusCode.NotFound,
                        $"The song with id = {gid} has been deleted.");
            }

            var details = await GetSongDetails(song);
            return Vue(details.Title, $"music4dance catalog: {details.Title} dance information", "song",
                details, helpPage: "song-details");
        }

        private async Task<SongDetailsModel> GetSongDetails(Song song)
        {
            BuildEnvironment(danceEnvironment: true, tagDatabase: true);
            return new()
            {
                Title = song.Title,
                SongHistory = await UserMapper.AnonymizeHistory(
                    song.GetHistory(_mapper), UserManager),
                Filter = _mapper.Map<SongFilterSparse>(Filter),
                UserName = UserName,
            };
        }

        [AllowAnonymous]
        public async Task<ActionResult> Album(string title)
        {
            try
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
            catch
            {
                return ReturnError(HttpStatusCode.NotFound, $"{title} album title not found.");
            }
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
        public async Task<ActionResult> UpdateSongAndServices(Guid id)
        {
            var song = await SongIndex.FindSong(id);
            if (song == null)
            {
                ReturnError(HttpStatusCode.NotFound, $"The song with id = {id} has been deleted.");
            }

            if (await MusicServiceManager.UpdateSongAndServices(Database, song))
            {
                await SaveSong(song);
            }

            HelpPage = "song-details";
            return await Details(song?.SongId);
        }

        // VUEDTODO: Do we still support this????
        //
        // GET: /Song/Delete/5
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var song = await SongIndex.FindSong(id);
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
        public async Task<ActionResult> DeleteConfirmed(Guid id)
        {
            var song = await SongIndex.FindSong(id);
            var userName = User.Identity?.Name;
            var user = await Database.FindUser(userName);
            await SongIndex.DeleteSong(user, song);

            return RedirectToAction("Index", new { Filter });
        }

        //
        // POST: /Song/BatchCorrectTempo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> BatchCorrectTempo(
            decimal multiplier = 0.5M,
            string user = null)
        {
            var applicationUser = user == null
                ? new ApplicationUser("tempo-bot", true)
                : await Database.FindOrAddUser(user);

            return BatchAdminExecute(
                Filter,
                async (dms, song) =>
                    await dms.SongIndex.CorrectTempoSong(song, applicationUser, multiplier),
                "BatchCorrectTempo");
        }

        //
        // POST: /Song/BatchAdminEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> BatchAdminEdit(SongFilter filter, string properties, string user = null)
        {
            Debug.Assert(User.Identity != null, "User.Identity != null");
            var applicationUser = await Database.FindUser(user ?? UserName);
            return BatchAdminExecute(
                Filter, (dms, song) =>
                    dms.SongIndex.AdminAppendSong(song, applicationUser, properties), "BatchAdminEdit");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchAdminModify(string properties)
        {
            return BatchAdminExecute(
                Filter,
                (dms, song) => dms.SongIndex.AdminModifySong(song, properties),
                "BatchAdminModify");
        }


        private ActionResult BatchAdminExecute(SongFilter filter,
            Func<DanceMusicCoreService, Song, Task<bool>> act, string name)
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
                            var results = await dms.SongIndex.Search(
                                filter, 1000, CruftFilter.AllCruft); var songs = results.Songs;

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

                                Logger.LogInformation($"{tried} songs tried.");
                            }

                            await dms.SongIndex.UpdateAzureIndex(succeeded.Concat(failed), dms);


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

            var song = await SongIndex.FindSong(id);
            if (await SongIndex.CleanupAlbums(user, song) != 0)
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
        public async Task<ActionResult> UndoUserChanges(Guid id, string userName = null)
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
            return RedirectToAction("Details", new { id, Filter });
        }


        [HttpGet]
        public async Task<ActionResult> CreateSpotify()
        {
            HelpPage = "spotify-playlist";
            UseVue = false;

            var authResult = await HttpContext.AuthenticateAsync();
            var canSpotify = await AdmAuthentication.HasAccess(
                Configuration, ServiceType.Spotify, User, authResult);

            return View(
                new SpotifyCreateInfo
                {
                    Title = string.IsNullOrWhiteSpace(Filter.ShortDescription)
                        ? "music4dance playlist"
                        : Filter.ShortDescription,
                    DescriptionPrefix =
                        "This playlist was created with information from music4dance.net: ",
                    Description = Filter.Description,
                    Count = 25,
                    Filter = Filter.ToString(),
                    IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                    IsPremium = User.IsInRole("premium") || User.IsInRole("trial"),
                    CanSpotify = canSpotify
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateSpotify([FromServices]IFileProvider fileProvider,
            [Bind("Title,DescriptionPrefix,Description,Count,Filter")]
            SpotifyCreateInfo info)
        {
            UseVue = false;
            var authResult = await HttpContext.AuthenticateAsync();
            var canSpotify = (await AdmAuthentication.GetServiceAuthorization(
                Configuration, ServiceType.Spotify, User, authResult)) != null;

            info.IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
            info.IsPremium = User.IsInRole("premium") || User.IsInRole("trial");
            info.CanSpotify = canSpotify;

            var filter = new SongFilter(info.Filter)
            {
                Purchase = "S"
            };
            ViewBag.SongFilter = filter;

            HelpPage = "spotify-playlist";

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

            PlaylistMetadata metadata = null;

            try
            {
                Logger.LogInformation($"CreateSpotify: {LogCreateInfo(info)}");
                var p = await AzureParmsFromFilter(filter, info.Count);
                p.IncludeTotalCount = true;
                var results = await new SongSearch(
                    filter, UserName, true, SongIndex, UserManager, _backgroundTaskQueue, info.Count).Search();

                var tracks = results?.Songs?.Select(s => s.GetPurchaseId(ServiceType.Spotify));
                var service = MusicService.GetService(ServiceType.Spotify);
                metadata = await MusicServiceManager.CreatePlaylist(
                    service, User, info.Title,
                    $"{info.DescriptionPrefix} {filter.Description}", fileProvider);

                Logger.LogInformation($"CreateSpotify: {LogMetaData(metadata)}");
                if (!await MusicServiceManager.SetPlaylistTracks(service, User, metadata.Id, tracks))
                {
                    ViewBag.StatusMessage = "Unable to set the playlist tracks.";
                    return View("Error");
                }
            }
            catch (Exception e)
            {
                var metaString = LogMetaData(metadata);
                var message = $"Unable to create a Spotify playlist at this time.  Please report the issue. ({e.Message}) {metaString}";
                Logger.LogError(e, message);
                ViewBag.StatusMessage = message;
                ViewBag.Exception = e;
                return View("Error");
            }

            var user = await Database.FindUser(User.Identity?.Name);
            Database.Context.ActivityLog.Add(new ActivityLog(
                "SpotifyExport", user, new SpotifyCreate { Id = metadata.Id, Info = info }));
            await Database.SaveChanges();
            return View("SpotifyCreated", metadata);
        }

        private string LogMetaData(PlaylistMetadata metadata)
        {
            var metaString = "MetaData = null";
            if (metadata != null)
            {
                metaString = $"MetaData = {{Id: {metadata.Id}, Name: {metadata.Name}, Description: {metadata.Description}, Link: {metadata.Link}, Reference: {metadata.Reference}, Count: {metadata.Count ?? -1}}}";
            }
            return metaString;
        }

        private string LogCreateInfo(SpotifyCreateInfo info)
        {
            var infoString = "SpotifyCreateInfo = null";
            if (info != null)
            {
                infoString = $"SpotifyCreateInfo = {{Title: {info.Title}, DescriptionPrefix: {info.DescriptionPrefix}, Description: {info.Description}, Filter: {info.Filter}, Count: {info.Count}, IsAuthenticated: {info.IsAuthenticated}, IsPremium: {info.IsPremium}, CanSpotify: {info.CanSpotify}}}";
            }
            return infoString;
        }

        [HttpGet]
        public ActionResult ExportPlaylist()
        {
            HelpPage = "export-playlist";
            UseVue = false;

            var isUserOnly = IsUserOnly(Filter);
            var userQuery = Filter.UserQuery;
            var title = isUserOnly
                ? $"{userQuery.UserName}'s Songs"
                : string.IsNullOrWhiteSpace(Filter.ShortDescription)
                    ? "music4dance playlist"
                    : Filter.Filename;
            return View(
                new ExportInfo
                {
                    Title = title,
                    Count = 100,
                    Filter = Filter.ToString(),
                    IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                    IsPremium = User.IsInRole("premium") || User.IsInRole("trial"),
                    IsSelf = isUserOnly 
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportPlaylist(
            [Bind("Title,DescriptionPrefix,IncludeSpecificDances,Count,Filter")]
            ExportInfo info)
        {
            HelpPage = "export-playlist";
            UseVue = false;

            info.IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
            info.IsPremium = User.IsInRole(DanceMusicCoreService.PremiumRole) || User.IsInRole(DanceMusicCoreService.TrialRole);
            var filter = new SongFilter(info.Filter);
            var isUserOnly = IsUserOnly(filter);
            info.Description = isUserOnly ? $"All of {filter.UserQuery.UserName}'s votes and tags" : filter.Description;
            info.Count =  isUserOnly ? 5000 : 100;

            if (!ModelState.IsValid)
            {
                return View(info);
            }

            var user = await Database.FindUser(User.Identity?.Name);
            Database.Context.ActivityLog.Add(new ActivityLog("CsvExport", user, info));
            await Database.SaveChanges();

            var spotifyId = await SpotifyFromFilter(filter, UserName);
            var userName = isUserOnly && User.IsInRole(DanceMusicCoreService.DiagRole)
                ? filter.UserQuery.UserName
                : UserName;
            var exporter = new PlaylistExport(info, SongIndex, UserManager, _backgroundTaskQueue, spotifyId);
            var file = info.IncludeSpecificDances
                ? await exporter.ExportFilteredDances(userName)
                : await exporter.Export(userName);

            return File(file, "text/csv", info.Title + ".csv");
        }

        private bool IsUserOnly(SongFilter filter)
        {
            var isUserOnly = filter.IsUserOnly;
            var isDefault = string.Equals(filter.UserQuery?.UserName, UserName, StringComparison.InvariantCultureIgnoreCase);
            var isDiag = User.IsInRole(DanceMusicCoreService.DiagRole);
            return isUserOnly && (isDefault || isDiag);
        }

        //
        // Merge: /Song/MergeCandidates
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> MergeCandidates(int? page, int? level, bool? autoCommit)
        {
            Filter.Action = "MergeCandidates";

            if (page.HasValue)
            {
                Filter.Page = page;
            }

            if (level.HasValue)
            {
                Filter.Level = level;
            }

            var songs =
                await Database.FindMergeCandidates(
                    autoCommit == true ? 10000 : 500, Filter.Level ?? 1);

            if (autoCommit.HasValue && autoCommit.Value)
            {
                songs = await AutoMerge(songs, Filter.Level ?? 1);
            }

            return await FormatSongList(songs, hiddenColumns: new List<string> { "dances", "echo", "length", "order", "play", "tags", "track" });
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
        public async Task<ActionResult> UpdateRatings(Guid id)
        {
            var song = await SongIndex.FindSong(id);
            if (song == null)
            {
                return ReturnError(
                    HttpStatusCode.NotFound,
                    $"The song with id = {id} has been deleted.");
            }

            song.SetRatingsFromProperties();
            await SaveSong(song);

            HelpPage = "song-details";

            return await Details(song?.SongId);
        }


        //
        // BulkEdit: /Song/BulkEdit
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> BulkEdit(Guid[] selectedSongs, string action)
        {
            var songs = await SongIndex.FindSongs(selectedSongs);

            switch (action)
            {
                case "Merge":
                    return Merge(songs);
                case "SimpleMerge":
                    return SongMerge(songs);
                case "Delete":
                    return await Delete(songs, Filter);
                case "CleanupAlbums":
                    return await CleanupAlbums(songs);
                default:
                    var list = songs.ToList().AsReadOnly();
                    return await FormatSongList(list, list.Count);
            }
        }

        //
        // Merge: /Song/Merge
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> MergeResults(string songIds)
        {
            var songs = (await SongIndex.FindSongs(songIds.Split(',').Select(Guid.Parse))).ToList();

            // Create a merged version of the song (and commit to DB)

            // Get the logged in user
            var user = await Database.FindUser(User.Identity?.Name);

            var song = await SongIndex.MergeSongs(
                user, songs,
                ResolveStringField(Song.TitleField, songs, Request.Form),
                ResolveStringField(Song.ArtistField, songs, Request.Form),
                ResolveDecimalField(Song.TempoField, songs, Request.Form),
                ResolveIntField(Song.LengthField, songs, Request.Form),
                Request.Form[Song.AlbumListField], new HashSet<string>(Request.Form.Keys));

            Database.RemoveMergeCandidates(songs);

            await DanceStatsManager.ClearCache(Database, true);

            ViewBag.BackAction = "MergeCandidates";

            return await Details(song?.SongId);
        }

        // CleanMusicServices: /Song/CleanMusicServices
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> CleanMusicServices(SongFilter filter, Guid id,
            string type = "S")
        {
            var song = await SongIndex.FindSong(id);
            if (song == null)
            {
                return ReturnError(
                    HttpStatusCode.NotFound,
                    $"The song with id = {id} has been deleted.");
            }

            var newSong = await CleanMusicServiceSong(song, Database, type);
            if (newSong != null)
            {
                await SongIndex.SaveSong(newSong);
            }

            HelpPage = "song-details";
            return await Details(newSong?.SongId ?? song.SongId);
        }


        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchUpdateService(string serviceType)
        {
            var service = MusicService.GetService(serviceType);
            var musicServiceManager = MusicServiceManager;
            return BatchProcess(
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
        public ActionResult BatchCleanService(string type = "D", int count = -1)
        {
            return BatchProcess((dms, song) => CleanMusicServiceSong(song, dms, type));
        }

        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchCleanupProperties(string type = "OYSMPNE")
        {
            return BatchProcess(
                 async (dms, song) =>
                     await song.CleanupProperties(dms, type)
                            ? song
                            : null);
        }

        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchReloadSongs()
        {
            return BatchProcess(
                async (dms, song) =>
                    await dms.SongIndex.ReloadSong(song)
                        ? song
                        : null);
        }

        [Authorize(Roles = "dbAdmin")]
        public ActionResult CheckProperties()
        {
            return BatchProcess(
                async (dms, song) =>
                    await dms.SongIndex.CheckProperties(song)
                        ? null
                        : song, preview: true);
        }

        private ActionResult BatchProcess(
            Func<DanceMusicCoreService, Song, Task<Song>> act, int count = -1, bool preview = false)
        {
            try
            {
                StartAdminTask("BatchProcess");
                AdminMonitor.UpdateTask("BatchProcess");

                var changed = new List<Guid>();

                var tried = 0;
                var done = false;

                Filter.Page = 1;

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
                                    ((Filter.Page ?? 1) - 1) * 500);

                                var parameters = dms.SongIndex.AzureParmsFromFilter(Filter, 500);
                                parameters.IncludeTotalCount = false;
                                var res = await dms.SongIndex.Search(
                                    Filter.SearchString, parameters,
                                    CruftFilter.AllCruft);
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
                                            ((Filter.Page ?? 1) - 1) * 500 + processed);

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

                                        Logger.LogInformation($"{tried} songs tried.");
                                    }
                                    catch (AbortBatchException e)
                                    {
                                        Logger.LogWarning($"Aborted Batch Process at {DateTime.Now}: {e.Message}");
                                        break;
                                    }
                                    catch (Exception e)
                                    {
                                        Logger.LogError($"{song.Title} by {song.Artist} failed with: {e.Message}");
                                    }
                                }

                                if (!preview && save.Count > 0)
                                {
                                    dms.SongIndex.SaveSongsImmediate(save);
                                }

                                Filter.Page += 1;
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
        public ActionResult BatchSamples()
        {
            return BatchProcess(
                async (dms, song) =>
                    await MusicServiceManager.GetSampleData(dms, song)
                        ? song
                        : null);
        }

        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchEchoNest()
        {
            // TODO: Consider re-instating the option to only lookup songs w/o tempo or beat info
            return BatchProcess(
                async (dms, song) =>
                    await MusicServiceManager.GetEchoData(dms, song)
                        ? song
                        : null);
        }

        #endregion

        #region General Utilities

        private async Task<SearchOptions> AzureParmsFromFilter(
            SongFilter filter, int? pageSize = null)
        {
            return SongIndex.AzureParmsFromFilter(
                await UserMapper.DeanonymizeFilter(filter, UserManager), pageSize);
        }

        private bool IsPremium()
        {
            return User.IsInRole(DanceMusicCoreService.PremiumRole) ||
                User.IsInRole(DanceMusicCoreService.TrialRole) ||
                User.IsInRole(DanceMusicCoreService.DiagRole);
        }

        private ActionResult HandleRedirect(RedirectException redirect)
        {
            UseVue = false;
            var model = redirect.Model;
            if (redirect.View == "Login" && model is SongFilter filter)
            {
                return Redirect(
                    $"/Identity/Account/Login/?ReturnUrl=/song/advancedsearchform?filter={filter}");
            }

            if (redirect.View == "RequiresPremium")
            {
                Filter.Level = null;
                var redirectUrl =
                    _linkGenerator.GetUriByAction(
                        HttpContext, "AdvancedSearchForm", "Song",
                        new { Filter });
                model = new PremiumRedirect
                {
                    FeatureType = "search",
                    FeatureName = "bonus content",
                    InfoUrl = "https://music4dance.blog/?page_id=8217",
                    RedirectUrl = redirectUrl
                };
            }

            return View(redirect.View, model);
        }

        private async Task<ActionResult> Delete(IEnumerable<Song> songs, SongFilter filter)
        {
            var user = await Database.FindUser(UserName);

            foreach (var song in songs)
            {
                await SongIndex.DeleteSong(user, song);
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

                        Logger.LogInformation($"Removing: {link.Link}");
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Link {link.Link} threw {e.Message}");
                }
            }

            if (del.Count == 0)
            {
                return false;
            }

            Logger.LogInformation($"Removed: {del.Count}");

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

            var results = await SongIndex.Search(filter.SearchString, p, filter.CruftFilter);

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

            Logger.LogInformation($"Removed: {del.Count}");

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

            Logger.LogInformation($"Removed: {del.Count}");

            foreach (var prop in del)
            {
                props.Remove(prop);
            }

            return true;
        }

        private bool CleanOrphanedAlbums(ICollection<SongProperty> props)
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

            Logger.LogInformation($"Removed: {del.Count}");

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
                            Logger.LogInformation($"Bad Merge: {cluster[0].Title}");
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
            songs = (await SongIndex.FindSongs(songs.Select(s => s.SongId))).ToList();

            var song = await SongIndex.MergeSongs(
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

        private ActionResult SongMerge(IEnumerable<Song> songs)
        {
            var sm = new SongMergeModel(songs.Select(s => s.GetHistory(_mapper)));
            var title = "Merge Songs";
            return Vue(title, $"music4dance catalog: {title} dance information", "song-merge",
                sm, helpPage: "song");

        }


        private async Task<ActionResult> CleanupAlbums(IEnumerable<Song> songs)
        {
            var user = await Database.FindUser(UserName);
            var scanned = 0;
            var changed = 0;
            var albums = 0;
            foreach (var song in songs)
            {
                var delta = await SongIndex.CleanupAlbums(user, song);
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
