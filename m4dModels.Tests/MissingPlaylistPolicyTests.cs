namespace m4dModels.Tests;

[TestClass]
public class MissingPlaylistPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 8, 0, 0);

    private static PlayList Missing(string id, DateTime? since = null) =>
        new() { Id = id, Type = PlayListType.SongsFromSpotify, NotFoundSince = since };

    [TestMethod]
    public void FirstMissStartsTheClockWithoutDeleting()
    {
        var playlist = Missing("a");

        var outcome = MissingPlaylistPolicy.Apply([playlist], 800, Now);

        Assert.IsFalse(outcome.MassFailure);
        Assert.AreEqual(Now, playlist.NotFoundSince);
        Assert.IsFalse(playlist.Deleted);
        CollectionAssert.AreEqual(new[] { playlist }, outcome.Started.ToList());
        Assert.AreEqual(0, outcome.Deleted.Count);
    }

    [TestMethod]
    public void StillMissingWithinTheGracePeriodKeepsTheFirstMiss()
    {
        var since = Now.AddDays(-6);
        var playlist = Missing("a", since);

        var outcome = MissingPlaylistPolicy.Apply([playlist], 800, Now);

        Assert.AreEqual(since, playlist.NotFoundSince);
        Assert.IsFalse(playlist.Deleted);
        Assert.AreEqual(0, outcome.Started.Count);
        Assert.AreEqual(0, outcome.Deleted.Count);
    }

    [TestMethod]
    public void MissingForTheWholeGracePeriodIsSoftDeleted()
    {
        var since = Now - MissingPlaylistPolicy.GracePeriod;
        var playlist = Missing("a", since);

        var outcome = MissingPlaylistPolicy.Apply([playlist], 800, Now);

        Assert.IsTrue(playlist.Deleted);
        Assert.AreEqual(Now, playlist.Updated);
        Assert.AreEqual(since, playlist.NotFoundSince);
        CollectionAssert.AreEqual(new[] { playlist }, outcome.Deleted.ToList());
    }

    // The 2026-11-18 run: 38 of ~800 missing is churn, not an outage
    [TestMethod]
    public void NormalChurnIsNotAMassFailure()
    {
        var playlists = Enumerable.Range(0, 38).Select(i => Missing($"p{i}", Now.AddDays(-8))).ToList();

        var outcome = MissingPlaylistPolicy.Apply(playlists, 800, Now);

        Assert.IsFalse(outcome.MassFailure);
        Assert.AreEqual(38, outcome.Deleted.Count);
    }

    [TestMethod]
    public void AMassFailureChangesNothing()
    {
        var old = Now.AddDays(-30);
        var playlists = Enumerable.Range(0, 100).Select(i => Missing($"p{i}", i % 2 == 0 ? old : null)).ToList();

        var outcome = MissingPlaylistPolicy.Apply(playlists, 800, Now);

        Assert.IsTrue(outcome.MassFailure);
        Assert.AreEqual(0, outcome.Started.Count);
        Assert.AreEqual(0, outcome.Deleted.Count);
        Assert.IsTrue(playlists.All(p => !p.Deleted));
        Assert.AreEqual(50, playlists.Count(p => p.NotFoundSince == null));
    }

    [TestMethod]
    [DataRow(9, 9, false)]     // below the minimum, even if it's the whole run
    [DataRow(10, 100, true)]   // at both thresholds
    [DataRow(10, 101, false)]  // under 10% of the run
    [DataRow(80, 800, true)]
    [DataRow(79, 800, false)]
    public void IsMassFailureNeedsBothTheMinimumAndTheFraction(int notFound, int attempted, bool expected)
    {
        Assert.AreEqual(expected, MissingPlaylistPolicy.IsMassFailure(notFound, attempted));
    }
}
