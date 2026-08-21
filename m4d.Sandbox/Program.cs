using m4d.Areas.Identity;
using m4d.Configuration;
using m4d.Services.ServiceHealth;

using m4dModels;
using m4dModels.Sandbox;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

// The no-external-service local server: builds on the real m4d controllers/views/middleware
// (via the ProjectReference to m4d.csproj) but replaces the SQL Server database and Azure
// Search with in-memory stand-ins from m4dModels.Sandbox, so it needs nothing installed. See
// architecture/contributor-test-environments.md, L1b.

const string SandboxDbName = "m4d-sandbox";

// Resets the static DanceLibrary.Dances registry from the embedded sandbox dataset before
// anything touches DanceStatsManager - DanceStats.DanceObject/.DanceName resolve against this
// registry, and without it DanceStatsHostedService fails during its own startup.
await SandboxServiceFactory.LoadDances();

var sandboxOptions = new M4dApplicationOptions { ConfigureDatabase = false, ConfigureSearch = false };

// m4d.Sandbox has no wwwroot of its own - the real static assets (CSS/JS, the Vite manifest,
// content/*.json read via IWebHostEnvironment.WebRootFileProvider) live in m4d/wwwroot. Point
// WebRootPath there explicitly rather than leaving it null (the ASP.NET Core default when the
// convention path doesn't exist), which otherwise breaks Vite.AspNetCore's manifest lookup and
// any static-file serving. ContentRootPath stays m4d.Sandbox's own, so this project's
// appsettings.json is still what gets loaded.
var webRootPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "m4d", "wwwroot"));

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = webRootPath
});

builder.AddM4dApplication(connectionString: null, appOptions: sandboxOptions);

builder.Services.AddDbContext<DanceMusicContext>(options =>
    options.UseInMemoryDatabase(SandboxDbName));

builder.Services.AddSingleton<ISearchServiceManager, LocalSearchServiceManager>();
builder.Services.AddSingleton<IDanceStatsManager>(new DanceStatsManager(new LocalDanceStatsFileManager()));

var mailDirectory = Path.Combine(builder.Environment.ContentRootPath, "..", "local", "mail");
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender>(
    _ => new FileEmailSender(mailDirectory));

// Several controllers/pages read loose content files (content/dances.json, sitemap files) via
// the injected IFileProvider using paths relative to the content root - e.g.
// DanceMusicController.ReadJsonFile's "/wwwroot/content/{name}.json". Those files live in m4d/,
// not m4d.Sandbox/ (which has no wwwroot of its own), so override AddM4dApplication's
// IFileProvider (rooted at m4d.Sandbox's own content root) to point at m4d's directory instead.
// appsettings.json/configuration loading is untouched - only file-serving is redirected.
var m4dContentRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "m4d"));
var m4dFileProvider = new CompositeFileProvider(
    new PhysicalFileProvider(m4dContentRoot),
    new EmbeddedFileProvider(typeof(m4d.Utilities.GlobalState).Assembly));
builder.Services.AddSingleton<IFileProvider>(m4dFileProvider);

var app = builder.Build();

var serviceHealth = app.Services.GetRequiredService<ServiceHealthManager>();
serviceHealth.MarkHealthy("Database");

using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await UserManagerHelpers.SeedData(userManager, roleManager, builder.Configuration);
}

await app.UseM4dPipeline(connectionString: null, useProdDb: false, useTestDb: false, appOptions: sandboxOptions);

// Starts Kestrel and runs every IHostedService.StartAsync - including DanceStatsHostedService,
// which calls IDanceStatsManager.Initialize(). Song seeding needs that to have already happened
// (DanceStats.Instance is null until then), so it can't run before this point, and
// DanceStatsManager.Initialize throws if called twice - so it can't be done manually either.
await app.StartAsync();

