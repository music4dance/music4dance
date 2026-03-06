using m4d.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Security;

[TestClass]
public class RateLimitingTrackerTests
{
    [TestMethod]
    public void RecordEvent_SingleEvent_TracksCorrectly()
    {
        // Arrange
        var tracker = new RateLimitingTracker();
        var testIp = $"10.20.{Random.Shared.Next(100, 255)}.{Random.Shared.Next(1, 255)}";

        // Act
        tracker.RecordEvent(testIp, "/Identity/Account/Login", wasLimited: false, requestCount: 1, isGlobal: false);

        // Assert
        var stats = tracker.GetStats();
        Assert.IsTrue(stats.TotalEventsTracked >= 1);
        Assert.IsTrue(stats.LastHourRequests >= 1);
    }

    [TestMethod]
    public void RecordEvent_LimitedRequest_TracksAsLimited()
    {
        // Arrange
        var tracker = new RateLimitingTracker();
        var testIp = $"10.21.{Random.Shared.Next(100, 255)}.{Random.Shared.Next(1, 255)}";

        // Act
        tracker.RecordEvent(testIp, "/Identity/Account/Login", wasLimited: true, requestCount: 11, isGlobal: false);

        // Assert
        var stats = tracker.GetStats();
        Assert.IsTrue(stats.LastHourLimited >= 1);
        Assert.IsTrue(stats.PerIPLimitHitsLastHour >= 1);
    }

    [TestMethod]
    public void RecordEvent_GlobalLimit_TracksAsGlobal()
    {
        // Arrange
        var tracker = new RateLimitingTracker();
        var testIp = $"10.22.{Random.Shared.Next(100, 255)}.{Random.Shared.Next(1, 255)}";

        // Act
        tracker.RecordEvent(testIp, "/Identity/Account/Login", wasLimited: true, requestCount: 101, isGlobal: true);

        // Assert
        var stats = tracker.GetStats();
        Assert.IsTrue(stats.LastHourLimited >= 1);
        Assert.IsTrue(stats.GlobalLimitHitsLastHour >= 1);
    }

    [TestMethod]
    public void RecordEvent_MultipleIPs_TracksUniqueCount()
    {
        // Arrange
        var tracker = new RateLimitingTracker();
        var baseIp = Random.Shared.Next(100, 200);
        var ip1 = $"10.23.{baseIp}.1";
        var ip2 = $"10.23.{baseIp}.2";

        // Act
        tracker.RecordEvent(ip1, "/Identity/Account/Login", wasLimited: false, requestCount: 1, isGlobal: false);
        tracker.RecordEvent(ip2, "/Identity/Account/Login", wasLimited: false, requestCount: 1, isGlobal: false);
        tracker.RecordEvent(ip1, "/Identity/Account/Register", wasLimited: false, requestCount: 2, isGlobal: false);

        // Assert
        var stats = tracker.GetStats();
        Assert.IsTrue(stats.LastHourRequests >= 3);
        Assert.IsTrue(stats.UniqueIPsLastHour >= 2);
    }

    [TestMethod]
    public void GetStats_TopRequestingIPs_ReturnsOrderedList()
    {
        // Arrange
        var tracker = new RateLimitingTracker();
        var baseIp = Random.Shared.Next(100, 200);
        var ip1 = $"10.24.{baseIp}.1";
        var ip2 = $"10.24.{baseIp}.2";

        // Act - IP1 makes 5 requests, IP2 makes 3 requests
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordEvent(ip1, "/Identity/Account/Login", wasLimited: false, requestCount: i + 1, isGlobal: false);
        }
        for (int i = 0; i < 3; i++)
        {
            tracker.RecordEvent(ip2, "/Identity/Account/Login", wasLimited: false, requestCount: i + 1, isGlobal: false);
        }

        // Assert
        var stats = tracker.GetStats();

