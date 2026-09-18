#nullable disable

using m4d.Services;
using m4dModels;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Services;

[TestClass]
[DoNotParallelize] // Tests set ArtistIndexCache's process-wide FirstBuildWait (restored after each).
public class ArtistIndexCacheTests
{
    /// <summary>
    /// Stands in for the file on disk. Counts reads so a test can tell "served from the snapshot"
    /// apart from "read it again".
    /// </summary>
    private class FakeFiles : IArtistIndexFileManager
    {
        public string Json { get; set; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public bool Deleted { get; private set; }

        public Task<string> Read()
        {
            Reads++;
            return Task.FromResult(Json);
        }

        public Task Write(string json)
        {
            Writes++;
            Json = json;
            return Task.CompletedTask;
        }

        public void Delete()
        {
            Deleted = true;
            Json = null;
        }
    }

    private static ArtistIndexCache Cache(FakeFiles files) =>
        new(NullLogger<ArtistIndexCache>.Instance, files);

    private static string Snapshot(DateTime built) =>
        ArtistIndex.Build([["Lindsey Stirling"], ["ZZ Ward"], ["Lindsey Stirling"]], built).SaveToJson();

    [TestMethod]
    public async Task GetAsync_ServesAFreshSnapshotFromDiskWithoutBuilding()
    {
        var files = new FakeFiles { Json = Snapshot(DateTime.UtcNow) };

        // Null service on purpose: a build would have to touch it, so this passing is the proof
        // that the first visitor after a restart is answered from disk rather than made to wait.
        var index = await Cache(files).GetAsync(null);

        Assert.AreEqual(2, index.Count);
        Assert.AreEqual("Lindsey Stirling", index.Top(1).Single().Name);
    }

    [TestMethod]
    public async Task GetAsync_ReadsTheFileOnceHoweverManyCallersArrive()
    {
        var files = new FakeFiles { Json = Snapshot(DateTime.UtcNow) };
        var cache = Cache(files);

        var indexes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => cache.GetAsync(null)));

        Assert.AreEqual(1, files.Reads);
        Assert.IsTrue(indexes.All(i => i != null && i.Count == 2));
    }

    [TestMethod]
    public async Task GetAsync_ReturnsNullRatherThanThrowingWhenTheBuildCannotRun()
    {
        // Nothing on disk and nothing to build from - the page says "still building" instead of
        // taking the failure into the request.
        var files = new FakeFiles { Json = null };
        Assert.IsNull(await WithShortWait(() => Cache(files).GetAsync(null)));
    }

    [TestMethod]
    public async Task GetAsync_KeepsServingDiskWhenTheBuildCannotRun()
    {
        // The reason for persisting at all: search or the database being unreachable costs
        // freshness, not the page.
        var files = new FakeFiles { Json = Snapshot(DateTime.UtcNow - TimeSpan.FromDays(3)) };
        var cache = Cache(files);

        var index = await WithShortWait(() => cache.GetAsync(null));

        Assert.IsNotNull(index);
        Assert.AreEqual(2, index.Count);
        Assert.AreEqual(0, files.Writes, "A failed build must not overwrite the snapshot.");
    }

    [TestMethod]
    public async Task Invalidate_DropsTheFileSoABackfillIsntUndoneByARestart()
    {
        var files = new FakeFiles { Json = Snapshot(DateTime.UtcNow) };
        var cache = Cache(files);

        Assert.IsNotNull(await cache.GetAsync(null));

        cache.Invalidate();

        Assert.IsTrue(files.Deleted);

        // And it doesn't come back from disk: the persisted copy is wrong after a backfill, not
        // merely old, so the next caller has to wait for a real build instead.
        Assert.IsNull(await WithShortWait(() => cache.GetAsync(null)));
    }

    /// <summary>
    /// Runs something that will fall through to the first-build wait, without spending the real
    /// ten seconds on it.
    /// </summary>
    private static async Task<ArtistIndex> WithShortWait(Func<Task<ArtistIndex>> act)
    {
        ArtistIndexCache.FirstBuildWait = TimeSpan.FromMilliseconds(50);
        try
        {
            return await act();
        }
        finally
        {
            ArtistIndexCache.FirstBuildWait = TimeSpan.FromSeconds(10);
        }
    }

    [TestMethod]
    public async Task Current_DoesNotWaitButPopulatesFromDiskForTheNextCaller()
    {
        var files = new FakeFiles { Json = Snapshot(DateTime.UtcNow) };
        var cache = Cache(files);

        // Type-ahead never blocks, so the very first keystroke gets nothing.
        Assert.IsNull(cache.Current(null));

        // The load it kicked off lands, and every keystroke after it is answered.
        for (var i = 0; i < 50 && cache.Current(null) == null; i++)
        {
            await Task.Delay(20);
        }

        Assert.IsNotNull(cache.Current(null));
    }
}
