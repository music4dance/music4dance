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
    [DataRow("/xmlrpc.php", true)]
    [DataRow("/fling.php?p=", true)]
    [DataRow("/this_is_a_new_hello_world.PHP", true)]
    [DataRow("/uploads/exploit.php/payload.jpg", true)]
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
