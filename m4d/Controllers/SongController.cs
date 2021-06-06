using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AutoMapper;
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
using Microsoft.AspNetCore.Mvc.Rendering;
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
            var user = User.Identity.Name;
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
        public ActionResult Search(string searchString, string dances, SongFilter filter)
        {
            if (string.IsNullOrWhiteSpace(searchString)) searchString = null;
            if (!string.Equals(searchString, filter.SearchString))
            {
                filter.SearchString = searchString;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(dances)) dances = null;

            if (!string.Equals(dances, filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                filter.Dances = dances;
                filter.Page = 1;

                if (string.IsNullOrWhiteSpace(filter.SortOrder)) filter.SortOrder = "Dances";
            }

            if (filter.Dances != null)
            {
                var stats = Database.DanceStats;
                var dq = filter.DanceQuery;
                foreach (var d in dq.DanceIds)
                {
                    var ds = stats.FromId(d);
                    if (ds == null)
                        return ReturnError(HttpStatusCode.NotFound,
                            $"Dance id = {d} is not defined.");
                    if (ds.SongCount == 0)
                        return RedirectToAction("Index", "Dances", new {dance = ds.SeoName});
                }
            }

            filter.Purchase = null;
            filter.TempoMin = null;
            filter.TempoMax = null;

            return DoAzureSearch(filter);
        }

        [AllowAnonymous]
        public ActionResult NewMusic(string type = null, int? page = null, SongFilter filter = null)
        {
            filter ??= new SongFilter();
            filter.Action = "newmusic";
            if (type != null) filter.SortOrder = type;
            if (string.IsNullOrWhiteSpace(filter.SortOrder)) filter.SortOrder = "Created";
            if (page != null) filter.Page = page;

            if (User.Identity.IsAuthenticated && filter.IsEmpty)
                filter.User = new UserQuery(User.Identity.Name, false, false).Query;

            return VueAzureSearch(filter, true);
        }

        [AllowAnonymous]
        public ActionResult HolidayMusic(string dance = null, int page = 1)
        {
            var filter = SongFilter.CreateHolidayFilter(dance, page);

            var ret = BuildAzureSearch(filter, out var results);
            if (ret != null) return ret;

            string playListId = null;

            if (!string.IsNullOrWhiteSpace(dance))
            {
                var ds = Database.DanceStats.FromName(dance);
                var name = $"Holiday {ds.DanceName}";
                var playlist = Database.PlayLists.FirstOrDefault(
                    p => p.Name == name && p.Type == PlayListType.SpotifyFromSearch);
                playListId = playlist?.Id;
            }

            var songs = results.Songs.Select(s => _mapper.Map<SongSparse>(s)).ToList();
            var histories = results.Songs.Select(s => s.GetHistory(_mapper)).ToList();
            return View(filter.Action, new HolidaySongListModel
            {
                Songs = songs,
                Histories = histories,
                Filter = _mapper.Map<SongFilterSparse>(filter),
                UserName = User.Identity.Name,
                Count = (int) results.TotalCount,
                Dance = dance,
                PlayListId = playListId,
                Validate = false
            });
        }

        [AllowAnonymous]
        public ActionResult AzureSearch(string searchString, SongFilter filter, int page = 1,
            string dances = null)
        {
            if (User.Identity.IsAuthenticated && filter.IsEmpty)
                filter.User = new UserQuery(User.Identity.Name, false, false).Query;

            if (string.IsNullOrWhiteSpace(dances)) dances = null;

            if (dances != null &&
                !string.Equals(dances, filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                filter.Dances = dances;
                filter.Page = 1;

                if (string.IsNullOrWhiteSpace(filter.SortOrder)) filter.SortOrder = "Dances";
            }

            if (searchString != null) filter.SearchString = searchString;

            if (page != 0)
                filter.Page = page;

            return DoAzureSearch(filter);
        }

        private ActionResult DoAzureSearch(SongFilter filter, string page = "azuresearch")
        {
            var ret = BuildAzureSearch(filter, out var results);
            if (ret != null) return ret;

            return VueAzureSearch(results, filter);
        }

        private ActionResult BuildAzureSearch(SongFilter filter, out SearchResults results)
        {
            HelpPage = filter.IsSimple ? "song-list" : "advanced-search";
            results = null;

            if (!filter.IsEmptyBot &&
                SpiderManager.CheckAnySpiders(Request.Headers[HeaderNames.UserAgent]))
            {
                UseVue = false;
                return View("BotFilter", filter);
            }

            if (filter.Level != null && filter.Level != 0 &&
                !(User.IsInRole(DanceMusicCoreService.PremiumRole) ||
                    User.IsInRole(DanceMusicCoreService.TrialRole) ||
                    User.IsInRole(DanceMusicCoreService.DiagRole)))
            {
                filter.Level = null;
                var redirectUrl =
                    _linkGenerator.GetUriByAction(HttpContext, "AdvancedSearchForm", "Song",
                        new {filter});
                var premiumRedirect = new PremiumRedirect
                {
                    FeatureType = "search",
                    FeatureName = "bonus content",
                    InfoUrl = "https://music4dance.blog/?page_id=8217",
                    RedirectUrl = redirectUrl
                };
                return View("RequiresPremium", premiumRedirect);
            }

            var userQuery = filter.UserQuery;
            if (!userQuery.IsEmpty && userQuery.IsAnonymous)
            {
                if (User.Identity.IsAuthenticated)
                {
                    var userName = User.Identity.Name;
                    filter.User = new UserQuery(userQuery, userName).Query;
                }
                else
                {
                    return LoginRedirect(filter);
                }
            }

            var p = Database.AzureParmsFromFilter(filter, 25);
            p.IncludeTotalResultCount = true;

            results = Database.AzureSearch(filter.SearchString, p, filter.CruftFilter,
                User.Identity.Name);

            ViewBag.RawSearch = p;

            return null;
        }

        private ActionResult VueAzureSearch(SongFilter filter, bool? hideSort = null)
        {
            var ret = BuildAzureSearch(filter, out var results);
            if (ret != null) return ret;

            return VueAzureSearch(results, filter, hideSort);
        }

        private ActionResult VueAzureSearch(
            SearchResults results, SongFilter filter, bool? hideSort = null)
        {
            return VueAzureSearch(results.Songs.ToList(), (int) results.TotalCount,
                filter, hideSort);
        }

        private ActionResult VueAzureSearch(IReadOnlyCollection<Song> songs,
            int? totalSongs, SongFilter filter, bool? hideSort = null)
        {
            var user = User.Identity.Name;
            if (user != null) filter.Anonymize(user);

            var sparse = songs.Select(s => _mapper.Map<SongSparse>(s)).ToList();
            var histories = songs.Select(s => s.GetHistory(_mapper)).ToList();
            var action = filter.Action;
            return View(
                action.Equals("Advanced", StringComparison.OrdinalIgnoreCase)
                || action.StartsWith("azure+raw", StringComparison.OrdinalIgnoreCase)
                || action.Equals("MergeCandidates")
                    ? "index"
                    : filter.Action,
                new SongListModel
                {
                    Songs = sparse,
                    Histories = histories,
                    Filter = _mapper.Map<SongFilterSparse>(filter),
                    UserName = user,
                    Count = totalSongs ?? songs.Count,
                    Validate = false
                });
        }

        //
        // GET: /Song/RawSearchForm
        [AllowAnonymous]
        public ActionResult RawSearchForm([FromServices] IDanceStatsManager danceStatsManager,
            SongFilter filter = null)
        {
            HelpPage = "advanced-search";

            ViewBag.AzureIndexInfo = Song.GetIndex(Database, danceStatsManager);
            UseVue = false;
            return View(new RawSearch(filter is {IsRaw: true} ? filter : null));
        }

        //
        // GET: /Song/RawSearch
        [AllowAnonymous]
        public ActionResult RawSearch([FromServices] IDanceStatsManager danceStatsManager,
            [Bind(
                "SearchText,ODataFilter,SortFields,SearchFields,Description,IsLucene,CruftFilter")]
            RawSearch rawSearch)
        {
            HelpPage = "advanced-search";

            ViewBag.AzureIndexInfo = Song.GetIndex(Database, danceStatsManager);
            return ModelState.IsValid
                ? DoAzureSearch(new SongFilter(rawSearch))
                : View("RawSearchForm", rawSearch);
        }

        //
        // GET: /Song/AdvancedSearchForm
        [AllowAnonymous]
        public ActionResult AdvancedSearchForm(SongFilter filter)
        {
            HelpPage = "advanced-search";

            var dances = new List<DanceObject>(Dances.Instance.AllDanceTypes);
            dances.AddRange(Dances.Instance.AllDanceGroups);

            var model = new SearchModel
            {
                Filter = _mapper.Map<SongFilterSparse>(filter),
                Dances = dances
            };

            return View("AdvancedSearchForm",
                JsonConvert.SerializeObject(model, CamelCaseSerializerSettings));
        }

        [AllowAnonymous]
        public ActionResult FilterSearch(SongFilter filter)
        {
            return DoAzureSearch(filter);
        }

        // Get: /Song/AdvancedSearch
        [AllowAnonymous]
        public ActionResult AdvancedSearch(string searchString = null, string dances = null,
            string tags = null, ICollection<string> services = null, decimal? tempoMin = null,
            decimal? tempoMax = null, string user = null, string sortOrder = null,
            string sortDirection = null, ICollection<int> bonusContent = null,
            SongFilter filter = null)
        {
            if (!filter.IsAdvanced) filter.Action = filter.IsAzure ? "azure+advanced" : "Advanced";

            if (string.IsNullOrWhiteSpace(searchString)) searchString = null;
            if (!string.Equals(searchString, filter.SearchString))
            {
                filter.SearchString = searchString;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(dances)) dances = null;
            if (!string.Equals(dances, filter.Dances, StringComparison.OrdinalIgnoreCase))
            {
                filter.Dances = dances;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(tags)) tags = null;

            if (!string.Equals(tags, filter.Tags, StringComparison.OrdinalIgnoreCase))
            {
                filter.Tags = tags;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(user)) user = null;

            if (!string.Equals(user, filter.User, StringComparison.OrdinalIgnoreCase))
            {
                filter.User = user;
                filter.Page = 1;
            }

            if (string.IsNullOrWhiteSpace(sortOrder) || string.Equals(sortOrder, "Closest Match",
                StringComparison.OrdinalIgnoreCase))
                sortOrder = null;
            else if (string.Equals(sortDirection, "Descending", StringComparison.OrdinalIgnoreCase))
                sortOrder = sortOrder + "_desc";

            if (!string.Equals(sortOrder, filter.SortOrder, StringComparison.OrdinalIgnoreCase))
            {
                filter.SortOrder = sortOrder;
                filter.Page = 1;
            }

            var purchase = string.Empty;
            if (services != null) purchase = string.Concat(services);

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
                foreach (var x in bonusContent) level |= x;
            }

            if (filter.Level != level)
            {
                filter.Level = level;
                filter.Page = 1;
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
            if (services != null) purchase = string.Concat(services);

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
        public ActionResult Index(SongFilter filter, string id = null, int? page = null,
            string purchase = null)
        {
            if (id != null && Database.DanceStats.Map.ContainsKey(id.ToUpper()))
                filter.Dances = id.ToUpper();

            if (page.HasValue) filter.Page = page;

            if (User.Identity.IsAuthenticated && filter.IsEmpty)
                filter.User = new UserQuery(User.Identity.Name, false, false).Query;

            if (!string.IsNullOrWhiteSpace(purchase)) filter.Purchase = purchase;

            return DoAzureSearch(filter);
        }

        //
        // GET: /AdvancedIndex/
        // TODO: Figure out if this ever gets called
        [AllowAnonymous]
        public ActionResult Advanced(int? page, string purchase, SongFilter filter)
        {
            return Index(filter, null, page, purchase);
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
        public ActionResult Details(SongFilter filter, Guid? id = null)
        {
            var spider = CheckSpiders();
            if (spider != null) return spider;

            var gid = id ?? Guid.Empty;
            var song = id.HasValue ? Database.FindSong(id.Value, User.Identity.Name) : null;
            if (song == null)
            {
                song = Database.FindMergedSong(gid, User.Identity.Name);
                return song != null
                    ? RedirectToActionPermanent("details",
                        new {id = song.SongId.ToString(), filter})
                    : ReturnError(HttpStatusCode.NotFound,
                        $"The song with id = {gid} has been deleted.");
            }

            HelpPage = "song-details";
            return View(GetSongDetails(song, filter));
        }

        private SongDetailsModel GetSongDetails(Song song, SongFilter filter)
        {
            return new()
            {
                SongHistory = song.GetHistory(_mapper),
                Filter = _mapper.Map<SongFilterSparse>(filter),
                UserName = User.Identity.Name,
                Song = _mapper.Map<SongSparse>(song)
            };
        }

        [AllowAnonymous]
        public ActionResult Album(string title)
        {
            var spider = CheckSpiders();
            if (spider != null) return spider;

            AlbumViewModel model;

            if (!string.IsNullOrWhiteSpace(title))
                model = AlbumViewModel.Create(
                    title, User.Identity.Name, _mapper, DefaultCruftFilter(), Database);
            else
                return ReturnError(HttpStatusCode.NotFound, @"Empty album title not valid.");

            return View("Album", model);
        }

        [AllowAnonymous]
        public ActionResult Artist(string name)
        {
            var spider = CheckSpiders();
            if (spider != null) return spider;

            if (!string.IsNullOrWhiteSpace(name))
            {
                var model = ArtistViewModel.Create(
                    name, User.Identity.Name, _mapper, DefaultCruftFilter(), Database);
                return View("Artist", model);
            }

            return ReturnError(HttpStatusCode.NotFound, $"Album '{name}' not found.");
        }

        //
        // GET: /Song/Add
        [AllowAnonymous]
        public ActionResult Augment(SongFilter filter)
        {
            HelpPage = "add-songs";
            return View();
        }

        //
        // GET: /Song/Delete/5
        [Authorize(Roles = "dbAdmin")]
        public ActionResult Delete(Guid id)
        {
            var song = Database.FindSong(id);
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
        public ActionResult DeleteConfirmed(Guid id, SongFilter filter)
        {
            var song = Database.FindSong(id);
            var userName = User.Identity.Name;
            var user = Database.FindUser(userName);
            Database.DeleteSong(user, song);

            return RedirectToAction("Index", new {filter});
        }

        //
        // POST: /Song/BatchCorrectTempo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchCorrectTempo(SongFilter filter, decimal multiplier = 0.5M,
            string user = null, int max = 1000)
        {
            var applicationUser = user == null
                ? new ApplicationUser("tempo-bot", true)
                : Database.FindOrAddUser(user);

            return BatchAdminExecute(filter,
                (dms, song) =>
                    dms.CorrectTempoSong(song, applicationUser, multiplier),
                "BatchCorrectTempo", max);
        }

        //
        // POST: /Song/BatchAdminEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchAdminEdit(SongFilter filter, string properties, string user = null,
            int max = 1000)
        {
            Debug.Assert(User.Identity != null, "User.Identity != null");
            var applicationUser = Database.FindUser(user ?? User.Identity.Name);
            return BatchAdminExecute(filter, (dms, song) =>
                dms.AdminAppendSong(song, applicationUser, properties), "BatchAdminEdit", max);
        }

        //
        // POST: /Song/AdminModify/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchAdminModify(SongFilter filter, string properties,
            string user = null, int max = 1000)
        {
            return BatchAdminExecute(
                filter,
                (dms, song) => dms.AdminModifySong(song, properties),
                "BatchAdminModify", max);
        }

        private ActionResult BatchAdminExecute(SongFilter filter,
            Func<DanceMusicCoreService, Song, bool> act, string name, int max)
        {
            if (!ModelState.IsValid || filter.IsEmpty)
                return RedirectToAction("Index", new {filter});

            try
            {
                StartAdminTask(name);
                AdminMonitor.UpdateTask(name);
                var tried = 0;

                var dms = Database.GetTransientService();
                Task.Run(() =>
                {
                    try
                    {
                        var songs = dms.TakeTail(filter, max, null,
                            DanceMusicCoreService.CruftFilter.AllCruft);

                        var processed = 0;

                        var succeeded = new List<Song>();
                        var failed = new List<Song>();
                        foreach (var song in songs)
                        {
                            AdminMonitor.UpdateTask($"Processing ({succeeded.Count})", processed);

                            tried += 1;
                            processed += 1;

                            if (act(dms, song))
                                succeeded.Add(song);
                            else
                                failed.Add(song);

                            if ((tried + 1) % 100 != 0) continue;

                            Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                                $"{tried} songs tried.");
                        }

                        dms.UpdateAzureIndex(succeeded.Concat(failed));


                        AdminMonitor.CompleteTask(true,
                            $"{name}: Completed={true}, Succeeded={succeeded.Count} - ({string.Join(",", succeeded.Select(s => s.SongId))}), Failed={failed.Count} - ({string.Join(",", failed.Select(s => s.SongId))})");
                    }
                    catch (Exception e)
                    {
                        AdminMonitor.CompleteTask(false, $"BatchAdminExecute: Failed={e.Message}");
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

        public ActionResult CleanupAlbums(Guid id, SongFilter filter)
        {
            var user = Database.FindUser(User.Identity.Name);

            var song = Database.FindSong(id);
            if (Database.CleanupAlbums(user, song) != 0) SaveSong(song);

            return RedirectToAction("Details", new {id, filter});
        }


        //
        // POST: /Song/Delete/5

        [HttpPost]
        [ActionName("UndoUserChanges")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult UndoUserChanges(SongFilter filter, Guid id, string userName = null)
        {
            if (userName == null)
                userName = User.Identity.Name;
            else if (!User.IsInRole("showDiagnostics"))
                return new StatusCodeResult((int) HttpStatusCode.Forbidden);

            var user = Database.FindUser(userName);
            Database.UndoUserChanges(user, id);
            return RedirectToAction("Details", new {id, filter});
        }


        [HttpGet]
        public async Task<ActionResult> CreateSpotify(SongFilter filter)
        {
            HelpPage = "spotify-playlist";
            UseVue = false;

            var authResult = await HttpContext.AuthenticateAsync();
            var canSpotify = AdmAuthentication.GetServiceAuthorization(
                Configuration, ServiceType.Spotify, User, authResult) != null;

            return View(new PlaylistCreateInfo
            {
                Title = string.IsNullOrWhiteSpace(filter.ShortDescription)
                    ? "music4dance playlist"
                    : filter.ShortDescription,
                DescriptionPrefix =
                    "This playlist was created with information from music4dance.net: ",
                Description = filter.Description,
                Count = 25,
                Filter = filter.ToString(),
                IsAuthenticated = User.Identity.IsAuthenticated,
                IsPremium = User.IsInRole("premium") || User.IsInRole("trial"),
                CanSpotify = canSpotify
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateSpotify([FromServices] IFileProvider fileProvider,
            [Bind("Title,DescriptionPrefix,Description,Count,Filter")]
            PlaylistCreateInfo info)
        {
            if (!ModelState.IsValid) return View(info);

            if (info.Count > 100)
            {
                ViewBag.StatusMessage = "Please stop trying to hack the site.";
                return View("Error");
            }

            if (!string.Equals(
                User.Claims.FirstOrDefault(c =>
                        c.Type ==
                        "http://schemas.microsoft.com/ws/2008/06/identity/claims/authenticationmethod")
                    ?.Value, "Spotify", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Title = "Connect your account to Spotify";
                ViewBag.Message =
                    "You must have a Spotify account associated with your music4dance account in order to use this feature. More instruction on adding an external account are available <a href='https://music4dance.blog/music4dance-help/account-management/#add-external-account'>here</a>.";
                return View("Info");
            }

            var authResult = await HttpContext.AuthenticateAsync();
            var canSpotify = AdmAuthentication.GetServiceAuthorization(
                Configuration, ServiceType.Spotify, User, authResult) != null;

            PlaylistMetadata metadata;
            var filter = new SongFilter(info.Filter);

            HelpPage = "spotify-playlist";

            try
            {
                filter.Purchase = "S";
                var p = Database.AzureParmsFromFilter(filter, info.Count);
                p.IncludeTotalResultCount = true;
                var results = Database.AzureSearch(filter.SearchString, p, filter.CruftFilter);
                var tracks = results.Songs.Select(s => s.GetPurchaseId(ServiceType.Spotify));

                var service = MusicService.GetService(ServiceType.Spotify);
                metadata = MusicServiceManager.CreatePlaylist(service, User, info.Title,
                    $"{info.DescriptionPrefix} {filter.Description}", fileProvider);

                if (!MusicServiceManager.SetPlaylistTracks(service, User, metadata.Id, tracks))
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
            return View("SpotifyCreated", metadata);
        }

        //
        // Merge: /Song/MergeCandidates
        [Authorize(Roles = "dbAdmin")]
        public ActionResult MergeCandidates(int? page, int? level, bool? autoCommit,
            SongFilter filter)
        {
            filter.Action = "MergeCandidates";

            if (page.HasValue) filter.Page = page;

            if (level.HasValue) filter.Level = level;

            var songs =
                Database.FindMergeCandidates(autoCommit == true ? 10000 : 500, filter.Level ?? 1);

            if (autoCommit.HasValue && autoCommit.Value)
                songs = AutoMerge(songs, filter.Level ?? 1);

            return VueAzureSearch(songs, null, filter);
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
        public ActionResult UpdateRatings(Guid id, SongFilter filter)
        {
            var song = Database.FindSong(id);
            if (song == null)
                return ReturnError(HttpStatusCode.NotFound,
                    $"The song with id = {id} has been deleted.");
            song.SetRatingsFromProperties();
            SaveSong(song);

            HelpPage = "song-details";

            return View("Details", song);
        }


        //
        // BulkEdit: /Song/BulkEdit
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult BulkEdit(Guid[] selectedSongs, string action, SongFilter filter)
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
                    var list = songs.ToList().AsReadOnly();
                    return VueAzureSearch(list, list.Count, new SongFilter());
            }
        }

        //
        // Merge: /Song/Merge
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "dbAdmin")]
        public ActionResult MergeResults(string songIds, SongFilter filter)
        {
            var songs = Database.FindSongs(songIds.Split(',').Select(Guid.Parse)).ToList();

            // Create a merged version of the song (and commit to DB)

            // Get the logged in user
            var user = Database.FindUser(User.Identity.Name);

            var song = Database.MergeSongs(user, songs,
                ResolveStringField(Song.TitleField, songs, Request.Form),
                ResolveStringField(Song.ArtistField, songs, Request.Form),
                ResolveDecimalField(Song.TempoField, songs, Request.Form),
                ResolveIntField(Song.LengthField, songs, Request.Form),
                Request.Form[Song.AlbumListField], new HashSet<string>(Request.Form.Keys));

            Database.RemoveMergeCandidates(songs);

            DanceStatsManager.ClearCache(Database, true);

            ViewBag.BackAction = "MergeCandidates";

            return View("details", GetSongDetails(song, filter));
        }

        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchClearUpdate(SongFilter filter, string type = "U", int count = 100)
        {
            // Type = (U)pdate
            //        (L)ookupStatus
            if (count == -1) count = int.MaxValue;
            try
            {
                StartAdminTask("BatchClearUpdate");
                AdminMonitor.UpdateTask("BatchClearUpdate");

                filter.Page = 1;

                var dms = Database.GetTransientService();
                Task.Run(() =>
                {
                    try
                    {
                        var c = 0;
                        while (c < count)
                        {
                            var prms = dms.AzureParmsFromFilter(filter, 500);
                            prms.IncludeTotalResultCount = false;
                            prms.Filter = prms.Filter == null ? "(" : prms.Filter + " and (";

                            if (type.Contains('U')) prms.Filter += "Purchase/any(t: t eq '---')";
                            if (type.Contains('L'))
                            {
                                if (type.Contains('U')) prms.Filter += " or ";
                                prms.Filter += "LookupStatus eq true";
                            }

                            prms.Filter += ")";

                            AdminMonitor.UpdateTask("BuildSongList",
                                ((filter.Page ?? 1) - 1) * 500);
                            var res = dms.AzureSearch(filter.SearchString, prms,
                                DanceMusicCoreService.CruftFilter.AllCruft);
                            if (!res.Songs.Any()) break;
                            dms.SaveSongsImmediate(res.Songs);

                            filter.Page = filter.Page + 1;
                            c += res.Songs.Count();
                        }

                        AdminMonitor.CompleteTask(true, $"BatchClearUpdate: Completed ({c})");
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
            catch (Exception e)
            {
                return FailAdminTask($"BatchClearUpdate: {e.Message}", e);
            }
        }


        // CleanMusicServices: /Song/CleanMusicServices
        [Authorize(Roles = "dbAdmin")]
        public async Task<ActionResult> CleanMusicServices(SongFilter filter, Guid id,
            string type = "S")
        {
            var song = Database.FindSong(id);
            if (song == null)
                return ReturnError(HttpStatusCode.NotFound,
                    $"The song with id = {id} has been deleted.");

            var newSong = await CleanMusicServiceSong(song, Database, type);
            if (newSong != null) Database.SaveSong(newSong);

            HelpPage = "song-details";
            return View("Details", newSong ?? song);
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
        public ActionResult BatchCleanupProperties(SongFilter filter, string type = "TC")
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
                filter, (dms, song) =>
                    Task.FromResult(
                        dms.ReloadSong(song)
                            ? song
                            : null));
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
                Task.Run(async () => // Intentionally drop this async on the floor
                {
                    try
                    {
                        while (!done)
                        {
                            AdminMonitor.UpdateTask("BuildSongList",
                                ((filter.Page ?? 1) - 1) * 500);

                            var prms = dms.AzureParmsFromFilter(filter, 500);
                            prms.IncludeTotalResultCount = false;
                            var res = await dms.AzureSearchAsync(filter.SearchString, prms,
                                DanceMusicCoreService.CruftFilter.AllCruft);
                            if (!res.Songs.Any()) break;
                            var save = new List<Song>();

                            var processed = 0;
                            foreach (var song in res.Songs)
                                try
                                {
                                    AdminMonitor.UpdateTask("Processing",
                                        ((filter.Page ?? 1) - 1) * 500 + processed);

                                    processed += 1;
                                    tried += 1;
                                    var songT = await act(dms, song);
                                    if (songT != null) changed.Add(songT.SongId);

                                    if (songT != null) save.Add(songT);

                                    if (count > 0 && tried > count)
                                        break;

                                    if ((tried + 1) % 25 != 0) continue;

                                    Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                                        $"{tried} songs tried.");
                                }
                                catch (Exception e)
                                {
                                    Trace.WriteLine(
                                        $"{song.Title} by {song.Artist} failed with: {e.Message}");
                                }

                            if (save.Count > 0) dms.SaveSongsImmediate(save);

                            filter.Page += 1;
                            if (processed < 500) done = true;
                        }

                        AdminMonitor.CompleteTask(true,
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
        public ActionResult BatchSamples(SongFilter filter, string options = null, int count = 1,
            int pageSize = 1000)
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

                filter.Purchase = "IS";

                var page = 0;
                var done = false;

                var user = Database.FindUser("batch-s");
                Debug.Assert(user != null);

                var parameters = Database.AzureParmsFromFilter(filter);
                parameters.Filter = (parameters.Filter == null ? "" : parameters.Filter + " and ") +
                    "(Sample eq null or Sample eq '.')";

                var sucIds = new List<Guid>();
                var failIds = new List<Guid>();

                var dms = Database.GetTransientService();
                Task.Run(() =>
                {
                    try
                    {
                        while (!done)
                        {
                            AdminMonitor.UpdateTask("BuildPage", page);
                            var songs = dms.TakeTail(parameters, pageSize);

                            var failed = new List<Song>();
                            var succeeded = new List<Song>();

                            var processed = 0;
                            foreach (var song in songs)
                            {
                                AdminMonitor.UpdateTask($"Processing ({succeeded.Count})",
                                    processed);

                                processed += 1;

                                tried += 1;
                                if (MusicServiceManager.GetSampleData(dms, song))
                                    succeeded.Add(song);
                                else
                                    failed.Add(song);

                                if (tried > count)
                                    break;

                                if ((tried + 1) % 100 != 0) continue;

                                Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                                    $"{tried} songs tried.");
                            }

                            sucIds.AddRange(succeeded.Select(s => s.SongId));
                            failIds.AddRange(failed.Select(s => s.SongId));

                            dms.UpdateAzureIndex(succeeded.Concat(failed));

                            page += 1;
                            if (processed < pageSize) done = true;
                        }

                        AdminMonitor.CompleteTask(true,
                            $"BatchSample: Completed={tried <= count}, Succeeded={sucIds.Count} - ({string.Join(",", sucIds)}), Failed={failIds.Count} - ({string.Join(",", failIds)}), Skipped={0}");
                    }
                    catch (Exception e)
                    {
                        AdminMonitor.CompleteTask(false, $"BatchSamples: Failed={e.Message}");
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
                return FailAdminTask($"BatchSample: {e.Message}", e);
            }
        }

        [Authorize(Roles = "dbAdmin")]
        public ActionResult BatchEchoNest(SongFilter filter, string options = null, int count = 1,
            int pageSize = 1000)
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

                var user = Database.FindUser(User.Identity.Name);

                filter.Purchase = "S";

                var parameters = DanceMusicCoreService.AddCruftInfo(
                    Database.AzureParmsFromFilter(filter),
                    DanceMusicCoreService.CruftFilter.NoCruft);

                // SkipTempo
                if (options != null && options.Contains("T"))
                    // TODO: Consider if we really want to do this based on performance issues against null checking
                    // https://social.msdn.microsoft.com/Forums/azure/en-US/977f5a45-6013-45b4-ae9d-285ada22d071/performance-of-queries-and-filters-with-null-values?forum=azuresearch
                    parameters.Filter =
                        (parameters.Filter == null ? "" : parameters.Filter + " and ") +
                        "(Tempo eq null)";

                // Retry???
                if (options == null || options.Contains("R"))
                    parameters.Filter =
                        (parameters.Filter == null ? "" : parameters.Filter + " and ") +
                        "(Beat eq null)";

                var dms = Database.GetTransientService();
                Task.Run(() =>
                {
                    try
                    {
                        while (!done)
                        {
                            AdminMonitor.UpdateTask("BuildPage", page);

                            var songs = dms.TakeTail(parameters, pageSize);
                            var processed = 0;

                            var stemp = new List<Song>();
                            var ftemp = new List<Song>();
                            foreach (var song in songs)
                            {
                                AdminMonitor.UpdateTask($"Processing ({succeeded.Count})",
                                    processed);

                                tried += 1;
                                processed += 1;

                                if (song.Purchase == null)
                                {
                                    Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                                        $"Bad Purchase: {song}");
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

                                if (MusicServiceManager.GetEchoData(dms, song))
                                    stemp.Add(song);
                                else
                                    ftemp.Add(song);

                                if (tried > count)
                                    break;

                                if ((tried + 1) % 100 != 0) continue;

                                Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                                    $"{tried} songs tried.");
                            }

                            dms.UpdateAzureIndex(stemp.Concat(ftemp));
                            succeeded.AddRange(stemp.Select(s => s.SongId));
                            failed.AddRange(ftemp.Select(s => s.SongId));

                            page += 1;
                            if (processed < pageSize) done = true;
                        }

                        AdminMonitor.CompleteTask(true,
                            $"BatchEchonest: Completed={tried <= count}, Succeeded={succeeded.Count} - ({string.Join(",", succeeded)}), Failed={failed.Count} - ({string.Join(",", failed)}), Skipped={skipped}");
                    }
                    catch (Exception e)
                    {
                        AdminMonitor.CompleteTask(false, $"BatchEchoNest: Failed={e.Message}");
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
                return FailAdminTask($"BatchEchoNest: {e.Message}", e);
            }
        }

        #endregion

        #region General Utilities

        private ActionResult LoginRedirect(SongFilter filter)
        {
            return Redirect(
                $"/Identity/Account/Login/?ReturnUrl=/song/advancedsearchform?filter={filter}");
        }

        private IEnumerable<SelectListItem> GetDancesSingle(bool includeEmpty = false)
        {
            var counts = DanceStatsManager.FlatDanceStats;

            var dances = new List<SelectListItem>(counts.Count)
            {
                new() {Value = string.Empty, Text = string.Empty, Selected = true}
            };

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var cnt in counts.Where(c => includeEmpty || c.SongCount > 0))
                dances.Add(new SelectListItem
                    {Value = cnt.DanceId, Text = cnt.DanceName, Selected = false});
            return dances;
        }

        private ActionResult Delete(IEnumerable<Song> songs, SongFilter filter)
        {
            var user = Database.FindUser(User.Identity.Name);

            foreach (var song in songs) Database.DeleteSong(user, song);

            return RedirectToAction("Index", new {filter});
        }

        #endregion

        #region MusicService

        private async Task<Song> CleanMusicServiceSong(Song song, DanceMusicCoreService dms,
            string type = "S", string region = "US")
        {
            var props = new List<SongProperty>(song.SongProperties);

            var changed = false;

            if (type.IndexOf('X') != -1) changed |= CleanDeletedServices(song.SongId, props);
            if (type.IndexOf('B') != -1) changed |= await CleanBrokenServices(song, props);
            if (type.IndexOf('S') != -1) changed |= CleanSpotify(props);
            if (type.IndexOf('A') != -1) changed |= CleanOrphanedAlbums(props);
            if (type.IndexOf('D') != -1) changed |= CleanDeprecatedProperties(song.SongId, props);

            var updateGenre = type.IndexOf('G') != -1;

            Song newSong = null;
            if (changed || updateGenre) newSong = new Song(song.SongId, props, dms);

            if (!updateGenre) return newSong;

            return UpdateSpotifyGenre(newSong, dms) || changed ? newSong : null;
        }

        private bool UpdateSpotifyGenre(Song song, DanceMusicCoreService dms)
        {
            var spotify = MusicService.GetService(ServiceType.Spotify);
            var tags = song.GetUserTags(spotify.User);

            foreach (var prop in SpotifySongProperties(song.SongProperties))
            {
                var id = PurchaseRegion.ParseIdAndRegionInfo(prop.Value, out _);
                var track = MusicServiceManager.GetMusicServiceTrack(id, spotify);
                if (track.Genres is {Length: > 0})
                    tags = tags.Add(
                        new TagList(
                            dms.NormalizeTags(
                                string.Join("|", track.Genres),
                                "Music", true)));
            }

            Trace.Write($"Tags={tags}\r\n");

            return song.EditSongTags(spotify.ApplicationUser, tags, dms.DanceStats);
        }

        private bool CleanSpotify(IEnumerable<SongProperty> props)
        {
            var changed = 0;
            var skipped = 0;

            foreach (var prop in SpotifySongProperties(props))
            {
                var id = PurchaseRegion.ParseIdAndRegionInfo(prop.Value, out var regions);

                if (null != regions || prop.Value.EndsWith("[]"))
                {
                    prop.Value = id;
                    changed += 1;
                }
                else
                {
                    skipped += 1;
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                $"Spotify: Changed = {changed}, Skipped = {skipped}");

            return changed > 0;
        }

        private IEnumerable<SongProperty> SpotifySongProperties(IEnumerable<SongProperty> props)
        {
            foreach (var prop in props.Where(p =>
                p.Name.StartsWith("Purchase") && p.Name.EndsWith(":SS"))) yield return prop;
        }

        private async Task<bool> CleanBrokenServices(Song song, ICollection<SongProperty> props)
        {
            var del = new List<SongProperty>();

            foreach (var link in song.GetPurchaseLinks())
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
                            if (t != null) del.Add(t);
                        }

                        if (!string.IsNullOrWhiteSpace(link.AlbumId))
                        {
                            var t = props.FirstOrDefault(
                                p => p.Name.StartsWith("Purchase") && p.Name.EndsWith("A") &&
                                    p.Value.StartsWith(link.AlbumId));
                            if (t != null) del.Add(t);
                        }

                        Trace.WriteLine($"Removing: {link.Link}");
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Link {link.Link} threw {e.Message}");
                }

            if (del.Count == 0) return false;

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Removed: {del.Count}");

            foreach (var prop in del) props.Remove(prop);

            return true;
        }

        [Authorize(Roles = "dbAdmin")]
        public async Task<IActionResult> DownloadJson(SongFilter filter, string type = "S",
            int count = 1)
        {
            var p = Database.AzureParmsFromFilter(filter, 1000);
            p.IncludeTotalResultCount = true;

            var results = await Database.AzureSearchAsync(filter.SearchString, p,
                filter.CruftFilter,
                User.Identity.Name);

            switch (type)
            {
                case "S":
                    return JsonCamelCase(
                        results.Songs.Select(s => _mapper.Map<SongSparse>(s)).ToList());
                case "H":
                    return JsonCamelCase(
                        results.Songs.Select(s => s.GetHistory(_mapper)).ToList());
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
            var companions = new List<string> {Song.EditCommand, Song.UserField, Song.TimeField};

            var del = props
                .Where(prop => prop.Name.StartsWith("Purchase:") && prop.Name.EndsWith(":XS"))
                .ToList();


            if (del.Count == 0) return false;

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Removed: {del.Count}");

            foreach (var prop in del) props.Remove(prop);

            return true;
        }

        private bool CleanDeprecatedProperties(Guid songId, ICollection<SongProperty> props)
        {
            var del = new List<SongProperty>();
            foreach (var prop in props)
                if (prop.Name.StartsWith("PromoteAlbum:") || prop.Name.StartsWith("OrderAlbums:"))
                    del.Add(prop);

            if (del.Count == 0) return false;

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Removed: {del.Count}");

            foreach (var prop in del) props.Remove(prop);

            return true;
        }

        private static bool CleanOrphanedAlbums(ICollection<SongProperty> props)
        {
            var del = new List<SongProperty>();
            // Check every purchase link and make sure it's still valid
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var prop in props.Where(p =>
                p.Name.StartsWith("Purchase") && p.Name.EndsWith("A")))
            {
                var s = prop.Name.Substring(0, prop.Name.Length - 1) + 'S';
                if (props.All(p => p.Name != s)) del.Add(prop);
            }

            if (del.Count == 0) return false;

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Removed: {del.Count}");

            foreach (var prop in del) props.Remove(prop);

            return true;
        }

        #endregion

        #region Merge

        private IReadOnlyCollection<Song> AutoMerge(IReadOnlyCollection<Song> songs, int level)
        {
            // Get the logged in user
            var userName = User.Identity.Name;
            var user = Database.FindUser(userName);

            var ret = new List<Song>();
            List<Song> cluster = null;

            try
            {
                foreach (var song in new List<Song>(songs))
                    if (cluster == null)
                    {
                        cluster = new List<Song> {song};
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
                            var s = AutoMerge(cluster, user);
                            ret.Add(s);
                        }
                        else if (cluster.Count == 1)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                                $"Bad Merge: {cluster[0].Title}");
                        }

                        cluster = new List<Song> {song};
                    }
            }
            finally
            {
                DanceStatsManager.ClearCache(Database, false);
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
            var sm = new SongMerge(songs.ToList(), Database.DanceStats);

            UseVue = false;
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
                if (!string.IsNullOrWhiteSpace(s)) int.TryParse(s, out idx);
            }
            else
            {
                for (var i = 0; i < songs.Count; i++)
                {
                    var s = songs[i];

                    if (s.GetType().GetProperty(fieldName)?.GetValue(s) == null) continue;

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