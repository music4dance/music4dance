using m4d.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Security;

[TestClass]
public class AuthenticationTrackerTests
{
    [TestMethod]
    public void RecordAttempt_Success_TracksAttempt()
    {
        // Arrange
        var tracker = new AuthenticationTracker();
        var testUsername = $"testuser_{Guid.NewGuid()}";
        var testIp = $"192.168.{Random.Shared.Next(100, 255)}.{Random.Shared.Next(1, 255)}";

        // Act
        tracker.RecordAttempt(testUsername, testIp, success: true);

        // Assert
        var stats = tracker.GetStats();
        Assert.AreEqual(1, stats.TotalAttempts);
        Assert.AreEqual(1, stats.LastHourAttempts);
        Assert.AreEqual(0, stats.FailedAttempts);
    }

    [TestMethod]
    public void RecordAttempt_Failure_TracksFailedAttempt()
    {
        // Arrange
        var tracker = new AuthenticationTracker();
        var testUsername = $"testuser_{Guid.NewGuid()}";
        var testIp = $"192.168.{Random.Shared.Next(100, 255)}.{Random.Shared.Next(1, 255)}";

        // Act
        tracker.RecordAttempt(testUsername, testIp, success: false, failureReason: "InvalidPassword");

        // Assert
        var stats = tracker.GetStats();
        Assert.AreEqual(1, stats.TotalAttempts);
        Assert.AreEqual(1, stats.FailedAttempts);
        Assert.AreEqual(1, stats.UniqueIPs);
        Assert.AreEqual(1, stats.UniqueUsernames);
    }

    [TestMethod]
    public void RecordAttempt_MultipleFailures_TracksAllAttempts()
    {
        // Arrange
        var tracker = new AuthenticationTracker();
        var user1 = $"user1_{Guid.NewGuid()}";
        var user2 = $"user2_{Guid.NewGuid()}";
        var baseIp = Random.Shared.Next(100, 200);

        // Act
        tracker.RecordAttempt(user1, $"172.16.{baseIp}.1", success: false, failureReason: "InvalidPassword");
        tracker.RecordAttempt(user2, $"172.16.{baseIp}.2", success: false, failureReason: "LockedOut");
        tracker.RecordAttempt(user1, $"172.16.{baseIp}.3", success: false, failureReason: "InvalidPassword");

        // Assert
        var stats = tracker.GetStats();
        Assert.AreEqual(3, stats.TotalAttempts);
        Assert.AreEqual(3, stats.FailedAttempts);
        Assert.AreEqual(3, stats.UniqueIPs);
        Assert.AreEqual(2, stats.UniqueUsernames);
    }

    [TestMethod]
    public void GetFailedAttemptsForIP_ReturnsCorrectCount()
    {
        // Arrange
        var tracker = new AuthenticationTracker();
        var testIp = $"10.0.{Random.Shared.Next(100, 255)}.{Random.Shared.Next(1, 255)}";

        // Act
        tracker.RecordAttempt($"user1_{Guid.NewGuid()}", testIp, success: false);
        tracker.RecordAttempt($"user2_{Guid.NewGuid()}", testIp, success: false);
        tracker.RecordAttempt($"user3_{Guid.NewGuid()}", testIp, success: true);

        // Assert
        var failedCount = tracker.GetFailedAttemptsForIP(testIp, TimeSpan.FromMinutes(5));
        Assert.AreEqual(2, failedCount);
    }

    [TestMethod]
    public void GetStats_TopTargetedUsernames_ReturnsOrderedList()
    {
        // Arrange
        var tracker = new AuthenticationTracker();
        var user1 = $"user1_{Guid.NewGuid()}";
        var user2 = $"user2_{Guid.NewGuid()}";
        var user3 = $"user3_{Guid.NewGuid()}";
        var baseIp = Random.Shared.Next(100, 200);

        // Act - Attack user1 5 times, user2 3 times, user3 once
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordAttempt(user1, $"10.1.{baseIp}.{i}", success: false);
        }
        for (int i = 0; i < 3; i++)
        {
            tracker.RecordAttempt(user2, $"10.2.{baseIp}.{i}", success: false);
        }
        tracker.RecordAttempt(user3, $"10.3.{baseIp}.1", success: false);

        // Assert
        var stats = tracker.GetStats();
        Assert.IsTrue(stats.TopTargetedUsernames.Count >= 3);

        // Find our test users in the results
        var foundUser1 = stats.TopTargetedUsernames.FirstOrDefault(u => u.Username == user1);
        Assert.IsNotNull(foundUser1);
        Assert.AreEqual(5, foundUser1.FailedAttempts);
    }

    [TestMethod]
    public void GetStats_TopAttackingIPs_ReturnsOrderedList()
    {
        // Arrange
        var tracker = new AuthenticationTracker();
        var baseIp = Random.Shared.Next(100, 200);
        var ip1 = $"10.10.{baseIp}.1";
        var ip2 = $"10.10.{baseIp}.2";

        // Act - IP1 attacks 4 times, IP2 attacks 2 times
        for (int i = 0; i < 4; i++)
        {
            tracker.RecordAttempt($"user{Guid.NewGuid()}", ip1, success: false);
        }
        tracker.RecordAttempt($"user{Guid.NewGuid()}", ip2, success: false);
        tracker.RecordAttempt($"user{Guid.NewGuid()}", ip2, success: false);

        // Assert
        var stats = tracker.GetStats();

        // Find our test IPs
        var foundIp1 = stats.TopAttackingIPs.FirstOrDefault(ip => ip.IpAddress == ip1);
        Assert.IsNotNull(foundIp1);
        Assert.AreEqual(4, foundIp1.FailedAttempts);
    }

    [TestMethod]
    public void RecordAttempt_WithNullValues_HandlesGracefully()
    {
        // Arrange
        var tracker = new AuthenticationTracker();

        // Act
        tracker.RecordAttempt(null, null, success: false);

        // Assert
        var stats = tracker.GetStats();
        Assert.IsTrue(stats.TotalAttempts >= 1);
        Assert.IsTrue(stats.FailedAttempts >= 1);
    }
}