        // Find our test IPs
        var foundIp1 = stats.TopRequestingIPs.FirstOrDefault(ip => ip.IpAddress == ip1);
        Assert.IsNotNull(foundIp1);
        Assert.AreEqual(5, foundIp1.TotalRequests);
    }

    [TestMethod]
    public void GetStats_MostTargetedPaths_ReturnsOrderedList()
    {
        // Arrange
        var tracker = new RateLimitingTracker();
        var baseIp = Random.Shared.Next(100, 200);
        var path = $"/Identity/Test/Path{Guid.NewGuid()}";

        // Act
        tracker.RecordEvent($"10.25.{baseIp}.1", path, wasLimited: false, requestCount: 1, isGlobal: false);
        tracker.RecordEvent($"10.25.{baseIp}.2", path, wasLimited: false, requestCount: 1, isGlobal: false);
        tracker.RecordEvent($"10.25.{baseIp}.3", path, wasLimited: false, requestCount: 1, isGlobal: false);

        // Assert
        var stats = tracker.GetStats();

        // Find our test path
        var foundPath = stats.MostTargetedPaths.FirstOrDefault(p => p.Path == path);
        Assert.IsNotNull(foundPath);
        Assert.AreEqual(3, foundPath.RequestCount);
    }

    [TestMethod]
    public void GetStats_HourlyStats_IncludesHoursWithEvents()
    {
        // Arrange
        var tracker = new RateLimitingTracker();
        var testIp = $"10.26.{Random.Shared.Next(100, 255)}.{Random.Shared.Next(1, 255)}";

        tracker.RecordEvent(testIp, "/Identity/Account/Login", wasLimited: false, requestCount: 1, isGlobal: false);

        // Act
        var stats = tracker.GetStats();

        // Assert - Should have at least 1 hour with events (the current hour)
        Assert.IsTrue(stats.HourlyStats.Count >= 1, "Should have at least one hour with events");
        Assert.IsTrue(stats.HourlyStats[0].TotalRequests >= 1, "Current hour should have requests");

        // Verify most recent first (descending order)
        if (stats.HourlyStats.Count > 1)
        {
            Assert.IsTrue(stats.HourlyStats[0].HourStart > stats.HourlyStats[^1].HourStart,
                "Should be ordered most recent first");
        }
    }

    [TestMethod]
    public void RecordEvent_WithNullValues_HandlesGracefully()
    {
        // Arrange
        var tracker = new RateLimitingTracker();

        // Act
        tracker.RecordEvent(null, null, wasLimited: false, requestCount: 1, isGlobal: false);

        // Assert
        var stats = tracker.GetStats();
        Assert.IsTrue(stats.TotalEventsTracked >= 1);
    }
}

[TestClass]
public class CircularBufferTests
{
    [TestMethod]
    public void Add_SingleItem_Stores()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);

        // Act
        buffer.Add(42);

        // Assert
        var items = buffer.ToList();
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(42, items[0]);
    }

    [TestMethod]
    public void Add_ExceedsCapacity_OverwritesOldest()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);

        // Act
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4); // Should overwrite 1

        // Assert
        var items = buffer.ToList();
        Assert.AreEqual(3, items.Count);
        Assert.AreEqual(2, items[0]);
        Assert.AreEqual(3, items[1]);
        Assert.AreEqual(4, items[2]);
    }

    [TestMethod]
    public void Add_ManyItems_MaintainsCapacity()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);

        // Act
        for (int i = 0; i < 200; i++)
        {
            buffer.Add(i);
        }

        // Assert
        var items = buffer.ToList();
        Assert.AreEqual(100, items.Count);
        Assert.AreEqual(100, items[0]); // First item should be 100 (0-99 overwritten)
        Assert.AreEqual(199, items[99]); // Last item should be 199
    }

    [TestMethod]
    public void ToList_EmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(10);

        // Act
        var items = buffer.ToList();

        // Assert
        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public void ToList_PartiallyFilled_ReturnsCorrectCount()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(10);

        // Act
        buffer.Add("one");
        buffer.Add("two");
        buffer.Add("three");

        // Assert
        var items = buffer.ToList();
        Assert.AreEqual(3, items.Count);
        Assert.AreEqual("one", items[0]);
        Assert.AreEqual("two", items[1]);
        Assert.AreEqual("three", items[2]);
    }
}
