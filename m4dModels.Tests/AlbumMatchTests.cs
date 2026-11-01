namespace m4dModels.Tests;

// Album names made only of symbols (Ed Sheeran's "+", "×", "÷", "=") all normalize to "", so the
// album and title comparisons must not treat two empty normalized forms as the same name (#363).
[TestClass]
public class AlbumMatchTests
{
    private static Song CreateSong(params (string Name, int Track)[] albums)
    {
        return new Song
        {
            Title = "Bad Habits",
            Artist = "Ed Sheeran",
            Albums =
            [
                .. albums.Select((a, i) => new AlbumDetails { Name = a.Name, Track = a.Track, Index = i })
            ]
        };
    }

    [TestMethod]
    public void AlbumFromTitle_SymbolTitleWithParenthetical_DoesNotMatchOtherSymbolAlbum()
    {
        var song = CreateSong(("=", 2), ("Pop Deluxe", 7));

        Assert.IsNull(song.AlbumFromTitle("÷ (Deluxe)"));
        Assert.AreEqual(0, song.TrackFromAlbum("÷ (Deluxe)"));
    }

    [TestMethod]
    public void AlbumFromTitle_SymbolTitle_ReturnsMatchingSymbolAlbum()
    {
        var song = CreateSong(("=", 2), ("÷", 5));

        Assert.AreEqual("÷", song.AlbumFromTitle("÷")?.Name);
        Assert.AreEqual(5, song.TrackFromAlbum("÷"));
        Assert.AreEqual("=", song.AlbumFromTitle("=")?.Name);
        Assert.AreEqual(2, song.TrackFromAlbum("="));
        Assert.IsNull(song.AlbumFromTitle("+"));
    }

    [TestMethod]
    public void AlbumFromTitle_NormalizedVariants_StillMatch()
    {
        var song = CreateSong(("=", 2), ("Mamma Mia! The Movie Soundtrack", 3), ("Divide", 4));

        Assert.AreEqual(
            "Mamma Mia! The Movie Soundtrack",
            song.AlbumFromTitle("mamma mia the movie soundtrack")?.Name);
        // Falls back to the title without its parenthetical
        Assert.AreEqual("Divide", song.AlbumFromTitle("Divide (Deluxe)")?.Name);
        Assert.AreEqual("=", song.AlbumFromTitle(" = ")?.Name);
    }

    [TestMethod]
    public void FindAlbum_SymbolAlbum_DoesNotMatchOtherSymbolAlbum()
    {
        var song = CreateSong(("=", 2), ("Pop Deluxe", 7));

        Assert.IsNull(song.FindAlbum("÷"));
        Assert.AreEqual("=", song.FindAlbum("=")?.Name);
    }

    [TestMethod]
    public void MergeAlbums_DistinctSymbolAlbums_AreNotMerged()
    {
        var song = CreateSong(("=", 2), ("÷", 5), ("+", 1));

        var merged = AlbumDetails.MergeAlbums(song.Albums, song.Artist, false);

        CollectionAssert.AreEquivalent(
            new[] { "=", "÷", "+" }, merged.Select(a => a.Name).ToList());
    }

    [TestMethod]
    public void MergeAlbums_SameSymbolAlbum_IsMerged()
    {
        var song = CreateSong(("÷", 5), ("÷ ", 0), ("=", 2));

        var merged = AlbumDetails.MergeAlbums(song.Albums, song.Artist, false);

        CollectionAssert.AreEquivalent(new[] { "÷", "=" }, merged.Select(a => a.Name).ToList());
    }

    [TestMethod]
    public void TitleArtistMatch_SymbolTitles()
    {
        var song = new Song { Title = "÷", Artist = "Ed Sheeran" };

        Assert.IsTrue(song.TitleArtistMatch("÷", "Ed Sheeran"));
        Assert.IsFalse(song.TitleArtistMatch("=", "Ed Sheeran"));
    }

    [TestMethod]
    public void NamesMatch()
    {
        Assert.IsTrue(Song.NamesMatch("Ed Sheeran", " ed sheeran "));
        Assert.IsTrue(Song.NamesMatch("Beyoncé", "Beyonce", Song.CreateNormalForm));
        Assert.IsTrue(Song.NamesMatch("!!!", "!!!", Song.CreateNormalForm));
        Assert.IsFalse(Song.NamesMatch("!!!", "+", Song.CreateNormalForm));
        Assert.IsFalse(Song.NamesMatch("Beyoncé", "Beyonce"));
        Assert.IsFalse(Song.NamesMatch("", ""));
        Assert.IsFalse(Song.NamesMatch(null, "Ed Sheeran"));
    }
}
