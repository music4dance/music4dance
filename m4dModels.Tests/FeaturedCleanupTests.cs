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
    public void Propose_NothingFeatured_ChangesNothing()
    {
        var merged = new Song { Title = "Song Title", Artist = "The Artist" };

        var names = FeaturedCleanup.Propose(merged, [merged]);

        Assert.AreEqual("Song Title", names.Title);
        CollectionAssert.AreEqual(new[] { "The Artist" }, names.Artists.ToList());
    }
}
