using System.Diagnostics;
using System.Globalization;
using System.Net.Mime;
using System.Text;

using CsvHelper;

using m4d.Areas.Identity;
using m4d.Services;
using m4d.Utilities;
using m4d.ViewModels;

using m4dModels;
using m4dModels.Utilities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

public class Review
{
    public string PlayList { get; set; }
    public IList<LocalMerger> Merge { get; set; }
}

public class SetupDiagnosticsAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Do something before the action executes.
        if (context.Controller is AdminController controller)
        {
            controller.SetupDiagnosticAttributes();
        }
    }
}


[Authorize]
[SetupDiagnostics]
public class AdminController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
    IFeatureManager featureManager, ILogger<ActivityLogController> logger) : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backroundTaskQueue, featureManager, logger)
{

    #region Commands

    //
    // GET: /Admin/
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult Index()
    {
        return View();
    }

    //
    // GET: /Admin/Tags
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult Tags()
    {
        return View();
    }

    //
    // GET: /Admin/Diagnostics
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult Diagnostics()
    {
        return View();
    }

    //
    // GET: /Admin/ResetAdmin
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult ResetAdmin()
    {
        AdminMonitor.CompleteTask(false, "Force Reset");
        return View("Diagnostics");
    }

    //
    // GET: /Admin/DumpCleanupCount
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult DumpCleanupCount()
    {
        Song.DumpCleanupCount();
        return View("Diagnostics");
    }

    //
    // GET: /Admin/InitializaitonTasks
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult InitializationTasks()
    {
        return View();
    }

    //
    // Get: //UpdateSitemap
    [Authorize(Roles = "dbAdmin")]
    public ActionResult UpdateSitemap([FromServices] IFileProvider fileProvider)
    {
        ViewBag.Name = "Update Sitemap";

        SiteMapInfo.ReloadCategories(fileProvider);

        return RedirectToAction("SiteMap", "Home");
    }

    //
    // Get: //UpdateWarning
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public ActionResult UpdateWarning(string message = null)
    {
        Logger.LogInformation($"Changed UpdateWarning to: {message}");
        GlobalState.UpdateMessage = message?.CleanWhitespace();
        return View("InitializationTasks");
    }

    [HttpGet]
    public ActionResult ToggleTestKeys()
    {
        GlobalState.UseTestKeys = !GlobalState.UseTestKeys;
        return View("InitializationTasks");
    }

    //
    // GET: /Admin/UploadBackup
    [Authorize(Roles = "dbAdmin")]
    public ActionResult UploadBackup()
    {
        return View();
    }

    //
    // Get: //Reseed
    //[Authorize(Roles = "dbAdmin")]
    [AllowAnonymous]
    public async Task<IActionResult> Reseed([FromServices] UserManager<ApplicationUser> userManager,
        [FromServices] RoleManager<IdentityRole> roleManager)
    {
        ViewBag.Name = "Reseed Database";
        await ReseedDb(userManager, roleManager);
        ViewBag.Success = true;
        ViewBag.Message = "Database was successfully reseeded";

        return View("Results");
    }


    //
    // Get: //ClearSongCache
    [Authorize(Roles = "showDiagnostics")]
    public async Task<ActionResult> ClearSongCache(bool reloadFromStore = true)
    {
        ViewBag.Name = "ClearSongCache";

        await DanceStatsManager.ClearCache(Database, reloadFromStore);
        DanceMusicController.ClearJsonCache();
        UsersController.ClearCache();

        ViewBag.Success = true;
        ViewBag.Message = "Cache was cleared";

        return View("Results");
    }

    //
    // Get: //ClearSongCache
    [Authorize(Roles = "showDiagnostics")]
    public async Task<ActionResult> ReloadDances()
    {
        ViewBag.Name = "ReloadDances";

        await DanceStatsManager.ReloadDances(Database);

        ViewBag.Success = true;
        ViewBag.Message = "Dances were loaded";

        return View("Results");
    }

    //
    // Get: //ToggleStructuredSchema
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult ToggleStructuredSchema()
    {
        SongFilter.StructuredSchema = !SongFilter.StructuredSchema;
        return View("Diagnostics");
    }

    //
    // Get: //SetTraceLevel
    [AllowAnonymous]
    public ActionResult ThrowException()
    {
        throw new Exception("This is an intentional exception");
    }


    //
    // Get: //SetTraceLevel
    [AllowAnonymous]
    public ActionResult SetTraceLevel(int level)
    {
        ViewBag.Name = "Set Trace Level";

        var tl = (TraceLevel)level;

        TraceLevels.SetGeneralLevel(tl);

        ViewBag.Success = true;
        ViewBag.Message = $"Trace level set: {tl}";

        return View("Results");
    }

    //
    // Get: //TestTrace
    [AllowAnonymous]
    public ActionResult TestTrace(string message, LogLevel level)
    {
        ViewBag.Name = "Test Trace";

        var logEnabled = Logger.IsEnabled(level);

        ViewBag.Success = true;
        ViewBag.Message = $"Trace message sent: '{message}'.  LogLevel is enabled = {logEnabled}";

        Logger.Log(level, $"Test Log via Logger: {level}");
        Console.WriteLine($"Test Log via Console: {level}");
        switch (level)
        {
            case LogLevel.Error:
                Trace.TraceError("Test Log via Diagnostic Trace: Error");
                break;
            case LogLevel.Warning:
                Trace.TraceWarning("Test Log via Diagnostic Trace: Warning");
                break;
            case LogLevel.Information:
                Trace.TraceInformation("Test Log via Diagnostic Trace: Info");
                break;
        }

        return View("Results");
    }

    //
    // Get: //SetSearchIdx
    public ActionResult SetSearchIdx(string id)
    {
        Logger.LogInformation($"Set Search Index: '{id}'");
        SearchService.DefaultId = id;

        DanceStatsManager.LoadFromAzure(Database, id);

        return RedirectToAction("Diagnostics");
    }

    //
    //
    // Get: //AzureFacets
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> AzureFacets(string categories, int count)
    {
        try
        {
            StartAdminTask("BuildFacets");

            var facets = await SongIndex.GetTagFacets(categories, count);

            foreach (var facet in facets)
            {
                Logger.LogInformation($"------------------{facet.Key}----------------");
                foreach (var value in facet.Value)
                {
                    Logger.LogInformation($"{value.Value}: {value.Count}");
                }
            }

            return CompleteAdminTask(true, "Finished rebuilding Dance Tags");
        }
        catch (Exception e)
        {
            return FailAdminTask("Dances Tags failed to rebuild", e);
        }
    }

    //
    // Get: //LoadIdx
    [HttpPost]
    [ValidateAntiForgeryToken]
    [DisableRequestSizeLimit]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> LoadIdx(IFormFile fileUpload, string idxName = "default",
        bool reset = true)
    {
        try
        {
            StartAdminTask("LoadIndex");
            AdminMonitor.UpdateTask("UploadFile");

            var lines = UploadFile(fileUpload);

            AdminMonitor.UpdateTask("LoadIndex");

            var idx = Database.GetSongIndex(idxName);
            if (reset)
            {
                await idx.ResetIndex();
            }

            var c = await idx.UploadIndex(lines, !reset);

            if (SearchService.GetInfo(idxName).Id == SearchService.GetInfo("default").Id)
            {
                await DanceStatsManager.LoadFromAzure(Database, idxName);
            }

            return CompleteAdminTask(true, $"Index {idxName} loaded with {c} songs");
        }
        catch (Exception e)
        {
            return FailAdminTask($"Load Index ({idxName}): {e.Message}", e);
        }
    }

    //
    // Get: //CloneIdx
    [HttpGet]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> CloneIdx(string id)
    {
        try
        {
            StartAdminTask("CloneIndex");

            AdminMonitor.UpdateTask("LoadIndex");

            await Database.CloneIndex(id);

            return CompleteAdminTask(true, $"Default index cloned to {id}");
        }
        catch (Exception e)
        {
            return FailAdminTask($"Clone Index ({id}): {e.Message}", e);
        }
    }

    //
    // Get: //ReloadDatabase
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> ReloadDatabase(
        [FromServices] UserManager<ApplicationUser> userManager,
        [FromServices] RoleManager<IdentityRole> roleManager,
        IFormFile fileUpload, string reloadDatabase, bool? batch)
    {
        try
        {
            StartAdminTask("ReloadDatabase");
            AdminMonitor.UpdateTask("UploadFile");

            var lines = UploadFile(fileUpload);

            AdminMonitor.UpdateTask("Compute Type");

            if (lines.Count > 0)
            {
                var users = TryGetSection(lines, DanceMusicService.IsUserBreak);
                var dances = TryGetSection(lines, DanceMusicService.IsDanceBreak);
                var tags = TryGetSection(lines, DanceMusicService.IsTagBreak);
                var playlists = TryGetSection(lines, DanceMusicService.IsPlaylistBreak);
                var searches = TryGetSection(lines, DanceMusicService.IsSearchBreak);
                var songs = TryGetSection(lines, DanceMusicService.IsSongBreak);

                var admin = string.Equals(
                    reloadDatabase, "admin",
                    StringComparison.InvariantCultureIgnoreCase);
                var reload = false;
                if (string.Equals(
                    reloadDatabase, "reload",
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    reload = true;
                    if (users != null && dances != null && tags != null)
                    {
                        AdminMonitor.UpdateTask("Wipe Database");
                        // Full delete/reload worked locally - the second time???
                        await RestoreDb(userManager, roleManager, delete: true);
                    }
                }

                if (users != null)
                {
                    await Database.LoadUsers(users);
                }

                if (dances != null)
                {
                    await Database.LoadDances(dances);
                }

                if (tags != null)
                {
                    await Database.LoadTags(tags);
                }

                if (playlists != null)
                {
                    await Database.LoadPlaylists(playlists);
                }

                if (searches != null)
                {
                    await Database.LoadSearches(searches, reload);
                }

                if (songs != null)
                {
                    if (reload)
                    {
                        await Database.LoadSongs(songs);
                    }
                    else if (admin)
                    {
                        await Database.AdminUpdate(songs);
                    }
                    else
                    {
                        await Database.UpdateSongs(songs);
                    }

                    await DanceStatsManager.ClearCache(Database, true);
                }

                return CompleteAdminTask(true, "Database restored");
            }

            return CompleteAdminTask(false, "Empty File or Bad File Format");
        }
        catch (Exception e)
        {
            return FailAdminTask($"{reloadDatabase}: {e.Message}", e);
        }
    }

    //
    // Get: //FixupUser
    [HttpPost]
    [ValidateAntiForgeryToken]
    [DisableRequestSizeLimit]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> FixupUser(IFormFile fileUpload, string user,
        string idxName = "default")
    {
        try
        {
            StartAdminTask("LoadIndex");
            AdminMonitor.UpdateTask("UploadFile");

            var lines = UploadFile(fileUpload);

            AdminMonitor.UpdateTask("LoadIndex");

            var idx = Database.GetSongIndex(idxName);

            var c = await idx.FixupUser(lines, Database, user);

            if (SearchService.GetInfo(idxName).Id == SearchService.GetInfo("default").Id)
            {
                await DanceStatsManager.LoadFromAzure(Database, idxName);
            }

            return CompleteAdminTask(true, $"Index {idxName} fixed {user} in {c} songs");
        }
        catch (Exception e)
        {
            return FailAdminTask($"Fix user {user} Index ({idxName}): {e.Message}", e);
        }
    }

    private static List<string> TryGetSection(List<string> lines, Predicate<string> start)
    {
        var breaks = new Predicate<string>[]
        {
            DanceMusicService.IsDanceBreak, DanceMusicService.IsTagBreak,
            DanceMusicService.IsPlaylistBreak, DanceMusicService.IsSearchBreak,
            DanceMusicService.IsSongBreak
        };

        if (!start(lines[0]))
        {
            return null;
        }

        var i = -1;
        foreach (var b in breaks)
        {
            i = lines.FindIndex(1, b);
            if (i != -1)
            {
                break;
            }
        }

        if (i == -1)
        {
            return lines;
        }

        var ret = lines.GetRange(0, i).ToList();
        lines.RemoveRange(0, i);

        return ret;
    }

    //
    // Post: //LoadUsage
    [HttpPost]
    [ValidateAntiForgeryToken]
    [DisableRequestSizeLimit]
    [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> LoadUsage(IFormFile fileUpload)
    {
        try
        {
            StartAdminTask("UploadUsage");
            AdminMonitor.UpdateTask("UploadFile");

            //await Database.UsageLog.ExecuteDeleteAsync();
            await Database.Context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE UsageLog");
            await Database.Context.SaveChangesAsync();

            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "\t", Mode = CsvMode.NoEscape };
            using var stream = fileUpload.OpenReadStream();
            using var tr = new StreamReader(stream);
            using var csv = new CsvReader(tr, config);

            var records = csv.GetRecords<UsageLog>();
            var count = 0;
            foreach (var record in records)
            {
                record.Id = 0;
                Database.UsageLog.Add(record);
                count += 1;
            }
            await Database.Context.SaveChangesAsync();
            return CompleteAdminTask(true, $"Usage Loaded: {count} records");
        }
        catch (Exception e)
        {
            return FailAdminTask($"UploadUsage: {e.Message}", e);
        }
    }

    //
    // Get: //FlushTelemetry
    [Authorize(Roles = "dbAdmin")]
    public ActionResult FlushTelemetry()
    {
        ViewBag.Name = "Flush Telemetry";


        ViewBag.Success = true;
        ViewBag.Message = "Telemetry has been flushed";

        return View("Results");
    }

    //
    // Get: //AdminStatus
    [Authorize(Roles = "dbAdmin")]
    public ActionResult AdminStatus()
    {
        return View(AdminMonitor.Status);
    }

    #region Catalog

    //
    // Get: //ReviewBatch
    [HttpGet]
    [Authorize(Roles = "dbAdmin")]
    public ActionResult ReviewBatch(string commit, string title, int fileId, string user = null,
        string dances = null, string tags = null, string headers = null)
    {
        var review = GetReviewById(fileId);

        ViewBag.Action = commit;
        ViewBag.Title = title;
        if (!string.IsNullOrWhiteSpace(user))
        {
            ViewBag.UserName = user;
        }

        if (!string.IsNullOrWhiteSpace(dances))
        {
            ViewBag.Dance = dances;
        }

        if (!string.IsNullOrWhiteSpace(tags))
        {
            ViewBag.Tags = tags;
        }

        if (!string.IsNullOrWhiteSpace(headers))
        {
            ViewBag.Headers = headers;
        }

        if (!string.IsNullOrWhiteSpace(review.PlayList))
        {
            ViewBag.PlayList = review.PlayList;
        }

        ViewBag.FileId = fileId;

        return View(review.Merge);
    }


    //C
    // Get: //UploadCatalog
    [HttpGet]
    [Authorize(Roles = "dbAdmin")]
    public ActionResult UploadCatalog(string separator = null, string headers = null,
        string dances = null, string artist = null, string album = null, string user = null,
        string tags = null)
    {
        // TODO:  This is probably a case where creating a viewmodel would be the right things to do...
        if (!string.IsNullOrEmpty(separator))
        {
            ViewBag.Separator = separator;
        }

        if (!string.IsNullOrEmpty(headers))
        {
            ViewBag.Headers = headers;
        }

        if (!string.IsNullOrEmpty(dances))
        {
            ViewBag.Dances = dances;
        }

        if (!string.IsNullOrEmpty(artist))
        {
            ViewBag.Artist = artist;
        }

        if (!string.IsNullOrEmpty(album))
        {
            ViewBag.Album = album;
        }

        if (!string.IsNullOrEmpty(user))
        {
            ViewBag.User = user;
        }

        if (!string.IsNullOrEmpty(tags))
        {
            ViewBag.Tags = tags;
        }

        return View();
    }

    //
    // Post: //UploadCatalog
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> UploadCatalog(IFormFile file, string songs,
        string separator,
        string headers, string user, string dances, string artist, string album, string tags)
    {
        ViewBag.Name = "Upload Catalog";

        IList<string> lines = null;

        if (string.IsNullOrWhiteSpace(songs) || string.IsNullOrWhiteSpace(separator))
        {
            lines = UploadFile(file);

            if (lines == null || lines.Count < 2)
            {
                // TODO: We should validate this on the client side - only way I know to do this is to have a full on class to
                // represent the fields, is there a lighter way???
                ViewBag.Success = false;
                ViewBag.Message =
                    "Must have non-empty songs and separator fields or a valid file";
                return View("Results");
            }

            if (string.IsNullOrWhiteSpace(separator))
            {
                separator = "\t";
            }
        }

        var appuser = await Database.FindUser(user);

        lines ??= FileToLines(songs);

        var headerList = !string.IsNullOrWhiteSpace(headers)
            ? Song.BuildHeaderMap(headers, ',')
            : HeaderFromList(lines);

        var newSongs = await Song.CreateFromRows(
            appuser, separator, headerList, lines, Database,
            Song.DanceRatingCreate);

        var hasArtist = false;
        if (!string.IsNullOrEmpty(artist))
        {
            hasArtist = true;
            artist = artist.Trim();
        }

        AlbumDetails ad = null;
        var hasAlbum = false;
        if (!string.IsNullOrEmpty(album))
        {
            hasAlbum = true;
            album = album.Trim();
            ad = new AlbumDetails { Name = album };
        }

        tags = !string.IsNullOrEmpty(tags) ? tags.Trim() : null;
        m4dModels.TagList tagList = null;
        if (tags != null)
        {
            tagList = newSongs[0].VerifyTags(tags, false);
            if (tagList == null)
            {
                ViewBag.ErrorMessage = $"Invalid Tag List: {tags}";
                return View("Error");
            }
        }

        if (hasArtist || hasAlbum || tagList != null)
        {
            foreach (var sd in newSongs)
            {
                if (hasArtist && string.IsNullOrEmpty(sd.Artist))
                {
                    sd.Artist = artist;
                }

                if (hasAlbum && sd.Albums.Count == 0)
                {
                    sd.Albums.Add(ad);
                }

                if (tagList != null)
                {
                    sd.AddTags(tagList, appuser.UserName, Database.DanceStats, sd, false);
                }
            }
        }

        ViewBag.UserName = user;
        ViewBag.Dances = dances;
        ViewBag.Separator = separator;
        ViewBag.Headers = headers;
        ViewBag.Artist = artist;
        ViewBag.Album = album;
        ViewBag.Tags = tags;
        ViewBag.Action = "CommitUploadCatalog";

        // ReSharper disable once InvertIf
        if (newSongs.Count > 0)
        {
            var results =
                await SongIndex.MatchSongs(newSongs, DanceMusicCoreService.MatchMethod.Merge);
            var review = new Review { Merge = results };
            ViewBag.FileId = CacheReview(review);
            return View("ReviewBatch", review.Merge);
        }

        return View("Error");
    }


    //
    // Post: //CommitUploadCatalog
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> CommitUploadCatalog(int fileId, string userName,
        string danceIds,
        string headers, string separator)
    {
        var initial = GetReviewById(fileId);

        ViewBag.Name = "Upload Catalog";
        ViewBag.FileId = fileId;
        ViewBag.User = userName;
        ViewBag.Dances = danceIds;
        ViewBag.Headers = headers;
        ViewBag.Separator = separator;

        if (string.IsNullOrEmpty(userName))
        {
            userName = UserName;
        }

        return View(
            await CommitCatalog(
                Database, initial,
                await Database.FindUser(userName), danceIds) == 0
                ? "Error"
                : "UploadCatalog");
    }

    #endregion


    //
    // Get: //IndexBackup
    [Authorize(Roles = "showDiagnostics")]
    public async Task<ActionResult> IndexBackup([FromServices] IWebHostEnvironment environment,
        string name = "default", int count = -1, string filter = null)
    {
        try
        {
            StartAdminTask("Index Backup");

            var dt = DateTime.Now;
            var fname = $"index-{dt.Year:d4}-{dt.Month:d2}-{dt.Day:d2}.txt";
            var path = Path.Combine(EnsureAppData(environment), fname);

            var n = 0;
            await using (var file = System.IO.File.CreateText(path))
            {
                var lines = await Database.GetSongIndex(name).BackupIndex(
                    count,
                    filter == null ? null : new SongFilter(filter));
                foreach (var line in lines)
                {
                    await file.WriteLineAsync(line);
                    AdminMonitor.UpdateTask("writeSongs", ++n);
                }

                file.Close();
            }

            AdminMonitor.CompleteTask(true, $"Backup ({n} songs) complete to: {path}");
            return File("~/AppData/" + fname, MediaTypeNames.Text.Plain, fname);
        }
        catch (Exception e)
        {
            return FailAdminTask("Failed to backup index", e);
        }
    }

    //
    // Get: //BackupDatabase
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult BackupDatabase([FromServices] IWebHostEnvironment environment,
        bool users = true, bool tags = true, bool dances = true, bool playlists = true,
        bool searches = true, bool songs = true, string useLookupHistory = null)
    {
        try
        {
            StartAdminTask("Backup");

            var history = !string.IsNullOrWhiteSpace(useLookupHistory);

            var dt = DateTime.Now;
            var h = history ? "-lookup" : string.Empty;
            var fname = $"backup-{dt.Year:d4}-{dt.Month:d2}-{dt.Day:d2}{h}.txt";
            var path = Path.Combine(EnsureAppData(environment), fname);

            Database.Context.Database.SetCommandTimeout(new TimeSpan(0, 5, 0));

            using (var file = System.IO.File.CreateText(path))
            {
                if (users)
                {
                    var n = 0;
                    AdminMonitor.UpdateTask("users");
                    foreach (var line in Database.SerializeUsers())
                    {
                        file.WriteLine(line);
                        AdminMonitor.UpdateTask("users", ++n);
                    }
                }

                if (dances)
                {
                    var n = 0;
                    AdminMonitor.UpdateTask("dances");
                    foreach (var line in Database.SerializeDances())
                    {
                        file.WriteLine(line);
                        AdminMonitor.UpdateTask("dances", ++n);
                    }
                }

                if (tags)
                {
                    var n = 0;
                    AdminMonitor.UpdateTask("tags");
                    foreach (var line in Database.SerializeTags())
                    {
                        file.WriteLine(line);
                        AdminMonitor.UpdateTask("tags", ++n);
                    }
                }

                if (playlists)
                {
                    var n = 0;
                    AdminMonitor.UpdateTask("playlists");
                    foreach (var line in Database.SerializePlaylists())
                    {
                        file.WriteLine(line);
                        AdminMonitor.UpdateTask("playlists", ++n);
                    }
                }

                if (searches)
                {
                    var n = 0;
                    AdminMonitor.UpdateTask("searches");
                    foreach (var line in Database.SerializeSearches())
                    {
                        file.WriteLine(line);
                        AdminMonitor.UpdateTask("searches", ++n);
                    }
                }

                // DBKILL: Should we enable a way to do this dump from the azure search DB?
                //if (songs)
                //{
                //    Context.Configuration.LazyLoadingEnabled = false;
                //    var n = 0;
                //    AdminMonitor.UpdateTask("songs");
                //    var lines = Database.SerializeSongs(true, history);
                //    AdminMonitor.UpdateTask("writeSongs");
                //    foreach (var line in lines)
                //    {
                //        file.WriteLine(line);
                //        AdminMonitor.UpdateTask("writeSongs", ++n);
                //    }
                //    Context.Configuration.LazyLoadingEnabled = true;
                //}
            }

            AdminMonitor.CompleteTask(true, "Backup complete to: " + path);
            return File("~/AppData/" + fname, MediaTypeNames.Text.Plain, fname);
            //AdminMonitor.CompleteTask(true, "Backup complete to: " + path);
            //var res = new FilePathResult("~/content/" + fname,System.Net.Mime.MediaTypeNames.Text.Plain);
            //res.FileDownloadName = fname;
            //return res;
        }
        catch (Exception e)
        {
            return FailAdminTask("Failed to backup database", e);
            //AdminMonitor.CompleteTask(false, "Failed to backup database", e);

            //var bytes = Encoding.UTF8.GetBytes(e.Message);
            //var stream = new MemoryStream(bytes);

            //var dt = DateTime.Now;
            //return File(stream, "text/plain", string.Format("backup-error-{0:d4}-{1:d2}-{2:d2}.txt", dt.Year, dt.Month, dt.Day));
        }
    }

    //
    // Get: //BackupTail
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult BackupTail(int count = 100, DateTime? from = null, string filter = null)
    {
        var users = Database.SerializeUsers(true, from);
        var dances = Database.SerializeDances(true, from);
        var tags = Database.SerializeTags(true, from);
        var playlists = Database.SerializeTags(true, from);
        var searches = Database.SerializeSearches(true, from);

        SongFilter songFilter = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            songFilter = new SongFilter(filter);
        }

        var songs = Database.SerializeSongs(true, true, count, from, songFilter);

        var s = string.Empty;
        if (users.Count > 0)
        {
            s += string.Join("\r\n", users) + "\r\n";
        }

        if (dances.Count > 0)
        {
            s += string.Join("\r\n", dances) + "\r\n";
        }

        if (tags.Count > 0)
        {
            s += string.Join("\r\n", tags) + "\r\n";
        }

        if (playlists.Count > 0)
        {
            s += string.Join("\r\n", playlists) + "\r\n";
        }

        if (searches.Count > 0)
        {
            s += string.Join("\r\n", searches) + "\r\n";
        }

        s += string.Join("\r\n", songs);

        var bytes = Encoding.UTF8.GetBytes(s);
        var stream = new MemoryStream(bytes);

        var dt = DateTime.Now;
        return File(stream, "text/plain", $"tail-{dt.Year:d4}-{dt.Month:d2}-{dt.Day:d2}.txt");
    }

    //
    // Get: //BackupDelta
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult BackupDelta(IFormFile file)
    {
        var lines = UploadFile(file);

        var exclusions = new HashSet<Guid>();
        foreach (var line in lines)
        {
            if (Guid.TryParse(line, out var guid))
            {
                exclusions.Add(guid);
            }
        }

        var songs = Database.SerializeSongs(true, true, -1, null, null, exclusions);

        var s = string.Join("\r\n", songs);
        var bytes = Encoding.UTF8.GetBytes(s);
        var stream = new MemoryStream(bytes);

        var dt = DateTime.Now;
        return File(stream, "text/plain", $"tail-{dt.Year:d4}-{dt.Month:d2}-{dt.Day:d2}.txt");
    }


    //
    // Get: //BackupTail
    [Authorize(Roles = "showDiagnostics")]
    public async Task<ActionResult> ExportCsv()
    {
        var rawFilter = new RawSearch { ODataFilter = "Sample ne null and Sample ne '.' and DanceTags/any()" };
        var exporter = new PlaylistExport(
            new ExportInfo
            {
                Title = "Full music4dance export",
                Filter = new SongFilter(rawFilter).ToString(),
                Count = -1,
                Description = "Please do not distribute this file publicly and remember to credit music4dance.net in any derived work. " +
                $"Copyright © {DateTime.Now.Year} by music4dance.net",
                IsPremium = true,
                IsSelf = true,
            }, SongIndex, UserManager, TaskQueue);

        var bytes = await exporter.ExportFilteredDances(UserName);
        var stream = new MemoryStream(bytes);

        var dt = DateTime.Now;
        return File(stream, "text/csv", $"songs-{dt.Year:d4}-{dt.Month:d2}-{dt.Day:d2}.csv");
    }

    //
    // Get: //RestoreDatabase
    //[Authorize(Roles = "dbAdmin")]
    [AllowAnonymous]
    public async Task<IActionResult> RestoreDatabase([FromServices] UserManager<ApplicationUser> userManager,
        [FromServices] RoleManager<IdentityRole> roleManager)
    {
        await RestoreDb(userManager, roleManager);

        ViewBag.Name = "Restore Database";
        ViewBag.Success = true;
        ViewBag.Message = "Database was successfully restored.";

        return View("Results");
    }

    //
    // Get: //UpdateDatabase
    [AllowAnonymous]
    public ActionResult UpdateDatabase(string state = null)
    {
        // CORETODO: Do we need this?
        Logger.LogInformation("Updating Database");
        //var configuration = new Configuration();
        //var migrator = new DbMigrator(configuration);
        //migrator.Update(state);

        //ViewBag.Name = "Update Database";
        //ViewBag.Success = true;
        //ViewBag.Message = "Database was successfully updated.";

        return View("Results");
    }

    #endregion

    #region Search

    //
    // Get: //ResetSearchIdx
    [Authorize(Roles = "showDiagnostics")]
    public async Task<ActionResult> ResetSearchIdx(string id = "default")
    {
        try
        {
            StartAdminTask("ResetIndex");

            var idx = Database.GetSongIndex(id);
            var success = (await idx.ResetIndex()) != null;

            ViewBag.Name = "Reset Index";
            ViewBag.Success = success;
            ViewBag.Message = @"Index Reset";

            return CompleteAdminTask(success, @"Index Reset");
        }
        catch (Exception e)
        {
            return FailAdminTask($"Reset: {e.Message}", e);
        }
    }
    #endregion

    #region Migration-Restore

    private async Task RestoreDb([FromServices] UserManager<ApplicationUser> userManager,
        [FromServices] RoleManager<IdentityRole> roleManager, string targetMigration = null,
        bool delete = false)
    {
        var context = Database.Context;
        var migrator = context.Database.GetService<IMigrator>();

        if (delete)
        {
            Logger.LogInformation("Deleting Database");
            context.Database.EnsureDeleted();
        }

        Logger.LogInformation($"Migrating to {targetMigration}");
        migrator.Migrate(targetMigration);

        await ReseedDb(userManager, roleManager);

        Logger.LogInformation("Exiting RestoreDB");
    }

    private async Task ReseedDb(UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await UserManagerHelpers.SeedData(userManager, roleManager, Configuration);
    }

    #endregion

    #region Utilities

    internal void SetupDiagnosticAttributes()
    {
        ViewData["TraceLevel"] = TraceLevels.General.Level.ToString();
        ViewData["BotReport"] = SpiderManager.CreateBotReport();
        ViewData["SearchIdx"] = SearchService.DefaultId;
        ViewData["StatsUpdateTime"] = DanceStatsManager.LastUpdate;
        ViewData["StatsUpdateSource"] = DanceStatsManager.Source;
#if DEBUG
        ViewData["Debug"] = true;
#else
        ViewData["Debug"] = false;
#endif
    }


    private IList<string> HeaderFromList(IList<string> songs)
    {
        if (songs.Count < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(songs));
        }

        var line = songs[0];

        var map = Song.BuildHeaderMap(line);

        // Kind of kludgy, but temporary build the header
        //  map to see if it's valid then pass back a cownomma
        // separated list of headers...
        if (map == null || map.All(p => p == null))
        {
            return null;
        }

        songs.RemoveAt(0);
        return map;
    }

    private static IList<string> FileToLines(string file)
    {
        return [.. file.Split(
            Environment.NewLine.ToCharArray(),
            StringSplitOptions.RemoveEmptyEntries)];
    }

    public static int CacheReview(Review review)
    {
        int ret;
        lock (Reviews)
        {
            Reviews.Add(review);
            ret = Reviews.Count - 1;
        }

        return ret;
    }

    private static Review GetReviewById(int id)
    {
        lock (Reviews)
        {
            return Reviews[id];
        }
    }

    // This is pretty kludgy as this is a basically a temporary
    //  store that only gets recycled on restart - but since
    //  for now it's being using primarily on the short running
    //  dev instance, it doesn't seem worthwhile to do anything
    //  more sophisticated
    private static readonly IList<Review> Reviews = [];

    #endregion
}