List<(Guid Id, string Title)> seededSongs;
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;

    // dancestatistics.txt (the sandbox's already-public, PII-cleaned dataset) carries a real
    // song history in its "cachedSongs" array - LocalDanceStatsFileManager clears that array
    // before handing the file to DanceStatsManager (so the stats cache starts empty each run),
    // but the same entries are exactly what SeedSongs loads here into the actual song database,
    // via the same Song.Create serialized-format path tests use (see testing-patterns.md /
    // CLAUDE.md).
    var dms = new DanceMusicService(
        sp.GetRequiredService<DanceMusicContext>(), sp.GetRequiredService<UserManager<ApplicationUser>>(),
        sp.GetRequiredService<ISearchServiceManager>(), sp.GetRequiredService<IDanceStatsManager>());

    seededSongs = await SeedSongs(dms);
}

PrintStartupBanner(app, builder.Configuration, seededSongs);

await app.WaitForShutdownAsync();

static async Task<List<(Guid Id, string Title)>> SeedSongs(DanceMusicService dms)
{
    var rawSongs = await SandboxServiceFactory.LoadCachedSongs();

    var songs = new List<Song>();
    foreach (var raw in rawSongs)
    {
        try
        {
            songs.Add(await Song.Create(raw, dms));
        }
        catch (Exception ex)
        {
            // A handful of historical entries may not survive being replayed against the
            // sandbox's trimmed dance/tag registry - skip and keep going rather than let one
            // bad entry take down every dev's clean-slate startup.
            Console.WriteLine($"WARNING: Skipped a seed song that failed to load: {ex.Message}");
        }
    }

    await dms.SongIndex.SaveSongs(songs);

    return [.. songs.Select(s => (s.SongId, s.Title))];
}

static void PrintStartupBanner(
    WebApplication app, IConfiguration configuration, List<(Guid Id, string Title)> seededSongs)
{
    var addresses = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "(see 'Now listening on' above)";
    var baseUrl = app.Urls.FirstOrDefault();

    Console.WriteLine();
    Console.WriteLine("========================================================================");
    Console.WriteLine(" music4dance sandbox — no external services, nothing installed");
    Console.WriteLine("========================================================================");
    Console.WriteLine($" Listening on: {addresses}");
    Console.WriteLine();
    Console.WriteLine(" Seeded accounts (see appsettings.json to change, or set the env vars");
    Console.WriteLine(" M4D_ADMIN_USER/M4D_TEST_USER/M4D_EDITOR_USER + _PASSWORD before running):");
    PrintSeededUser(configuration, "admin", "canTag, canEdit, showDiagnostics, dbAdmin", "M4D_ADMIN_USER");
    PrintSeededUser(configuration, "plain, roleless", "voting/tagging as an ordinary user", "M4D_TEST_USER");
    PrintSeededUser(configuration, "canEdit only", "bulk tag removal / full song edit", "M4D_EDITOR_USER");
    Console.WriteLine();
    Console.WriteLine($" Seeded {seededSongs.Count} songs (here are a few to start with):");
    foreach (var (id, title) in seededSongs.Take(8))
    {
        Console.WriteLine($"   - {baseUrl}/Song/Details/{id}  ({title})");
    }
    if (seededSongs.Count > 8)
    {
        Console.WriteLine($"   ... and {seededSongs.Count - 8} more, only reachable directly by SongId until search lands");
    }
    Console.WriteLine();
    Console.WriteLine(" ⚠ Search relevance is NOT representative - SongIndexLocal is an in-memory");
    Console.WriteLine("   stand-in for Azure Search using simple substring/LINQ matching, not real ranking.");
    Console.WriteLine(" ⚠ All state is in-memory. Ctrl+C and re-run for a clean slate.");
    Console.WriteLine("========================================================================");
    Console.WriteLine();
}

static void PrintSeededUser(IConfiguration configuration, string role, string covers, string envVarPrefix)
{
    var name = configuration[envVarPrefix];
    if (!string.IsNullOrEmpty(name))
    {
        Console.WriteLine($"   - {name,-10} ({role}) — {covers}");
    }
}
