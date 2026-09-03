using m4d.Services.ServiceHealth;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Services;

// Exercises the cooldown added to ServiceHealthManager.IsServiceHealthy: nothing in production
// ever calls MarkHealthy for most services (e.g. SearchService - see architecture/admin-pages.md),
// so without a cooldown a single transient failure (a brief Azure Search throttling spike, say)
// would wedge the whole app in degraded mode until the process restarts, long after the
// underlying service recovered on its own.
[TestClass]
public class ServiceHealthManagerTests
{
    private const string ServiceName = "SearchService";

    private static ServiceHealthManager CreateManager(TimeSpan cooldown)
    {
        var manager = new ServiceHealthManager(NullLogger<ServiceHealthManager>.Instance)
        {
            UnavailableCooldown = cooldown
        };
        return manager;
    }

    [TestMethod]
    public void IsServiceHealthy_UnknownService_ReturnsTrue()
    {
        var manager = CreateManager(TimeSpan.FromMinutes(1));

        Assert.IsTrue(manager.IsServiceHealthy(ServiceName));
    }

    [TestMethod]
    public void IsServiceHealthy_MarkedUnavailable_ReturnsFalseBeforeCooldownElapses()
    {
        var manager = CreateManager(TimeSpan.FromMinutes(1));
        manager.MarkUnavailable(ServiceName, "throttled");

        Assert.IsFalse(manager.IsServiceHealthy(ServiceName));
    }

    [TestMethod]
    public async Task IsServiceHealthy_MarkedUnavailable_ReturnsTrueAfterCooldownElapses()
    {
        var manager = CreateManager(TimeSpan.FromMilliseconds(100));
        manager.MarkUnavailable(ServiceName, "throttled");

        await Task.Delay(150);

        Assert.IsTrue(manager.IsServiceHealthy(ServiceName));
    }

    [TestMethod]
    public async Task IsServiceHealthy_RenewedFailure_RestartsTheCooldown()
    {
        var manager = CreateManager(TimeSpan.FromMilliseconds(100));
        manager.MarkUnavailable(ServiceName, "throttled");

        await Task.Delay(60);
        Assert.IsFalse(manager.IsServiceHealthy(ServiceName), "Still within the first cooldown window");

        // Simulates an optimistic retry (after the cooldown would otherwise have let one
        // through) failing again - MarkUnavailable should reset the clock.
        manager.MarkUnavailable(ServiceName, "still throttled");

        await Task.Delay(60);
        Assert.IsFalse(
            manager.IsServiceHealthy(ServiceName),
            "120ms have passed since the first failure (over the 100ms cooldown), but only " +
            "60ms since the renewed failure, so the cooldown should not have elapsed yet");
    }

    [TestMethod]
    public void IsServiceHealthy_MarkedHealthyAfterFailure_ReturnsTrueImmediately()
    {
        var manager = CreateManager(TimeSpan.FromMinutes(1));
        manager.MarkUnavailable(ServiceName, "throttled");
        manager.MarkHealthy(ServiceName);

        Assert.IsTrue(manager.IsServiceHealthy(ServiceName));
    }
}
