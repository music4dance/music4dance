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
    [DataRow("Shape of You", "Ed Sheeran", "Shape of You", "Ed Sheeran & Khalid", SharedIdMatch.Close)]
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
}
