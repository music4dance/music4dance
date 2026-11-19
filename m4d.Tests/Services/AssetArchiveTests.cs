#nullable disable

using m4d.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace m4d.Tests.Services;

[TestClass]
public class AssetArchiveTests
{
    internal sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private string _root;
    private string _webAssets;
    private string _archiveRoot;
    private TestClock _clock;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"asset-archive-{Guid.NewGuid():N}");
        _webAssets = Path.Combine(_root, "wwwroot", "vclient", "assets");
        _archiveRoot = Path.Combine(_root, "data", AssetArchive.FolderName);
        _clock = new TestClock(DateTimeOffset.UtcNow);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private AssetArchive CreateArchive(int keepBuilds = 3, double minRetentionHours = 48) =>
        new(_archiveRoot, _webAssets,
            new AssetArchiveOptions { KeepBuilds = keepBuilds, MinRetention = TimeSpan.FromHours(minRetentionHours) },
            _clock, NullLogger<AssetArchive>.Instance);

    // Replaces the "deployed" wwwroot bundles with these files, as a new package would
    private void Deploy(params string[] files)
    {
        if (Directory.Exists(_webAssets))
        {
            Directory.Delete(_webAssets, recursive: true);
        }

        _ = Directory.CreateDirectory(_webAssets);
        foreach (var file in files)
        {
            File.WriteAllText(Path.Combine(_webAssets, file), $"content of {file}");
        }
    }

    private string[] ArchivedBuilds() =>
        [.. Directory.GetDirectories(_archiveRoot).Select(Path.GetFileName).Where(n => !n.StartsWith('.')).Order()];

    // Deploys a build with one bundle, archives it, and moves the clock on
    private void DeployAndArchive(AssetArchive archive, string bundle, TimeSpan advance)
    {
        Deploy(bundle);
        archive.ArchiveCurrent();
        _clock.Now += advance;
    }

    [TestMethod]
    public void ArchiveCurrent_CopiesBundlesIntoAFolderNamedForTheBuild()
    {
        Deploy("index-AAAA1111.js", "style-BBBB2222.css");

        CreateArchive().ArchiveCurrent();

        var id = AssetArchive.BuildId(["index-AAAA1111.js", "style-BBBB2222.css"]);
        CollectionAssert.AreEqual(new[] { id }, ArchivedBuilds());
        Assert.AreEqual("content of index-AAAA1111.js",
            File.ReadAllText(Path.Combine(_archiveRoot, id, "index-AAAA1111.js")));
        Assert.IsTrue(File.Exists(Path.Combine(_archiveRoot, id, AssetArchive.MarkerFile)));
    }

    [TestMethod]
    public void ArchiveCurrent_SameBuildAgain_ReusesItsFolder()
    {
        var archive = CreateArchive();
        Deploy("index-AAAA1111.js");

        archive.ArchiveCurrent();
        archive.ArchiveCurrent();

        Assert.AreEqual(1, ArchivedBuilds().Length);
    }

    [TestMethod]
    public void ArchiveCurrent_NoBundles_DoesNothing()
    {
        CreateArchive().ArchiveCurrent();

        Assert.IsFalse(Directory.Exists(_archiveRoot));
    }

    [TestMethod]
    public void Find_ReturnsAnEarlierBuildsBundle()
    {
        var archive = CreateArchive();
        DeployAndArchive(archive, "old-AAAA1111.js", TimeSpan.FromMinutes(5));
        DeployAndArchive(archive, "new-BBBB2222.js", TimeSpan.Zero);

        var found = archive.Find("old-AAAA1111.js");

        Assert.IsNotNull(found);
        Assert.AreEqual("content of old-AAAA1111.js", File.ReadAllText(found));
    }

    [TestMethod]
    [DataRow("missing-CCCC3333.js")]
    [DataRow("../old-AAAA1111.js")]
    [DataRow("sub/old-AAAA1111.js")]
    [DataRow(".archived")]
    [DataRow("")]
    [DataRow(null)]
    public void Find_UnknownOrUnsafeName_ReturnsNull(string name)
    {
        var archive = CreateArchive();
        DeployAndArchive(archive, "old-AAAA1111.js", TimeSpan.Zero);

        Assert.IsNull(archive.Find(name));
    }

    [TestMethod]
    public void Find_IgnoresAFolderWithoutAMarker()
    {
        // Another instance is still copying into it, or it's damaged
        var partial = Path.Combine(_archiveRoot, "0123456789abcdef");
        _ = Directory.CreateDirectory(partial);
        File.WriteAllText(Path.Combine(partial, "half-AAAA1111.js"), "partial");

        Assert.IsNull(CreateArchive().Find("half-AAAA1111.js"));
    }

    [TestMethod]
    public void Prune_RapidRedeploys_KeepsTheNewestBuildsAndAnyReplacedRecently()
    {
        // Five deploys an hour apart: everything replaced within the last 48 hours stays
        var archive = CreateArchive();
        foreach (var i in Enumerable.Range(1, 5))
        {
            DeployAndArchive(archive, $"build{i}-AAAA1111.js", TimeSpan.FromHours(1));
        }

        Assert.AreEqual(5, ArchivedBuilds().Length);
        Assert.IsNotNull(archive.Find("build1-AAAA1111.js"));
    }

    [TestMethod]
    public void Prune_RemovesOldBuildsOnceTheirSuccessorIsPastMinRetention()
    {
        var archive = CreateArchive();
        foreach (var i in Enumerable.Range(1, 5))
        {
            DeployAndArchive(archive, $"build{i}-AAAA1111.js", TimeSpan.FromDays(3));
        }

        Assert.AreEqual(3, ArchivedBuilds().Length);
        Assert.IsNull(archive.Find("build1-AAAA1111.js"));
        Assert.IsNull(archive.Find("build2-AAAA1111.js"));
        Assert.IsNotNull(archive.Find("build3-AAAA1111.js"));
        Assert.IsNotNull(archive.Find("build5-AAAA1111.js"));
    }

    [TestMethod]
    public void Prune_KeepsABuildWhoseSuccessorWasArchivedRecently()
    {
        // build1 serves for ten days, then three deploys in quick succession replace it. Pages
        // cached from build1 can still be out there, so it outlives the "newest three" rule.
        var archive = CreateArchive();
        DeployAndArchive(archive, "build1-AAAA1111.js", TimeSpan.FromDays(10));
        DeployAndArchive(archive, "build2-AAAA1111.js", TimeSpan.FromHours(1));
        DeployAndArchive(archive, "build3-AAAA1111.js", TimeSpan.FromHours(1));
        DeployAndArchive(archive, "build4-AAAA1111.js", TimeSpan.FromDays(3));

        Assert.IsNotNull(archive.Find("build1-AAAA1111.js"));

        // Three days on, build1 and build2 have both been replaced for longer than 48 hours
        DeployAndArchive(archive, "build5-AAAA1111.js", TimeSpan.Zero);

        Assert.IsNull(archive.Find("build1-AAAA1111.js"));
        Assert.IsNull(archive.Find("build2-AAAA1111.js"));
        Assert.AreEqual(3, ArchivedBuilds().Length);
    }

    [TestMethod]
    public void Prune_RedeployingAnOlderBuild_MakesItTheNewest()
    {
        var archive = CreateArchive(keepBuilds: 1, minRetentionHours: 0);
        DeployAndArchive(archive, "build1-AAAA1111.js", TimeSpan.FromHours(1));
        DeployAndArchive(archive, "build2-AAAA1111.js", TimeSpan.FromHours(1));

        // Roll back to build1: it's archived again (restored) and build2 is the one pruned
        DeployAndArchive(archive, "build1-AAAA1111.js", TimeSpan.Zero);

        Assert.IsNotNull(archive.Find("build1-AAAA1111.js"));
        Assert.IsNull(archive.Find("build2-AAAA1111.js"));
    }

    [TestMethod]
    public void Prune_RemovesAbandonedTemporaryFolders()
    {
        var abandoned = Path.Combine(_archiveRoot, ".tmp-abandoned");
        _ = Directory.CreateDirectory(abandoned);
        _clock.Now = DateTimeOffset.UtcNow.AddHours(2);
        Deploy("index-AAAA1111.js");

        CreateArchive().ArchiveCurrent();

        Assert.IsFalse(Directory.Exists(abandoned));
    }

    [TestMethod]
    public void BuildId_DependsOnlyOnTheNames()
    {
        Assert.AreEqual(AssetArchive.BuildId(["a.js", "b.js"]), AssetArchive.BuildId(["a.js", "b.js"]));
        Assert.AreNotEqual(AssetArchive.BuildId(["a.js", "b.js"]), AssetArchive.BuildId(["a.js", "c.js"]));
        Assert.AreEqual(16, AssetArchive.BuildId(["a.js"]).Length);
    }

    [TestMethod]
    public void ArchiveCurrent_PublishesTheBuildWithItsMarker()
    {
        Deploy("index-AAAA1111.js");

        CreateArchive().ArchiveCurrent();

        var build = Path.Combine(_archiveRoot, ArchivedBuilds().Single());
        Assert.IsTrue(File.Exists(Path.Combine(build, AssetArchive.MarkerFile)));
        // No marker temp files left behind
        Assert.AreEqual(2, Directory.GetFiles(build).Length);
    }

    [TestMethod]
    public void Find_SeesAnotherInstancesBuild_OnceTheIndexExpires()
    {
        var archive = CreateArchive();
        DeployAndArchive(archive, "build1-AAAA1111.js", TimeSpan.Zero);
        Assert.IsNull(archive.Find("build2-AAAA1111.js")); // builds the index

        // A second instance, sharing the archive, starts a new build
        DeployAndArchive(CreateArchive(), "build2-AAAA1111.js", TimeSpan.Zero);

        Assert.IsNull(archive.Find("build2-AAAA1111.js"), "answered from the index, not storage");
        _clock.Now += AssetArchive.IndexLifetime;
        Assert.IsNotNull(archive.Find("build2-AAAA1111.js"));
    }

    [TestMethod]
    public void Prune_RemovesABuildFolderThatNeverGotAMarker_OnceItIsStale()
    {
        var markerless = Path.Combine(_archiveRoot, "0123456789abcdef");
        _ = Directory.CreateDirectory(markerless);
        Deploy("index-AAAA1111.js");

        // Younger than an hour: it may still be in progress
        CreateArchive().ArchiveCurrent();
        Assert.IsTrue(Directory.Exists(markerless));

        _clock.Now = DateTimeOffset.UtcNow.AddHours(2);
        CreateArchive().ArchiveCurrent();
        Assert.IsFalse(Directory.Exists(markerless));
        Assert.AreEqual(1, ArchivedBuilds().Length);
    }
}
