using m4d.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Security;

[TestClass]
public class Http4xxTrackerTests
{
    [TestMethod]
    public void RecordEvent_SingleEvent_TracksCorrectly()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        var testUrl = $"/song/details/{Guid.NewGuid()}";

        // Act
        tracker.RecordEvent(testUrl, 404);

        // Assert
        var stats = tracker.GetStats();
        Assert.AreEqual(1, stats.TotalEventsTracked);
        Assert.AreEqual(1, stats.LastHourCount);
    }

    [TestMethod]
    public void RecordEvent_RepeatedSameUrlAndStatus_AggregatesCount()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        var testUrl = $"/song/details/{Guid.NewGuid()}";

        // Act
        tracker.RecordEvent(testUrl, 404);
        tracker.RecordEvent(testUrl, 404);
        tracker.RecordEvent(testUrl, 404);

        // Assert
        var stats = tracker.GetStats();
        var found = stats.TopUrls.FirstOrDefault(u => u.Url == testUrl);
        Assert.IsNotNull(found);
        Assert.AreEqual(404, found.StatusCode);
        Assert.AreEqual(3, found.Count);
    }

    [TestMethod]
    public void RecordEvent_SameUrlDifferentStatusCodes_TracksSeparately()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        var testUrl = $"/customsearch/{Guid.NewGuid()}";

        // Act
        tracker.RecordEvent(testUrl, 404);
        tracker.RecordEvent(testUrl, 400);

        // Assert
        var stats = tracker.GetStats();
        var entries = stats.TopUrls.Where(u => u.Url == testUrl).ToList();
        Assert.AreEqual(2, entries.Count);
        Assert.IsTrue(entries.Any(e => e.StatusCode == 404 && e.Count == 1));
        Assert.IsTrue(entries.Any(e => e.StatusCode == 400 && e.Count == 1));
    }

    [TestMethod]
    public void GetStats_TopUrls_OrderedByCountDescending()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        var popularUrl = $"/song/details/{Guid.NewGuid()}";
        var rareUrl = $"/song/details/{Guid.NewGuid()}";

        // Act - popularUrl hit 5 times, rareUrl hit 2 times
        for (var i = 0; i < 5; i++)
        {
            tracker.RecordEvent(popularUrl, 404);
        }
        for (var i = 0; i < 2; i++)
        {
            tracker.RecordEvent(rareUrl, 404);
        }

        // Assert
        var stats = tracker.GetStats();
        var popularIndex = stats.TopUrls.FindIndex(u => u.Url == popularUrl);
        var rareIndex = stats.TopUrls.FindIndex(u => u.Url == rareUrl);
        Assert.IsTrue(popularIndex >= 0 && rareIndex >= 0);
        Assert.IsTrue(popularIndex < rareIndex, "More frequently hit URL should be ordered first");
    }

    [TestMethod]
    public void GetStats_RespectsTopNLimit()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        for (var i = 0; i < 10; i++)
        {
            tracker.RecordEvent($"/bad-link-{i}-{Guid.NewGuid()}", 404);
        }

        // Act
        var stats = tracker.GetStats(topN: 3);

        // Assert
        Assert.AreEqual(3, stats.TopUrls.Count);
    }

    [TestMethod]
    public void GetStats_EmptyTracker_ReturnsEmptyStats()
    {
        // Arrange
        var tracker = new Http4xxTracker();

        // Act
        var stats = tracker.GetStats();

        // Assert
        Assert.AreEqual(0, stats.TotalEventsTracked);
        Assert.AreEqual(0, stats.TopUrls.Count);
    }

    [TestMethod]
    public void RecordEvent_WithNullUrl_HandlesGracefully()
    {
        // Arrange
        var tracker = new Http4xxTracker();

        // Act
        tracker.RecordEvent(null!, 404);

        // Assert
        var stats = tracker.GetStats();
        Assert.AreEqual(1, stats.TotalEventsTracked);
        Assert.AreEqual("/", stats.TopUrls[0].Url);
    }

    [TestMethod]
    public void GetStats_DefaultTopN_Is100()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        for (var i = 0; i < 150; i++)
        {
            tracker.RecordEvent($"/bad-link-{i}-{Guid.NewGuid()}", 404);
        }

        // Act
        var stats = tracker.GetStats();

        // Assert
        Assert.AreEqual(100, stats.TopUrls.Count);
    }

    [TestMethod]
    public void GetStats_TopNNull_ReturnsEveryDistinctUrl()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        for (var i = 0; i < 150; i++)
        {
            tracker.RecordEvent($"/bad-link-{i}-{Guid.NewGuid()}", 404);
        }

        // Act
        var stats = tracker.GetStats(null);

        // Assert
        Assert.AreEqual(150, stats.TopUrls.Count);
    }

    [TestMethod]
    [DataRow("/wp-login.php", true)]
    [DataRow("/wp-admin/", true)]
    [DataRow("/wp/wp-json/batch/v1", true)]
    [DataRow("/administrator/", true)]
    [DataRow("/administrator", true)]
    [DataRow("/.env", true)]
    [DataRow("/admin/.env", true)]
    [DataRow("/backend/.env", true)]
    [DataRow("/.git/config", true)]
    [DataRow("/.git-credentials", true)]
    [DataRow("/.aws/credentials", true)]
    [DataRow("/.npmrc", true)]
    [DataRow("/.s3cfg", true)]
    [DataRow("/.boto", true)]
    [DataRow("/proc/self/environ", true)]
    [DataRow("/api/proc/self/cmdline", true)]
    [DataRow("/fckeditor/editor/filemanager/browser/default/browser.html", true)]
    [DataRow("/admin/editor/filemanager/browser/default/browser.html", true)]
    [DataRow("/var/run/secrets/kubernetes.io/serviceaccount/token", true)]
    [DataRow("/graphql", true)]
    [DataRow("/api/graphql", true)]
    [DataRow("/service-account.json", true)]
    [DataRow("/firebase-adminsdk.json", true)]
    [DataRow("/credentials.json", true)]
    [DataRow("/terraform.tfstate", true)]
    [DataRow("/xmlrpc.php", true)]
    [DataRow("/fling.php?p=", true)]
    [DataRow("/this_is_a_new_hello_world.PHP", true)]
    [DataRow("/uploads/exploit.php/payload.jpg", true)]
    // Added from the 2026-09-19 4xx triage pass - see architecture/distributed-attack-mitigation.md
    [DataRow("/secrets.env", true)]
    [DataRow("/config/env/aws_credentials.env", true)]
    [DataRow("/.ssh/id_rsa", true)]
    [DataRow("/.ssh/authorized_keys", true)]
    [DataRow("/ssl/server.key", true)]
    [DataRow("/key.pem", true)]
    [DataRow("/docker-compose.yml", true)]
    [DataRow("/Dockerfile", true)]
    [DataRow("/.dockerenv", true)]
    [DataRow("/appsettings.Production.json", true)]
    [DataRow("/local.settings.json", true)]
    [DataRow("/application.properties", true)]
    [DataRow("/serverless.yaml", true)]
    [DataRow("/.svn/entries", true)]
    [DataRow("/.vscode/sftp.json", true)]
    [DataRow("/.idea/WebServers.xml", true)]
    [DataRow("/.gitlab-ci.yml", true)]
    [DataRow("/.claude/settings.local.json", true)]
    [DataRow("/.htpasswd", true)]
    [DataRow("/.bashrc", true)]
    [DataRow("/rclone.conf", true)]
    [DataRow("/dump.sql.gz", true)]
    [DataRow("/db.tar.gz", true)]
    [DataRow("/storage/logs/laravel.log", true)]
    [DataRow("/serviceAccountKey.json", true)]
    [DataRow("/__/firebase/init.json", true)]
    [DataRow("/wordpress/", true)]
    [DataRow("/WP", true)]
    [DataRow("/phpinfo", true)]
    [DataRow("/cgi-bin/authLogin.cgi", true)]
    [DataRow("/admin/assets/plugins/jquery-file-upload/server/php/", true)]
    [DataRow("/userfiles?path=../../../.env", true)]
    [DataRow("/__vite_rsc_findSourceMapURL?filename=file:///app/.env&environmentName=rsc", true)]
    [DataRow("/Telerik.Web.UI.WebResource.axd?type=rau", true)]
    [DataRow("/sitecore/shell/sitecore.version.xml", true)]
    [DataRow("/modules/mod_webshell/", true)]
    [DataRow("/blocks/rce/lang/en/", true)]
    [DataRow("/admin/controller/extension/extension/", true)]
    [DataRow("/z9x8c7v6b5-debug-trigger-music4dance.net", true)]
    [DataRow("/api/fs/exec", true)]
    [DataRow("/.vite/manifest.json", true)]
    // Benign platform probes and real app URLs must stay visible in the export
    [DataRow("/sitemap.xml", false)]
    [DataRow("/favicon.ico", false)]
    [DataRow("/apple-touch-icon.png", false)]
    [DataRow("/.well-known/security.txt", false)]
    [DataRow("/.well-known/assetlinks.json", false)]
    [DataRow("/manifest.json", false)]
    [DataRow("/login", false)]
    [DataRow("/account/login", false)]
    [DataRow("/mcp", false)]
    [DataRow("/song/album", false)]
    [DataRow("/song/home/privacypolicy", false)]
    [DataRow("/api/song/?filter=v2-index---Everywhere", false)]
    [DataRow("/vclient/assets/SongCore-BB83Kvsj.js", false)]
    [DataRow("/song/artist", false)]
    [DataRow("/api/usagelog/batch", false)]
    [DataRow("/css/site.css", false)]
    [DataRow("", false)]
    public void IsKnownAttackUrl_ClassifiesKnownPatterns(string url, bool expected)
    {
        Assert.AreEqual(expected, Http4xxTracker.IsKnownAttackUrl(url));
    }

    [TestMethod]
    public void GetStats_ExcludeKnownAttacks_FiltersOutAttackUrls()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        tracker.RecordEvent("/wp-login.php", 404);
        tracker.RecordEvent("/song/artist", 404);

        // Act
        var stats = tracker.GetStats(filter: Http4xxUrlFilter.ExcludeKnownAttacks);

        // Assert
        Assert.AreEqual(1, stats.TopUrls.Count);
        Assert.AreEqual("/song/artist", stats.TopUrls[0].Url);
    }

    [TestMethod]
    public void GetStats_KnownAttacksOnly_ReturnsOnlyAttackUrls()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        tracker.RecordEvent("/wp-login.php", 404);
        tracker.RecordEvent("/song/artist", 404);

        // Act
        var stats = tracker.GetStats(filter: Http4xxUrlFilter.KnownAttacksOnly);

        // Assert
        Assert.AreEqual(1, stats.TopUrls.Count);
        Assert.AreEqual("/wp-login.php", stats.TopUrls[0].Url);
        Assert.IsTrue(stats.TopUrls[0].IsKnownAttack);
    }
}
