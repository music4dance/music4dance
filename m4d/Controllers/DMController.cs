using System.Net;
using System.Security.Principal;

using m4d.Services;
using m4d.Utilities;
using m4d.ViewModels;

using m4dModels;
using m4dModels.Utilities;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;
using Microsoft.Net.Http.Headers;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace m4d.Controllers;

public class DanceMusicController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backgroundTaskQueue,
    IFeatureManager featureManager, ILogger logger) : Controller
{
    protected static readonly JsonSerializerSettings CamelCaseSerializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
        ContractResolver = new StatsContractResolver(true, true)
    };

    protected static readonly JsonSerializer CamelCaseSerializer =
        JsonSerializer.Create(CamelCaseSerializerSettings);

    private MusicServiceManager _musicServiceManager;

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var usageId = GetUsageId();
        var time = DateTime.Now;
        var userAgent = Request.Headers[HeaderNames.UserAgent];
        var userExpiration = await UserExpiration.Create(UserName, UserManager);
        ViewData["UserExpiration"] = userExpiration;

        await next();

        if (SpiderManager.CheckAnySpiders(userAgent, Configuration) ||
            await FeatureManager.IsEnabledAsync(FeatureFlags.UsageLogging))
        {
            return;
        }

        var filter = GetFilterFromContext(context);
        if (filter.IsDefault)
        {
            filter = null;
        }
        var filterString = filter?.ToString();
        var page = Request.Path;
        var query = Request.QueryString.ToString();
        var user = userExpiration.User;

        var referrer = Request.GetTypedHeaders().Referer?.ToString();

        var usage = new UsageLog
        {
            UsageId = usageId,
            UserName = user?.UserName,
            Date = time,
            Page = page,
            Query = query,
            Filter = filterString,
            Referrer = referrer,
            UserAgent = userAgent
        };

        TaskQueue.EnqueueTask(
            async (serviceScopeFactory, cancellationToken) =>
            {
                try
                {
                    using var scope = serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();

                    await context.UsageLog.AddAsync(usage, cancellationToken);
                    if (user != null)
                    {
                        // Need to get the user from the context to update it
                        user = await context.Users.FindAsync([user.Id], cancellationToken: cancellationToken);
                        user.LastActive = time;
                        user.HitCount += 1;
                    }
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Unable to log usage {ex.Message}");
                }
            });
    }

    private string GetUsageId()
    {
        Request.Cookies.TryGetValue("Usage", out var usageString);
        string usageId;
        int usageCount = 1;
        if (string.IsNullOrEmpty(usageString))
        {
            usageId = Guid.NewGuid().ToString("D");
        }
        else
        {
            var fields = usageString.Split('_');
            usageId = fields[0];
            if (fields.Length > 1)
            {
                _ = int.TryParse(fields[1], out usageCount);
            }
            usageCount += 1;
        }
        Response.Cookies.Append("Usage", $"{usageId}_{usageCount}");
        return usageId;
    }

    protected SongFilter GetFilterFromContext(ActionExecutingContext context)
    {
        var user = UserName;
        var request = context.HttpContext.Request;
        var filterString = request.Query["filter"];
        SongFilter filter = null;

        if (string.IsNullOrWhiteSpace(filterString) && request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            filterString = context.HttpContext.Request.Form["filter"];
        }
        if (!string.IsNullOrEmpty(filterString))
        {
            filter = new SongFilter(filterString);
        }
        else
        {
            filter = SongFilter.GetDefault(user);
            context.ActionArguments["filter"] = filter;
        }

        return filter;
    }

    protected UseVue UseVue { get; set; } = UseVue.No;
    public DanceMusicService Database { get; set; } =
            new DanceMusicService(context, userManager, searchService, danceStatsManager);

    protected MusicServiceManager MusicServiceManager =>
        _musicServiceManager ??= new MusicServiceManager(Configuration);

    protected IConfiguration Configuration { get; } = configuration;

    protected ISearchServiceManager SearchService { get; } = searchService;

    protected IDanceStatsManager DanceStatsManager { get; } = danceStatsManager;

    protected ILogger Logger { get; } = logger;

    protected IFileProvider FileProvider { get; } = fileProvider;
    protected IFeatureManager FeatureManager { get; } = featureManager;
    protected IBackgroundTaskQueue TaskQueue { get; } = backgroundTaskQueue;

    protected UserManager<ApplicationUser> UserManager => Database.UserManager;

    protected DanceMusicContext Context => Database.Context;

    protected SongIndex SongIndex => Database.SongIndex;

    protected string HelpPage { get; set; }

    protected string UserName => Identity.Name;

    protected IIdentity Identity
    {
        get
        {
            if (User == null)
            {
                throw new Exception("Attempted access undefined User");
            }

            if (User.Identity == null)
            {
                throw new Exception("Attempted to access undefined Identity");
            }

            return User.Identity;
        }
    }

    public ActionResult ReturnError(
        HttpStatusCode statusCode = HttpStatusCode.InternalServerError, string message = null,
        Exception exception = null)
    {
        var model = new ErrorModel
        { HttpStatusCode = (int)statusCode, Message = message, Exception = exception };

        Response.StatusCode = (int)statusCode;
        // Response.TrySkipIisCustomErrors = true;

        UseVue = UseVue.No;
        return View("HttpError", model);
    }

    protected IActionResult JsonCamelCase(object json)
    {
        return new JsonResult(json, JsonHelpers.CamelCaseSerializer);
    }

    public override ViewResult View(string viewName, object model)
    {
        ViewData["Help"] = HelpPage;
        ViewData["UseVue"] = UseVue;
        return base.View(viewName, model);
    }

    public ActionResult CheckSpiders()
    {
        return SpiderManager.CheckBadSpiders(Request.Headers[HeaderNames.UserAgent], Configuration)
            ? View("BotWarning")
            : null;
    }

    protected async Task SaveSong(Song song)
    {
        await SongIndex.SaveSong(song);
    }

    protected async Task<int> CommitCatalog(DanceMusicCoreService dms, Review review,
        ApplicationUser user, string danceIds = null)
    {
        List<string> dances = null;
        if (!string.IsNullOrWhiteSpace(danceIds))
        {
            dances = [.. danceIds.Split(';')];
        }

        if (review.Merge.Count <= 0)
        {
            return 0;
        }

        var modified = dms.SongIndex.MergeCatalog(user, review.Merge, dances).ToList();

        var i = 0;

        foreach (var song in modified)
        {
            AdminMonitor.UpdateTask("UpdateService", i);
            await MusicServiceManager.UpdateSongAndServices(dms, song);
            await song.CleanupProperties(dms, "HYE");
            i += 1;
        }

        var saved = modified.Count;
        await dms.SongIndex.SaveSongs(modified);

        if (!string.IsNullOrEmpty(review.PlayList))
        {
            await dms.UpdatePlayList(review.PlayList, review.Merge.Select(m => m.Left));
        }

        return saved;
    }

    protected async Task<string> SpotifyFromFilter(SongFilter filter, string userName)
    {
        var filterString = filter.Normalize(userName).ToString();
        var map = await GetSpotifyMap(userName);
        return map.TryGetValue(filterString, out var spotify) ? spotify.Id : null;
    }

    // I'm pretty sure this will only come through as a single userName, since this is per request
    private async Task<Dictionary<string, SpotifyCreate>> GetSpotifyMap(string userName) =>
        _spotifyExports ??= await MapSpotify(userName);
    private Dictionary<string, SpotifyCreate> _spotifyExports;

    private async Task<Dictionary<string, SpotifyCreate>> MapSpotify(string userName)
    {
        var map = new Dictionary<string, SpotifyCreate>();
        foreach (var export in await GetSpotify(userName))
        {
            if (export?.Info == null)
            {
                continue;
            }

            var filter = new SongFilter(export.Info.Filter).Normalize(userName).ToString();
            if (!map.ContainsKey(filter))
            {
                map[filter] = export;
            }
        }
        return map;
    }

    private async Task<List<SpotifyCreate>> GetSpotify(string userName)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return [];
        }

        var user = await UserManager.FindByNameAsync(userName);
        if (user == null)
        {
            return [];
        }

        var userId = user.Id;

        return [.. Database.ActivityLog.Where(l => l.ApplicationUserId == userId).OrderByDescending(e => e.Date).Select(ex => JsonConvert.DeserializeObject<SpotifyCreate>(ex.Details))];
    }

    protected async Task<string> GetLoginKey(string name)
    {
        var user = await UserManager.FindByNameAsync(UserName);
        var services = await UserManager.GetLoginsAsync(user);
        return services.FirstOrDefault(s => s.LoginProvider == name)?.ProviderKey;
    }


    protected async Task<ApplicationUser> GetApplicationUser()
    {
        return User.Identity.IsAuthenticated ? await UserManager.GetUserAsync(User) : null;
    }

    protected List<string> UploadFile(IFormFile file)
    {
        var lines = new List<string>();

        ViewBag.Key = file.Name;
        // ReSharper disable once PossibleNullReferenceException
        ViewBag.Size = file.Length;
        ViewBag.ContentType = file.ContentType;

        // ReSharper disable once PossibleNullReferenceException
        using var stream = file.OpenReadStream();
        using var tr = new StreamReader(stream);

        string s;
        while ((s = tr.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                lines.Add(s);
            }
        }

        return lines;
    }

    protected string EnsureAppData(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.WebRootPath, "AppData");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return path;
    }




    #region AdminTaskHelpers

    protected void StartAdminTask(string name)
    {
        ViewBag.Name = name;
        if (!AdminMonitor.StartTask(name))
        {
            throw new AdminTaskException(
                name +
                "failed to start because there is already an admin task running");
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

        if (e is not AdminTaskException)
        {
            AdminMonitor.CompleteTask(false, message, e);
        }

        return View("Results");
    }

    protected void BuildEnvironment(bool danceEnvironment = false, bool tagDatabase = false)
    {
        if (danceEnvironment)
        {
            ViewData["DanceEnvironment"] = Database.DanceStats.GetJsonDanceEnvironment();
        }
        if (tagDatabase)
        {
            ViewData["TagDatabase"] = Database.DanceStats.GetJsonTagDatabse();
        }
    }

    protected void BuildEnvironment3(IFileProvider fileProvider, bool danceEnvironment = false, bool tagDatabase = false)
    {
        if (danceEnvironment)
        {
            if (s_danceDatabaseCache == null)
            {
                var dancesJson = ReadJsonFile(fileProvider, "dances");
                var groupsJson = ReadJsonFile(fileProvider, "danceGroups");
                var metricsJson = JArray.FromObject(Database.DanceStats.GetMetrics().Values, CamelCaseSerializer);
                var library = new JObject(
                    new JProperty("dances", dancesJson),
                    new JProperty("groups", groupsJson),
                    new JProperty("metrics", metricsJson));
                s_danceDatabaseCache = library.ToString();
            }
            ViewData["DanceDatabase"] = s_danceDatabaseCache;
        }
        if (tagDatabase)
        {
            s_tagDatabaseCache ??= Database.DanceStats.GetJsonTagDatabse();
            ViewData["TagDatabase"] = s_tagDatabaseCache;
        }
    }

    private JArray ReadJsonFile(IFileProvider fileProvider, string name)
    {
        var path = fileProvider.GetFileInfo($"/wwwroot/content/{name}.json").PhysicalPath ?? throw new Exception($"Unable to find file {name}.json");
        using var reader = System.IO.File.OpenText(path);
        return (JArray)JToken.ReadFrom(new JsonTextReader(reader));
    }

    internal static void ClearJsonCache()
    {
        s_danceDatabaseCache = null;
        s_tagDatabaseCache = null;
    }

    private static string s_danceDatabaseCache = null;
    private static string s_tagDatabaseCache = null;

    protected ActionResult Vue(string title, string description, string name,
        object model = null, string helpPage = null,
        bool danceEnvironment = false, bool tagEnvironment = false,
        string script = null)
    {
        UseVue = UseVue.V2;
        if (!string.IsNullOrEmpty(helpPage))
        {
            HelpPage = helpPage;
        }
        if (danceEnvironment || tagEnvironment)
        {
            BuildEnvironment(danceEnvironment, tagEnvironment);
        }

        if (model is string s)
        {
            model = s.Replace(@"'", @"\'");
        }

        return View(
            "Vue", new VueModel
            {
                Title = title,
                Description = description,
                Name = name,
                Script = script,
                Model = model,
            });
    }

    protected ActionResult Vue3(string title, string description, string name,
        object model = null, string helpPage = null,
        bool danceEnvironment = false, bool tagEnvironment = false,
        string script = null, bool preserveCase = false)
    {
        UseVue = UseVue.V3;
        if (!string.IsNullOrEmpty(helpPage))
        {
            HelpPage = helpPage;
        }
        if (danceEnvironment || tagEnvironment)
        {
            BuildEnvironment3(FileProvider, danceEnvironment, tagEnvironment);
        }

        if (model is string s)
        {
            model = s.Replace(@"'", @"\'");
        }

        ViewData["NoSiteCss"] = true;

        return View(
            "Vue3", new VueModel
            {
                Title = title,
                Description = description,
                Name = name,
                Script = script,
                Model = model,
                PreserveCase = preserveCase
            });
    }

    #endregion
}
