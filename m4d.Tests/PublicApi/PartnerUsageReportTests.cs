using m4d.PublicApi;

namespace m4d.Tests.PublicApi;

[TestClass]
public class PartnerUsageReportTests
{
    private static readonly DateOnly Today = new(2026, 11, 23);

    [TestMethod]
    public void Trailing_FillsEveryDayNewestFirst()
    {
        var report = PartnerUsageReport.Trailing(
            [new PartnerUsageDay(Today.AddDays(-2), Exact: 5, Total: 5)], Today, softCap: 100);

        Assert.HasCount(PartnerUsageReport.TrailingDays, report.Days);
        Assert.AreEqual(Today, report.Days[0].Day);
        Assert.AreEqual(Today.AddDays(1 - PartnerUsageReport.TrailingDays), report.Days[^1].Day);
        Assert.AreEqual(5, report.Days[2].Total);
        Assert.AreEqual(0, report.Days[1].Total);
    }

    [TestMethod]
    public void Trailing_SummarizesTheWindow()
    {
        var report = PartnerUsageReport.Trailing(
        [
            new PartnerUsageDay(Today, Exact: 10, Fuzzy: 20, Total: 30, Limited: 7, CapLimited: 2, RateLimitedMinutes: 3),
            new PartnerUsageDay(Today.AddDays(-1), Exact: 120, Total: 120),
            // Outside the window: ignored
            new PartnerUsageDay(Today.AddDays(-40), Exact: 999, Total: 999)
        ], Today, softCap: 100);

        Assert.AreEqual(150, report.Total);
        Assert.AreEqual(5, report.AveragePerDay);
        Assert.AreEqual(Today.AddDays(-1), report.Busiest.Day);
        Assert.AreEqual(1, report.DaysOverSoftCap);
        Assert.AreEqual(5, report.RateLimited);
        Assert.AreEqual(3, report.RateLimitedMinutes);
        Assert.AreEqual(2, report.CapLimited);
    }

    [TestMethod]
    public void Trailing_WithNoTrafficHasNoBusiestDay()
    {
        var report = PartnerUsageReport.Trailing([], Today, softCap: 100);

        Assert.IsNull(report.Busiest);
        Assert.AreEqual(0, report.Total);
        Assert.AreEqual(0, report.DaysOverSoftCap);
    }
}
