using CsvHelper;

using m4d.Areas.Identity;
using m4d.Services;
using m4d.Services.Diagnostics;
using m4d.Services.ServiceHealth;
using m4d.Utilities;
using m4d.ViewModels;

using m4dModels.Utilities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

using System;
using System.Globalization;
using System.Net.Mime;
using System.Text;

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
        // Set up static diagnostic data before the action executes
        if (context.Controller is AdminController controller)
        {
            controller.SetupDiagnosticAttributes();
        }
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        // Set up GC/dump data after action completes, but only for Diagnostics view
        // and only if not already set by the action
        if (context.Result is ViewResult viewResult &&
            (viewResult.ViewName == "Diagnostics" || viewResult.ViewName == null && context.RouteData.Values["action"]?.ToString() == "Diagnostics") &&
            context.Controller is AdminController controller)
        {
            controller.ViewBag.GcSnapshot ??= GcDiagnostics.CaptureSnapshot();
            controller.ViewBag.RecentDumps ??= GcDiagnostics.GetRecentDumps();
        }
    }
}


[Authorize]
[SetupDiagnostics]
public class AdminController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
    IFeatureManagerSnapshot featureManager, ILogger<ActivityLogController> logger,
    IOptionsMonitor<LoggerFilterOptions> loggerFilterOptions,
    ServiceHealthManager serviceHealth
) : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
    fileProvider, backroundTaskQueue, featureManager, logger, serviceHealth)
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
        // GcSnapshot and RecentDumps set by filter
        return View();
    }

    //
    // GET: /Admin/CaptureGcSnapshot
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult CaptureGcSnapshot()
    {
        var snapshot = GcDiagnostics.CaptureSnapshot();
        Logger.LogInformation("GC Snapshot captured: TotalMemory={TotalMB:F2}MB, Heap={HeapMB:F2}MB, Fragmented={FragMB:F2}MB, " +
            "Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}, WorkingSet={WorkingSetMB:F2}MB, MemoryLoad={LoadPct:F1}%",
            snapshot.TotalMemoryMB, snapshot.HeapSizeMB, snapshot.FragmentedMB,
            snapshot.Gen0Collections, snapshot.Gen1Collections, snapshot.Gen2Collections,
            snapshot.WorkingSetMB, snapshot.MemoryLoadPercent);

        ViewBag.GcSnapshot = snapshot; // Explicit - we want THIS snapshot logged
        ViewBag.Message = $"GC Snapshot captured at {snapshot.CapturedAt:HH:mm:ss.fff} and logged.";
        return View("Diagnostics");
    }

    //
    // POST: /Admin/ForceGarbageCollection
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public ActionResult ForceGarbageCollection()
    {
        var (before, after, memoryDelta) = GcDiagnostics.ForceCollectionWithMetrics();

        var deltaMB = memoryDelta / (1024.0 * 1024.0);
        Logger.LogWarning("Forced GC: Before={BeforeMB:F2}MB, After={AfterMB:F2}MB, Delta={DeltaMB:F2}MB",
            before.TotalMemoryMB, after.TotalMemoryMB, deltaMB);

        // Explicit - we need before/after snapshots for comparison display
        ViewBag.GcSnapshot = after;
        ViewBag.GcBefore = before;
        ViewBag.GcAfter = after;
        ViewBag.MemoryDelta = memoryDelta;

        if (memoryDelta >= 0)
            ViewBag.Message = $"Forced GC completed. Freed {deltaMB:F2} MB.";
        else
            ViewBag.Message = $"Forced GC completed. Memory increased by {-deltaMB:F2} MB.";

        return View("Diagnostics");
    }

    //
    // GET: /Admin/ResetAdmin
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult ResetAdmin()
    {
        AdminMonitor.CompleteTask(false, "Force Reset");
        ViewBag.Message = "Admin task reset.";
        // GcSnapshot and RecentDumps set by filter
        return View("Diagnostics");
    }

    //
    // POST: /Admin/CreateMemoryDump
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> CreateMemoryDump(DumpType dumpType = DumpType.Heap, bool download = false)
    {
        Logger.LogWarning("Memory dump requested: Type={DumpType}, Download={Download}", dumpType, download);

        var result = await GcDiagnostics.CreateDumpAsync(dumpType: dumpType);

        if (result.Success && download && result.FilePath != null && System.IO.File.Exists(result.FilePath))
        {
            Logger.LogInformation("Memory dump created and downloading: {FilePath} ({SizeMB:F2} MB)",
                result.FilePath, result.FileSizeMB);

            return PhysicalFile(result.FilePath, "application/octet-stream", result.FileName);
        }

        ViewBag.DumpResult = result;

        if (result.Success)
        {
            Logger.LogInformation("Memory dump created: {FilePath} ({SizeMB:F2} MB)",
                result.FilePath, result.FileSizeMB);
            ViewBag.Message = $"Memory dump created: {result.FilePath} ({result.FileSizeMB:F2} MB)";
            ViewBag.ErrorMessage = null;
        }
        else
        {
            Logger.LogError("Memory dump failed: {Error}", result.ErrorMessage);
            ViewBag.ErrorMessage = $"Memory dump failed: {result.ErrorMessage}";
            ViewBag.Message = null;
        }

        // GcSnapshot and RecentDumps set by filter (will show new dump)
        return View("Diagnostics");
    }

    //
    // GET: /Admin/DownloadDump
    [Authorize(Roles = "dbAdmin")]
    public ActionResult DownloadDump(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("File name is required");
        }

        // Security: Only allow downloading from the known dump directory
        var dumpDir = GcDiagnostics.DefaultDumpDirectory;
        var filePath = Path.Combine(dumpDir, Path.GetFileName(fileName));

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound($"Dump file not found: {fileName}");
        }

        Logger.LogInformation("Downloading dump file: {FilePath}", filePath);
        return PhysicalFile(filePath, "application/octet-stream", Path.GetFileName(filePath));
    }

    //
    // POST: /Admin/DeleteDump
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public ActionResult DeleteDump(string fileName)
    {
        if (GcDiagnostics.DeleteDump(fileName))
        {
            Logger.LogInformation("Deleted dump file: {FileName}", fileName);
            ViewBag.Message = $"Deleted dump file: {fileName}";
        }
        else
        {
            Logger.LogWarning("Failed to delete dump file: {FileName}", fileName);
            ViewBag.ErrorMessage = $"Failed to delete dump file: {fileName}";
        }

        // GcSnapshot and RecentDumps set by filter (will show updated list)
        return View("Diagnostics");
    }

    //
    // POST: /Admin/DeleteAllDumps
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public ActionResult DeleteAllDumps()
    {
        var count = GcDiagnostics.DeleteAllDumps();
        Logger.LogInformation("Deleted {Count} dump files", count);
        ViewBag.Message = $"Deleted {count} dump file(s).";

        // GcSnapshot and RecentDumps set by filter (will show empty list)
        return View("Diagnostics");
    }

    //
    // GET: /Admin/DumpCleanupCount
    [Authorize(Roles = "showDiagnostics")]
    public ActionResult DumpCleanupCount()
    {
        Song.DumpCleanupCount();
        // GcSnapshot and RecentDumps set by filter
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
    // Get: //ThrowException
    [AllowAnonymous]
    public ActionResult ThrowException()
    {
        throw new Exception("This is an intentional exception");
    }

    //
    // GET: /Admin/SetLogLevel
    [AllowAnonymous]
    public ActionResult SetLogLevel(LogLevel level)
    {
        ViewBag.Name = "Set Log Level";
        loggerFilterOptions.CurrentValue.MinLevel = level;
        ViewBag.Success = true;
        ViewBag.Message = $"Log level set: {level}";
        ViewBag.CurrentLogLevel = loggerFilterOptions.CurrentValue.MinLevel.ToString();
        return View("Diagnostics");
    }

    //
    // Get: //TestLog
    [AllowAnonymous]
    public ActionResult TestLog(string message, LogLevel level)
    {
        ViewBag.Name = "Test Log";

        var logEnabled = Logger.IsEnabled(level);

        Logger.Log(level, $"Test Log via Logger: {level} - {message}");
        Console.WriteLine($"Test Log via Console: {level} - {message}");

        ViewBag.Success = true;
        ViewBag.Message = $"Log message sent: '{message}'. LogLevel is enabled = {logEnabled}";
        ViewBag.CurrentLogLevel = loggerFilterOptions.CurrentValue.MinLevel.ToString();

        return View("Diagnostics");
    }

    //
    // Get: //SetSearchIdx
    public ActionResult SetSearchIdx(string id)
    {
        Logger.LogInformation($"Set Search Index: '{id}'");
        SearchService.DefaultId = id;

        _ = DanceStatsManager.LoadFromAzure(Database, id);

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
                _ = await idx.ResetIndex();
            }

            var c = await idx.UploadIndex(lines, !reset);

            if (SearchService.GetInfo(idxName).Id == SearchService.GetInfo("default").Id)
            {
                _ = await DanceStatsManager.LoadFromAzure(Database, idxName);
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
                _ = await DanceStatsManager.LoadFromAzure(Database, idxName);
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
    // Post: //LoadUsage (incremental - appends to existing data)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [DisableRequestSizeLimit]
    [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> LoadUsage(IFormFile fileUpload, int batchSize = 5000)
    {
        try
        {
            if (fileUpload == null)
            {
                return BadRequest("Usage file is required.");
            }

            if (!TryGetValidUsageBatchSize(batchSize, out var validBatchSize, out var errorMessage))
            {
                return BadRequest(errorMessage);
            }

            StartAdminTask("UploadUsage");
            AdminMonitor.UpdateTask("UploadFile");

            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = "\t",
                Mode = CsvMode.NoEscape
            };

            await using var stream = fileUpload.OpenReadStream();
            using var tr = new StreamReader(stream);
            using var csv = new CsvReader(tr, config);

            var count = await LoadUsageRecordsAsync(csv, validBatchSize);

            return CompleteAdminTask(true, $"Usage Loaded (incremental): {count} records");
        }
        catch (Exception e)
        {
            return FailAdminTask($"UploadUsage: {e.Message}", e);
        }
    }

    //
    // Post: //LoadUsageFromAppData (streams large file from AppData directory)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> LoadUsageFromAppData(
        [FromServices] IWebHostEnvironment environment,
        string fileName = "usage.tsv",
        int batchSize = 5000,
        bool truncate = true)
    {
        try
        {
            if (!TryGetValidUsageBatchSize(batchSize, out var validBatchSize, out var errorMessage))
            {
                return BadRequest(errorMessage);
            }

            StartAdminTask("LoadUsageFromAppData");

            var appDataPath = EnsureAppData(environment);
            var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "usage.tsv" : fileName);
            var filePath = Path.Combine(appDataPath, safeFileName);

            if (!System.IO.File.Exists(filePath))
            {
                return FailAdminTask($"File not found: {filePath}", null);
            }

            var fileInfo = new FileInfo(filePath);
            AdminMonitor.UpdateTask($"Found file: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");

            if (truncate)
            {
                AdminMonitor.UpdateTask("Truncating table");
                _ = await Database.Context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE UsageLog");
            }

            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = "\t",
                Mode = CsvMode.NoEscape
            };

            AdminMonitor.UpdateTask("Streaming file");

            // Use large buffer for efficient streaming of large files
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 65536,
                useAsync: true);

            using var tr = new StreamReader(stream, bufferSize: 65536);
            using var csv = new CsvReader(tr, config);

            var count = await LoadUsageRecordsAsync(csv, validBatchSize);

            return CompleteAdminTask(true, $"Usage Loaded from AppData: {count:N0} records");
        }
        catch (Exception e)
        {
            return FailAdminTask($"LoadUsageFromAppData: {e.Message}", e);
        }
    }

    /// <summary>
    /// Shared helper to load UsageLog records from a CsvReader in batches.
    /// </summary>
    private async Task<int> LoadUsageRecordsAsync(CsvReader csv, int batchSize)
    {
        var records = csv.GetRecords<UsageLog>();
        var count = 0;
        var batch = new List<UsageLog>(batchSize);

        foreach (var record in records)
        {
            record.Id = 0;
            batch.Add(record);

            if (batch.Count >= batchSize)
            {
                await Database.UsageLog.AddRangeAsync(batch);
                _ = await Database.Context.SaveChangesAsync();
                Database.Context.ChangeTracker.Clear();

                count += batch.Count;
                AdminMonitor.UpdateTask("Loading records", count);
                batch.Clear();
            }
        }

        // Handle remaining records in the final batch
        if (batch.Count > 0)
        {
            await Database.UsageLog.AddRangeAsync(batch);
            _ = await Database.Context.SaveChangesAsync();
            Database.Context.ChangeTracker.Clear();
            count += batch.Count;
            AdminMonitor.UpdateTask("Loading records", count);
        }

        return count;
    }

    private const int MinUsageBatchSize = 100;
    private const int MaxUsageBatchSize = 50000;

    private static bool TryGetValidUsageBatchSize(int requested, out int validBatchSize, out string? errorMessage)
    {
        validBatchSize = Math.Clamp(requested, MinUsageBatchSize, MaxUsageBatchSize);
        if (requested < MinUsageBatchSize || requested > MaxUsageBatchSize)
        {
            errorMessage = $"Batch size must be between {MinUsageBatchSize} and {MaxUsageBatchSize}.";
            return false;
        }

        errorMessage = null;
        return true;
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

        if (string.IsNullOrWhiteSpace(user))
        {
            throw new ArgumentNullException(
                nameof(user), "You must specify a user");
        }

        var appuser = await Database.FindUser(user);

        if (appuser == null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(user), $"'{user}' does not exist.");
        }

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

        lines ??= FileToLines(songs);

        var headerList = !string.IsNullOrWhiteSpace(headers)
            ? Song.BuildHeaderMap(headers, ',')
            : HeaderFromList(lines);

        var newSongs = await Song.CreateFromRows(
            appuser, separator, headerList, lines, Database,
            Song.DanceRatingCreate);

        SongProperty artistProp = null;
        var hasArtist = false;
        if (!string.IsNullOrEmpty(artist))
        {
            hasArtist = true;
            artist = artist.Trim();
            artistProp = SongProperty.Create(Song.ArtistField, artist);
        }

        SongProperty albumProp = null;
        var hasAlbum = false;
        if (!string.IsNullOrEmpty(album))
        {
            hasAlbum = true;
            album = album.Trim();
            albumProp = new SongProperty(Song.AlbumField, album, index: 0);
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
                var props = new List<SongProperty>();
                var addHeader = false;
                if (hasArtist && !string.Equals(artist, sd.Artist))
                {
                    addHeader = !string.IsNullOrEmpty(sd.Artist);
                    props.Add(artistProp);
                }

                // We just won't handle the case with album "override" now
                if (hasAlbum && sd.AlbumList.Length == 0)
                {
                    props.Add(albumProp);
                }

                if (tagList != null)
                {
                    var newTags = tagList;
                    var oldTags = sd.GetUserTags(appuser.UserName);
                    if (!oldTags.IsEmpty)
                    {
                        newTags = tagList.Subtract(oldTags);
                    }

                    if (!newTags.IsEmpty)
                    {
                        props.Add(SongProperty.Create(Song.AddedTags, newTags.ToString()));
                    }
                }

                if (props.Count > 0)
                {
                    _ = await sd.AdminAppend(addHeader ? appuser : null, props, Database);
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
                    filter == null ? null : Database.SearchService.GetSongFilter(filter));
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
            songFilter = Database.SearchService.GetSongFilter(filter);
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
                _ = exclusions.Add(guid);
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
                Filter = Database.SearchService.GetSongFilter(rawFilter).ToString(),
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

    //
    // Get: UpdateSearchIdx
    [Authorize(Roles = "showDiagnostics")]
    public async Task<ActionResult> UpdateSearchIdx()
    {
        try
        {
            StartAdminTask("UpdateSearchIdx");

            if (SearchService.DefaultId == SearchServiceManager.ExperimentalId)
            {
                Logger.LogInformation("Can't update experimental version, skipping index update");
                ViewBag.Name = "Update Index";
                ViewBag.Success = true;
                ViewBag.Message = "Index is already up to date";
                return CompleteAdminTask(false, @"UpdateSearchIdx");
            }

            if (!SearchService.HasNextVersion || SearchService.NextVersion)
            {
                Logger.LogInformation("No next version or already updated, skipping index update");
                ViewBag.Name = "Update Index";
                ViewBag.Success = true;
                ViewBag.Message = "Index is already up to date";
                return CompleteAdminTask(false, @"UpdateSearchIdx");
            }

            var message = @"We are in the process of upgrading the music4dance.net infrastructure.
                You should still be able to use the site to browse and search but please
                hold off doing any editing or registering an account until this banner disappears
                (reload the page to check status). Thanks!";

            GlobalState.UpdateMessage = message?.CleanWhitespace();

            await Database.UpdateIndex();

            AdminMonitor.UpdateTask("Reloading Stats");

            _ = await DanceStatsManager.LoadFromAzure(Database);

            ViewBag.Name = "Update Index";
            ViewBag.Success = true;
            ViewBag.Message = @"Index Updated";

            GlobalState.UpdateMessage = null;

            return CompleteAdminTask(true, @"UpdateSearchIdx");
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
            _ = context.Database.EnsureDeleted();
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
        ViewData["CurrentLogLevel"] = loggerFilterOptions.CurrentValue.MinLevel.ToString();
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
        //  map to see if it's valid then pass back a comma
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
