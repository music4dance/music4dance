#nullable disable

using m4d.ViewModels;
using m4dModels;

namespace m4d.Tests.ViewModels;

[TestClass]
public class ArtistIndexModelTests
{
    // Two artists whose keys share "gray", so a search can be steered to one hit or several
    private static ArtistIndex Index() => ArtistIndex.Build(
    [
        ["David Gray"], ["David Gray"], ["David Gray"],
        ["Macy Gray"], ["Macy Gray"],
        ["Lindsey Stirling"], ["Lindsey Stirling"],
        ["Solo Act"],
    ]);

    private static ArtistIndexModel Search(string query, int minSongs = 2) =>
        ArtistIndexModel.Create(Index(), null, query, minSongs);

    [TestMethod]
    public void SoleSearchMatch_OneHit_ReturnsTheArtistsDisplayName()
    {
        var model = Search("macy");

        Assert.AreEqual(1, model.Artists.Count);
        Assert.AreEqual("Macy Gray", model.SoleSearchMatch());
    }

    [TestMethod]
    public void SoleSearchMatch_SeveralHits_ReturnsNull()
    {
        var model = Search("gray");

        Assert.AreEqual(2, model.Artists.Count);
        Assert.IsNull(model.SoleSearchMatch());
    }

    [TestMethod]
    public void SoleSearchMatch_NoHits_ReturnsNull()
    {
        var model = Search("nobody here");

        Assert.AreEqual(0, model.Artists.Count);
        Assert.IsNull(model.SoleSearchMatch());
    }

    [TestMethod]
    public void SoleSearchMatch_CountsWhatThePageWouldShow_NotEveryMatch()
    {
        // The rule is "one entry on the page", so the song-count filter is part of it: "Solo Act"
        // has a single song, which the default threshold hides and the "include singles" toggle
        // brings back - and only in the second case is there an entry to redirect to.
        var withSingles = Search("solo", minSongs: 1);
        Assert.AreEqual("Solo Act", withSingles.SoleSearchMatch());

        var withoutSingles = Search("solo");
        Assert.AreEqual(0, withoutSingles.Artists.Count);
        Assert.IsNull(withoutSingles.SoleSearchMatch());
    }

    [TestMethod]
    public void SoleSearchMatch_LetterBucketWithOneArtist_ReturnsNull()
    {
        // Browsing to a letter that happens to hold one artist is not a lookup - the listing is
        // the answer, so the page stays put.
        var model = ArtistIndexModel.Create(Index(), "L", null, 2);

        Assert.AreEqual(1, model.Artists.Count);
        Assert.IsNull(model.SoleSearchMatch());
    }

    [TestMethod]
    public void SoleSearchMatch_BlankQuery_ReturnsNull()
    {
        // A blank query isn't a search: Create falls through to the top-artists listing
        var model = ArtistIndexModel.Create(Index(), null, "   ", 2);

        Assert.IsNull(model.SoleSearchMatch());
    }

    [TestMethod]
    public void SoleSearchMatch_IndexStillBuilding_ReturnsNull()
    {
        var model = ArtistIndexModel.Create(null, null, "macy", 2);

        Assert.IsTrue(model.Building);
        Assert.IsNull(model.SoleSearchMatch());
    }
}
