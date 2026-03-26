using Microsoft.EntityFrameworkCore;

namespace m4dModels.Tests;

[TestClass]
public class SearchesTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    [TestMethod]
    public async Task LoadSearches_LoadsAllRecords()
    {
        using var service = await DanceMusicTester.CreatePopulatedService("SearchesTests_LoadAll");

        var count = service.Context.Searches.Count();
        Assert.AreEqual(16, count, "All 16 searches from test-searches.txt should be loaded");
    }

    [TestMethod]
    public async Task LoadSearches_Incremental_UpdatesCountNotDuplicate()
    {
        using var service = await DanceMusicTester.CreateServiceWithUsers("SearchesTests_Incremental");

        var lines = new List<string>
        {
            "dwgray\tMy CHA Search\t.-CHA-.-.-.-.-120.0-124.0\tFalse\t3\t1/1/2024 12:00 PM\t1/1/2024 12:00 PM"
        };
        await service.LoadSearches(lines);

        // Second load of identical user+query with a higher count
        var lines2 = new List<string>
        {
            "dwgray\tMy CHA Search\t.-CHA-.-.-.-.-120.0-124.0\tFalse\t5\t1/1/2024 12:00 PM\t1/2/2024 12:00 PM"
        };
        await service.LoadSearches(lines2);

        var searches = service.Context.Searches
            .Where(s => s.Query == ".-CHA-.-.-.-.-120.0-124.0")
            .ToList();
        Assert.AreEqual(1, searches.Count, "Incremental load must not create a duplicate row");
        Assert.AreEqual(5, searches[0].Count, "Count should be updated to the incoming value");
    }

    [TestMethod]
    public async Task LoadSearches_AnonymousUser_UsesNullUserId()
    {
        using var service = await DanceMusicTester.CreateServiceWithUsers("SearchesTests_Anonymous");

        // A line with an empty username field (anonymous search)
        var lines = new List<string>
        {
            "\tAnon Search\t.-FXT-Advanced-.-.-.-.\tFalse\t1\t1/1/2024 12:00 PM\t1/1/2024 12:00 PM"
        };
        await service.LoadSearches(lines);

        var searches = service.Context.Searches.ToList();
        Assert.AreEqual(1, searches.Count);
        Assert.IsNull(searches[0].ApplicationUserId, "Anonymous user should have null ApplicationUserId");
    }

    [TestMethod]
    public async Task SerializeSearches_RoundTrip()
    {
        using var service = await DanceMusicTester.CreateServiceWithUsers("SearchesTests_RoundTrip");

        var now = DateTime.Now;
        var lines = new List<string>
        {
            $"dwgray\tMy CHA Search\t.-CHA-.-.-.-.-120.0-124.0\tFalse\t3\t{now:g}\t{now:g}",
            $"batch\tMy FXT Search\t.-FXT-.-.-.-.\tFalse\t5\t{now:g}\t{now:g}",
        };
        await service.LoadSearches(lines);
        Assert.AreEqual(2, service.Context.Searches.Count());

        // Serialize
        var serialized = service.SerializeSearches(withHeader: false);
        Assert.AreEqual(2, serialized.Count, "Serialized output should contain 2 records");

        // Clear
        service.Context.Searches.RemoveRange(service.Context.Searches);
        await service.Context.SaveChangesAsync();
        Assert.AreEqual(0, service.Context.Searches.Count());

        // Reload via bulk
        await service.LoadSearches(new List<string>(serialized), reload: true);

        Assert.AreEqual(2, service.Context.Searches.Count(), "Round-trip should preserve all records");
    }

    [TestMethod]
    public async Task SerializeSearches_FromDateFilter_ExcludesOlderRecords()
    {
        using var service = await DanceMusicTester.CreatePopulatedService("SearchesTests_FromDate");

        // All test-searches.txt records have Modified dates in January 2016
        var cutoff = new DateTime(2020, 1, 1);
        var filtered = service.SerializeSearches(withHeader: false, from: cutoff);

        Assert.AreEqual(0, filtered.Count, "Records older than the cutoff date should be excluded");
    }

    [TestMethod]
    public async Task SerializeSearches_WithRecentRecord_IncludesItAfterCutoff()
    {
        using var service = await DanceMusicTester.CreateServiceWithUsers("SearchesTests_Recent");

        var recentDate = DateTime.Now.AddDays(-1);
        var lines = new List<string>
        {
            $"dwgray\tRecent Search\t.-CHA-.-.-.-.\tFalse\t1\t{recentDate:g}\t{recentDate:g}"
        };
        await service.LoadSearches(lines);

        var cutoff = DateTime.Now.AddDays(-2);
        var filtered = service.SerializeSearches(withHeader: false, from: cutoff);

        Assert.AreEqual(1, filtered.Count, "A recently-modified record should appear after the cutoff");
    }

    [TestMethod]
    public async Task LoadSearches_SevenFieldFormat_MostRecentPageIsNull()
    {
        // Backward-compatibility: old 7-field backup files must load without error and
        // produce null MostRecentPage on every row.
        using var service = await DanceMusicTester.CreateServiceWithUsers("SearchesTests_SevenField");

        var lines = new List<string>
        {
            "dwgray\tMy CHA Search\t.-CHA-.-.-.-.-120.0-124.0\tFalse\t3\t1/1/2024 12:00 PM\t1/1/2024 12:00 PM",
            "batch\tMy FXT Search\t.-FXT-.-.-.-.\tFalse\t5\t1/1/2024 12:00 PM\t1/1/2024 12:00 PM"
        };
        await service.LoadSearches(lines);

        var searches = service.Context.Searches.ToList();
        Assert.AreEqual(2, searches.Count, "Both 7-field records should load successfully");
        Assert.IsTrue(searches.All(s => s.MostRecentPage == null),
            "MostRecentPage should be null when loading 7-field (old) backup format");
    }

    [TestMethod]
    public async Task SerializeSearches_WithMostRecentPage_RoundTripsCorrectly()
    {
        using var service = await DanceMusicTester.CreateServiceWithUsers("SearchesTests_MRPRoundTrip");

        var now = DateTime.Now;
        var lines = new List<string>
        {
            $"dwgray\tMy CHA Search\t.-CHA-.-.-.-.-120.0-124.0\tFalse\t3\t{now:g}\t{now:g}"
        };
        await service.LoadSearches(lines);

        // Manually set MostRecentPage on the loaded record to simulate a page-2 visit
        var loaded = service.Context.Searches.First();
        loaded.MostRecentPage = 3;
        _ = await service.Context.SaveChangesAsync();

        // Round-trip
        var serialized = service.SerializeSearches(withHeader: false);
        Assert.AreEqual(1, serialized.Count);

        service.Context.Searches.RemoveRange(service.Context.Searches);
        _ = await service.Context.SaveChangesAsync();

        await service.LoadSearches(new List<string>(serialized), reload: true);

        var restored = service.Context.Searches.First();
        Assert.AreEqual(3, restored.MostRecentPage, "MostRecentPage should survive a serialize/reload round-trip");
    }
}
