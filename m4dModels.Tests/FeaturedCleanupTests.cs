using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Moving featured artists out of titles and credits (ArtistSplitter.RemoveTitleFeaturing,
/// RemoveArtistFeaturing) and the names FeaturedCleanup proposes for a merged song.
/// </summary>
[TestClass]
public class FeaturedCleanupTests
{
    [TestMethod]
    [DataRow("Song Title (feat. Crazy Man)", "Song Title")]
    [DataRow("Song Title [ft. Crazy Man]", "Song Title")]
    [DataRow("Song Title (Featuring Crazy Man) (Remix)", "Song Title (Remix)")]
    [DataRow("Song Title feat. Crazy Man", "Song Title")]
    [DataRow("Song Title - feat. Crazy Man", "Song Title")]
    [DataRow("Song Title feat. Crazy Man - Radio Edit", "Song Title - Radio Edit")]
    [DataRow("Song Title (Radio Edit)", "Song Title (Radio Edit)")]
    [DataRow("Defeated", "Defeated")]
    public void RemoveTitleFeaturing(string title, string expected)
    {
        Assert.AreEqual(expected, ArtistSplitter.RemoveTitleFeaturing(title));
    }

    [TestMethod]
    [DataRow("Ed Sheeran feat. Khalid", "Ed Sheeran")]
    [DataRow("Ed Sheeran (ft. Khalid)", "Ed Sheeran")]
    [DataRow("Ed Sheeran Featuring Khalid & Beyoncé", "Ed Sheeran")]
    [DataRow("Little Feat", "Little Feat")]
    [DataRow("Ed Sheeran", "Ed Sheeran")]
    public void RemoveArtistFeaturing(string artist, string expected)
    {
        Assert.AreEqual(expected, ArtistSplitter.RemoveArtistFeaturing(artist));
    }

    [TestMethod]
    public void Propose_MovesTheTitleFeatIntoTheArtists()
    {
        var merged = new Song { Title = "Song Title (feat. Crazy Man)", Artist = "The Artist" };

        var names = FeaturedCleanup.Propose(merged, [merged]);

        Assert.AreEqual("Song Title", names.Title);
        Assert.AreEqual("The Artist", names.Artist);
        CollectionAssert.AreEqual(new[] { "The Artist", "Crazy Man" }, names.Artists.ToList());
    }

    [TestMethod]
    public void Propose_AddsArtistsOnlyTheOtherSongsFeature()
    {
        var merged = new Song { Title = "Song Title", Artist = "The Artist" };
        var featuring = new Song { Title = "Song Title", Artist = "The Artist feat. Crazy Man" };
        var otherCredit = new Song { Title = "Song Title", Artist = "The Artist & Friend" };

        var names = FeaturedCleanup.Propose(merged, [merged, featuring, otherCredit]);

        Assert.AreEqual("Song Title", names.Title);
        CollectionAssert.AreEqual(new[] { "The Artist", "Crazy Man" }, names.Artists.ToList());
    }

    [TestMethod]
    public void Propose_FeatInTitleAndCredit_AddsBothAndKeepsTheCredit()
    {
        var merged = new Song { Title = "Song Title feat. Crazy Man", Artist = "The Artist feat. Friend" };

        var names = FeaturedCleanup.Propose(merged, [merged]);

        Assert.AreEqual("Song Title", names.Title);
        Assert.AreEqual("The Artist feat. Friend", names.Artist);
        CollectionAssert.AreEquivalent(new[] { "The Artist", "Friend", "Crazy Man" }, names.Artists.ToList());
        Assert.AreEqual("The Artist", names.Artists[0]);
    }

    [TestMethod]
    public void Propose_AnArtistFeaturedTwiceInDifferentSpellings_IsListedOnce()
    {
        var merged = new Song { Title = "Song Title", Artist = "The Artist" };
        var first = new Song { Title = "Song Title (feat. Crazy Man)", Artist = "The Artist" };
        var second = new Song { Title = "Song Title", Artist = "The Artist ft. crazy man" };

        var names = FeaturedCleanup.Propose(merged, [first, second]);

        CollectionAssert.AreEqual(new[] { "The Artist", "Crazy Man" }, names.Artists.ToList());
    }

    [TestMethod]
    public void MatchWithoutFeaturing_NeedsEverySongToHaveAnArtist()
    {
        Assert.IsTrue(FeaturedCleanup.MatchWithoutFeaturing(
            [new Song { Title = "Song feat. X", Artist = "A" }, new Song { Title = "Song", Artist = "A feat. X" }]));
        Assert.IsFalse(FeaturedCleanup.MatchWithoutFeaturing(
            [new Song { Title = "Song feat. X", Artist = "" }, new Song { Title = "Song", Artist = "" }]));
    }

    [TestMethod]
    public void Propose_NothingFeatured_ChangesNothing()
    {
        var merged = new Song { Title = "Song Title", Artist = "The Artist" };

        var names = FeaturedCleanup.Propose(merged, [merged]);

        Assert.AreEqual("Song Title", names.Title);
        Assert.AreEqual("The Artist", names.Artist);
        CollectionAssert.AreEqual(new[] { "The Artist" }, names.Artists.ToList());
    }

    [TestMethod]
    public void Propose_ACreditLeavingAnArtistOut_TakesASongsCreditThatNamesThemAll()
    {
        var merged = new Song { Title = "Song Title (feat. Crazy Man)", Artist = "The Artist" };
        var plain = new Song { Title = "Song Title", Artist = "The Artist" };
        var credited = new Song { Title = "Song Title", Artist = "The Artist feat. Crazy Man" };

        var names = FeaturedCleanup.Propose(merged, [plain, merged, credited]);

        Assert.AreEqual("Song Title", names.Title);
        Assert.AreEqual("The Artist feat. Crazy Man", names.Artist);
        CollectionAssert.AreEqual(new[] { "The Artist", "Crazy Man" }, names.Artists.ToList());
    }

    [TestMethod]
    public void Propose_ACreditLeavingArtistsOut_WithEvidence_KeepsEveryArtistAndTheFullCredit()
    {
        var merged = new Song { Title = "Song Title", Artist = "Tony Bennett" };
        var full = new Song { Title = "Song Title", Artist = "Tony Bennett & Lady Gaga" };

        var without = FeaturedCleanup.Propose(merged, [merged, full]);
        var names = FeaturedCleanup.Propose(merged, [merged, full],
            FeaturedCleanup.Knowing(["Tony Bennett", "Lady Gaga"]));

        Assert.AreEqual("Tony Bennett", without.Artist, "Without evidence \"&\" could be a duo's name");
        Assert.AreEqual("Tony Bennett & Lady Gaga", names.Artist);
        CollectionAssert.AreEqual(new[] { "Tony Bennett", "Lady Gaga" }, names.Artists.ToList());
    }

    [TestMethod]
    public void Propose_ACreditThatNamesEveryArtist_IsKept()
    {
        var merged = new Song { Title = "Song Title", Artist = "The Artist & Crazy Man" };
        var credited = new Song { Title = "Song Title", Artist = "The Artist feat. Crazy Man" };

        var names = FeaturedCleanup.Propose(merged, [merged, credited]);

        Assert.AreEqual("The Artist & Crazy Man", names.Artist);
    }

    [TestMethod]
    public void Propose_NoSongCreditsEveryArtist_KeepsTheMergedCredit()
    {
        var merged = new Song { Title = "Song Title (feat. Crazy Man)", Artist = "The Artist" };
        var other = new Song { Title = "Song Title", Artist = "The Artist feat. Friend" };

        var names = FeaturedCleanup.Propose(merged, [merged, other]);

        Assert.AreEqual("The Artist", names.Artist);
        CollectionAssert.AreEquivalent(new[] { "The Artist", "Crazy Man", "Friend" }, names.Artists.ToList());
    }
}
