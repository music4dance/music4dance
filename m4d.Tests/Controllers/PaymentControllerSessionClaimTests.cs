using m4d.Controllers;

using m4dModels;

using Microsoft.EntityFrameworkCore;

using static m4d.Controllers.PaymentController;

namespace m4d.Tests.Controllers;

/// <summary>
/// A paid checkout session is credited once. The record lives in the database, so it survives a
/// restart and is shared between instances (a fresh context stands in for either here).
/// </summary>
[TestClass]
public class PaymentControllerSessionClaimTests
{
    private static DbContextOptions<DanceMusicContext> NewDatabase() =>
        new DbContextOptionsBuilder<DanceMusicContext>()
            .UseInMemoryDatabase($"checkout-{Guid.NewGuid()}")
            .Options;

    [TestMethod]
    public async Task FirstClaimSucceedsAndIsPersisted()
    {
        var options = NewDatabase();
        await using (var context = new DanceMusicContext(options))
        {
            var claim = await TryClaimSession(context, "cs_test_1", "user-1");
            Assert.IsNotNull(claim);
        }

        await using var fresh = new DanceMusicContext(options);
        var stored = await fresh.CheckoutSessions.SingleAsync();
        Assert.AreEqual("cs_test_1", stored.SessionId);
        Assert.AreEqual("user-1", stored.ApplicationUserId);
    }

    [TestMethod]
    public async Task SecondClaimFromAFreshContextIsADuplicate()
    {
        var options = NewDatabase();
        await using (var context = new DanceMusicContext(options))
        {
            Assert.IsNotNull(await TryClaimSession(context, "cs_test_1", "user-1"));
        }

        // Simulates a reload after an app restart, or a request served by another instance
        await using var restarted = new DanceMusicContext(options);
        Assert.IsNull(await TryClaimSession(restarted, "cs_test_1", "user-1"));
    }

    [TestMethod]
    public async Task DifferentSessionsAreIndependent()
    {
        await using var context = new DanceMusicContext(NewDatabase());

        Assert.IsNotNull(await TryClaimSession(context, "cs_test_1", "user-1"));
        Assert.IsNotNull(await TryClaimSession(context, "cs_test_2", "user-1"));
    }

    [TestMethod]
    public async Task ReleasedSessionCanBeClaimedAgain()
    {
        var options = NewDatabase();
        await using (var context = new DanceMusicContext(options))
        {
            var claim = await TryClaimSession(context, "cs_test_1", "user-1");
            Assert.IsNotNull(claim);
            await ReleaseSession(context, claim);
        }

        await using var retry = new DanceMusicContext(options);
        Assert.IsNotNull(await TryClaimSession(retry, "cs_test_1", "user-1"));
    }
}
