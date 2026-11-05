using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace m4dApi.Tests;

[TestClass]
public class PartnerGateTests
{
    private static HttpRequest Request(string authorization, string query = null)
    {
        var context = new DefaultHttpContext();
        if (authorization != null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        if (query != null)
        {
            context.Request.QueryString = new QueryString(query);
        }

        return context.Request;
    }

    // Completes at once whenever the keys are loaded
    private static PartnerGate.Admission Admit(FakePartners partners, HttpRequest request) =>
        partners.Gate.Admit(request).GetAwaiter().GetResult();

    private static int Status(PartnerGate.Admission admission) =>
        ((ObjectResult)admission.Rejection).StatusCode ?? 0;

    [TestMethod]
    public void Admit_AcceptsAValidKey()
    {
        var partners = new FakePartners();

        var admission = Admit(partners, Request($"Bearer {partners.Key}"));

        Assert.IsTrue(admission.Admitted);
        Assert.AreEqual(FakePartners.ClientId, admission.ClientId);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Basic dXNlcjpwYXNz")]
    public void Admit_Returns401WithAChallengeWhenTheKeyIsMissing(string authorization)
    {
        var partners = new FakePartners();
        var request = Request(authorization);

        var admission = Admit(partners, request);

        Assert.AreEqual(StatusCodes.Status401Unauthorized, Status(admission));
        Assert.AreEqual("Bearer", request.HttpContext.Response.Headers.WWWAuthenticate.ToString());
    }

    [TestMethod]
    public void Admit_Returns401ForAnUnknownKey()
    {
        var partners = new FakePartners();
        var request = Request($"Bearer {PartnerKeyFormat.Generate(PartnerKeyFormat.TestPrefix, out _)}");

        var admission = Admit(partners, request);

        Assert.AreEqual(StatusCodes.Status401Unauthorized, Status(admission));
        StringAssert.Contains(request.HttpContext.Response.Headers.WWWAuthenticate.ToString(), "invalid_token");
    }

    [TestMethod]
    [DataRow("?key=m4d_live_7KQ2M9XA4PZT_x")]
    [DataRow("?anything=M4D_TEST_abc")]
    [DataRow("?m4d_live_7KQ2M9XA4PZT_x")]
    public void Admit_Returns400ForAKeyInTheUrl(string query)
    {
        var partners = new FakePartners();

        // Even with a valid header, a key-shaped value in the URL is refused
        var admission = Admit(partners, Request($"Bearer {partners.Key}", query));

        Assert.AreEqual(StatusCodes.Status400BadRequest, Status(admission));
    }

    [TestMethod]
    public async Task Admit_Returns503WithRetryAfterWhenKeysNeverLoad()
    {
        var partners = new FakePartners(load: false);
        var request = Request($"Bearer {partners.Key}");

        var pending = partners.Gate.Admit(request);
        Assert.IsFalse(pending.IsCompleted);
        partners.Time.Advance(PartnerGate.FirstLoadWait);
        var admission = await pending;

        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, Status(admission));
        Assert.AreEqual("30", request.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    [TestMethod]
    public async Task Admit_WaitsForTheFirstLoadOnAColdInstance()
    {
        var partners = new FakePartners(load: false);

        var pending = partners.Gate.Admit(Request($"Bearer {partners.Key}"));
        Assert.IsFalse(pending.IsCompleted);
        await partners.Store.Refresh(CancellationToken.None);

        Assert.IsTrue((await pending).Admitted);
    }

    [TestMethod]
    public void Admit_Returns403ForADisabledClient()
    {
        var partners = new FakePartners(new ClientConfig("Partner", Status: ClientConfig.Disabled));

        Assert.AreEqual(StatusCodes.Status403Forbidden, Status(Admit(partners, Request($"Bearer {partners.Key}"))));
    }

    [TestMethod]
    public void Admit_RateLimitsWith429AndRetryAfter()
    {
        var partners = new FakePartners(new ClientConfig("Partner", RatePerSecond: 0.5, Burst: 2));

        Assert.IsTrue(Admit(partners, Request($"Bearer {partners.Key}")).Admitted);
        Assert.IsTrue(Admit(partners, Request($"Bearer {partners.Key}")).Admitted);
        var request = Request($"Bearer {partners.Key}");
        var admission = Admit(partners, request);

        Assert.AreEqual(StatusCodes.Status429TooManyRequests, Status(admission));
        Assert.AreEqual("2", request.HttpContext.Response.Headers.RetryAfter.ToString());

        partners.Time.Advance(TimeSpan.FromSeconds(2));
        Assert.IsTrue(Admit(partners, Request($"Bearer {partners.Key}")).Admitted);
    }

    [TestMethod]
    public void Admit_UsesTheDefaultsForLimitsTheClientLeavesOut()
    {
        var partners = new FakePartners(defaults: new PartnerDefaults(RatePerSecond: 1, Burst: 1));

        Assert.IsTrue(Admit(partners, Request($"Bearer {partners.Key}")).Admitted);
        Assert.AreEqual(StatusCodes.Status429TooManyRequests, Status(Admit(partners, Request($"Bearer {partners.Key}"))));
    }

    [TestMethod]
    public void Admit_StopsAtTheDailyCapUntilUtcMidnight()
    {
        var partners = new FakePartners(new ClientConfig("Partner", DailyCap: 2));
        partners.Usage.Record(FakePartners.ClientId, "exact");
        partners.Usage.Record(FakePartners.ClientId, "fuzzy");
        var request = Request($"Bearer {partners.Key}");

        var admission = Admit(partners, request);

        Assert.AreEqual(StatusCodes.Status429TooManyRequests, Status(admission));
        // The fake clock is 20:00 UTC
        Assert.AreEqual("14400", request.HttpContext.Response.Headers.RetryAfter.ToString());

        partners.Time.Advance(TimeSpan.FromHours(4));
        Assert.IsTrue(Admit(partners, Request($"Bearer {partners.Key}")).Admitted);
    }

    [TestMethod]
    public async Task Admit_CountsRejectedRequestsAsLimitedButNotTowardTheCap()
    {
        var partners = new FakePartners(new ClientConfig("Partner", RatePerSecond: 1, Burst: 1, DailyCap: 5));
        for (var i = 0; i < 4; i++)
        {
            _ = Admit(partners, Request($"Bearer {partners.Key}"));
        }

        await partners.Usage.Flush(CancellationToken.None);

        Assert.AreEqual(3, partners.Table.Rows[(FakePartners.ClientId, "2026-11-04")][UsageMeter.Limited]);
        Assert.AreEqual(0, partners.Usage.DailyTotal(FakePartners.ClientId));
    }
}
