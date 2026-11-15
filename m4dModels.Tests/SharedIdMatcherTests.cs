using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// The shared-id review's match tiers: how closely the titles and artists of songs that share an
/// id agree.
/// </summary>
[TestClass]
public class SharedIdMatcherTests
{
    private static Song S(string title, string artist) => new() { Title = title, Artist = artist };

    [TestMethod]
    [DataRow("Shape of You", "Ed Sheeran", "Shape Of You (Remastered)", "ed sheeran", SharedIdMatch.Exact)]
    [DataRow("Shape of You", "Ed Sheeran", "Shape of You!", "Ed Sheeran", SharedIdMatch.Exact)]
    [DataRow("Shape of You", "Ed Sheeran feat. Khalid", "Shape of You", "Ed Sheeran", SharedIdMatch.WithoutFeaturing)]
    [DataRow("Shape of You feat. Khalid", "Ed Sheeran", "Shape of You", "Ed Sheeran", SharedIdMatch.WithoutFeaturing)]
    [DataRow("Shape of You - 2017 Remaster", "Ed Sheeran", "Shape of You", "Ed Sheeran", SharedIdMatch.Close)]
    [DataRow("Shape of You", "Ed Sheeran", "Shape of You", "Ed Sheeran & Khalid", SharedIdMatch.FewerArtists)]
    [DataRow("Perfect", "Ed Sheeran", "Shape of You", "Ed Sheeran", SharedIdMatch.Different)]
    [DataRow("Shape of You", "Ed Sheeran", "Shape of You", "Taylor Swift", SharedIdMatch.Different)]
    [DataRow("Shape of You", "", "Shape of You", "", SharedIdMatch.Different)]
    public void Match_ClassifiesTheTiers(string titleA, string artistA, string titleB, string artistB,
        SharedIdMatch expected)
    {
        Assert.AreEqual(expected, SharedIdMatcher.Match([S(titleA, artistA), S(titleB, artistB)]));
    }

    [TestMethod]
    public void Match_EveryPairMustMatch()
    {
        // The first two are a close match, but the third is a different song
        Assert.AreEqual(SharedIdMatch.Different, SharedIdMatcher.Match(
            [S("Shape of You", "Ed Sheeran"), S("Shape of You - 2017 Remaster", "Ed Sheeran"), S("Perfect", "Ed Sheeran")]));
    }

    [TestMethod]
    public void Match_FewerThanTwoSongs_IsDifferent()
    {
        Assert.AreEqual(SharedIdMatch.Different, SharedIdMatcher.Match([S("Shape of You", "Ed Sheeran")]));
        Assert.AreEqual(SharedIdMatch.Different, SharedIdMatcher.Match([]));
    }

    [TestMethod]
    public void TitleArtistMatch_RequiresAnArtist()
    {
        Assert.IsFalse(SharedIdMatcher.TitleArtistMatch([S("Shared Song", ""), S("Shared Song", "")]));
        Assert.IsFalse(SharedIdMatcher.CloseMatch([S("Shared Song", ""), S("Shared Song", "")]));
    }

    private static Song L(int? length) => new() { Title = "Song", Artist = "Artist", Length = length };

    [TestMethod]
    public void LengthSpread_IsTheLongestMinusTheShortest_IgnoringSongsWithoutALength()
    {
        Assert.AreEqual(42, SharedIdMatcher.LengthSpread([L(200), L(null), L(242), L(210)]));
        Assert.AreEqual(0, SharedIdMatcher.LengthSpread([L(200), L(null)]));
        Assert.AreEqual(0, SharedIdMatcher.LengthSpread([L(null), L(null)]));
    }

    [TestMethod]
    [DataRow("Benny Goodman and His Orchestra", "Benny Goodman")]
    [DataRow("Benny Goodman & His Orchestra", "Benny Goodman")]
    public void Match_TheSameArtistsCreditedDifferently_IsSameArtists(string first, string second)
    {
        Assert.AreEqual(SharedIdMatch.SameArtists, SharedIdMatcher.Match([S("Song", first), S("Song", second)]));
    }

    [TestMethod]
    public void Match_ACreditThatLeavesAnArtistOut_IsFewerArtists()
    {
        Assert.AreEqual(SharedIdMatch.FewerArtists,
            SharedIdMatcher.Match([S("Song", "Tony Bennett"), S("Song (Remastered)", "Tony Bennett & Lady Gaga")]));
        Assert.AreEqual(SharedIdMatch.FewerArtists,
            SharedIdMatcher.Match([S("Song", "Tony Bennett"), S("Song (feat. Lady Gaga)", "Tony Bennett & Diana Krall")]),
            "Featured artists count");
    }

    [TestMethod]
    public void Match_DifferentLeadArtists_OrDifferentTitles_AreNotArtistMatches()
    {
        Assert.IsNull(SharedIdMatcher.ArtistsMatch([S("Song", "Lady Gaga & Tony Bennett"), S("Song", "Tony Bennett & Diana Krall")]));
        Assert.IsNull(SharedIdMatcher.ArtistsMatch([S("Song", "Tony Bennett"), S("Other Song", "Tony Bennett & Lady Gaga")]));
    }
}
