using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// SharedIdMatcher.WithinLengthTolerance: the admin merge candidates' length filter (Level 3),
/// the same pairwise rule the add-time match uses. Songs whose lengths are apart from one another
/// (more than SharedIdMatcher.LengthTolerance) are dropped until none are; songs without a length
/// are kept.
/// </summary>
[TestClass]
public class WithinLengthToleranceTests
{
    private static Song S(int? length, string title = "Song") =>
        new() { SongId = Guid.NewGuid(), Title = title, Length = length };

    private static void AssertNoPairApart(IReadOnlyList<Song> songs)
    {
        for (var i = 0; i < songs.Count; i++)
        {
            for (var j = i + 1; j < songs.Count; j++)
            {
                Assert.IsFalse(SharedIdMatcher.LengthsApart(songs[i], songs[j]),
                    $"{songs[i].Length} and {songs[j].Length} are apart");
            }
        }
    }

    [TestMethod]
    public void Outlier_IsDropped()
    {
        var songs = new List<Song> { S(180), S(195), S(300) };

        var kept = SharedIdMatcher.WithinLengthTolerance(songs);

        CollectionAssert.AreEquivalent(new[] { 180, 195 }, kept.Select(s => s.Length).ToList());
    }

    [TestMethod]
    public void AllWithinTolerance_AreAllKept()
    {
        var songs = new List<Song> { S(180), S(185), S(195) };

        Assert.AreEqual(3, SharedIdMatcher.WithinLengthTolerance(songs).Count);
    }

    [TestMethod]
    public void ExactlyTheTolerance_IsKept_OneMoreSecondIsNot()
    {
        Assert.AreEqual(2, SharedIdMatcher.WithinLengthTolerance([S(180), S(195)]).Count);
        Assert.AreEqual(1, SharedIdMatcher.WithinLengthTolerance([S(180), S(196)]).Count);
    }

    [TestMethod]
    public void MultipleOutliers_AreAllDropped()
    {
        var songs = new List<Song> { S(180), S(190), S(195), S(300), S(60) };

        var kept = SharedIdMatcher.WithinLengthTolerance(songs);

        CollectionAssert.AreEquivalent(new[] { 180, 190, 195 }, kept.Select(s => s.Length).ToList());
    }

    [TestMethod]
    public void NoLengths_AreAllKept()
    {
        var songs = new List<Song> { S(null), S(null), S(null) };

        Assert.AreEqual(3, SharedIdMatcher.WithinLengthTolerance(songs).Count);
    }

    [TestMethod]
    public void SongsWithoutLength_AreKeptBesideTheOthers()
    {
        var songs = new List<Song> { S(180), S(195), S(null), S(300) };

        var kept = SharedIdMatcher.WithinLengthTolerance(songs);

        Assert.AreEqual(3, kept.Count);
        Assert.IsTrue(kept.Any(s => !s.Length.HasValue));
        Assert.IsFalse(kept.Any(s => s.Length == 300));
    }

    [TestMethod]
    public void SingleSong_IsUnchanged()
    {
        Assert.AreEqual(1, SharedIdMatcher.WithinLengthTolerance([S(180)]).Count);
    }

    [TestMethod]
    public void TwoFarApartSongs_OneIsDropped()
    {
        // Neither is the one to keep: a tie, so one goes and the other stays
        var kept = SharedIdMatcher.WithinLengthTolerance([S(100), S(200)]);

        Assert.AreEqual(1, kept.Count);
    }

    [TestMethod]
    public void EvenCountWithADivergentEnd_KeepsAPairWithinTolerance()
    {
        var songs = new List<Song> { S(180), S(190), S(200), S(210) };

        var kept = SharedIdMatcher.WithinLengthTolerance(songs);

        Assert.AreEqual(2, kept.Count);
        AssertNoPairApart(kept);
    }

    [TestMethod]
    public void Chain_LeavesNoPairApart()
    {
        // Each neighbour is within tolerance, but the ends are not
        var songs = new List<Song> { S(100), S(110), S(120), S(130), S(140) };

        var kept = SharedIdMatcher.WithinLengthTolerance(songs);

        Assert.IsTrue(kept.Count >= 2);
        AssertNoPairApart(kept);
    }

    [TestMethod]
    public void RealWorldMix_DropsTheVersionsFarFromTheRest()
    {
        var songs = new List<Song>
        {
            S(180), S(185), S(190), S(195), S(210), S(210), S(360), S(420), S(null)
        };

        var kept = SharedIdMatcher.WithinLengthTolerance(songs);

        Assert.IsFalse(kept.Any(s => s.Length >= 360), "The extended and live versions go");
        Assert.IsTrue(kept.Any(s => !s.Length.HasValue), "The song with no length stays");
        AssertNoPairApart(kept);
    }
}
