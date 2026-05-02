namespace m4dModels.Tests;

/// <summary>
/// Unit tests for Song.WasEditedBy.
/// These tests do not require a database service — SongProperties is populated directly
/// using SongProperty.Load to keep tests fast and deterministic.
/// </summary>
[TestClass]
public class SongWasEditedByTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await DanceMusicTester.LoadDances();
    }

    // A song with:
    //   .Create  user=dwgray   time=2015-03-10
    //   .Edit    user=batch-a  time=2015-03-12
    //   .Edit    user=dwgray   time=2016-08-22
    private const string TwoBlocksSameUser =
        ".Create=\tUser=dwgray\tTime=03/10/2015 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tTempo=120.0\t" +
        ".Edit=\tUser=batch-a\tTime=03/12/2015 11:00:00 AM\tTag+=4/4:Tempo\t" +
        ".Edit=\tUser=dwgray\tTime=08/22/2016 14:00:00\tDanceRating=SLS+1";

    // A song with only a .Create block (no .Edit blocks)
    private const string CreateOnly =
        ".Create=\tUser=dwgray\tTime=06/01/2015 09:00:00 AM\tTitle=Solo Create\tArtist=Someone\tTempo=100.0";

    // A song with no blocks at all (just raw properties)
    private const string NoBlocks =
        "User=nobody\tTitle=No Blocks\tArtist=X\tTempo=90.0";

    private static Song LoadSong(string raw)
    {
        var song = new Song();
        SongProperty.Load(raw, song.SongProperties);
        return song;
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [TestMethod]
    public void WasEditedBy_MatchesCreate_ReturnsTrue()
    {
        var song = LoadSong(CreateOnly);
        Assert.IsTrue(song.WasEditedBy("dwgray",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31)));
    }

    [TestMethod]
    public void WasEditedBy_MatchesOneOfTwoEditBlocksSameUser_ReturnsTrue()
    {
        var song = LoadSong(TwoBlocksSameUser);
        // 2015 dwgray create block matches
        Assert.IsTrue(song.WasEditedBy("dwgray",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31)));
    }

    [TestMethod]
    public void WasEditedBy_MatchesSecondEditBlock_ReturnsTrue()
    {
        var song = LoadSong(TwoBlocksSameUser);
        // 2016 dwgray edit block matches
        Assert.IsTrue(song.WasEditedBy("dwgray",
            new DateTime(2016, 1, 1), new DateTime(2016, 12, 31)));
    }

    [TestMethod]
    public void WasEditedBy_MatchesBatchBlock_ReturnsTrue()
    {
        var song = LoadSong(TwoBlocksSameUser);
        Assert.IsTrue(song.WasEditedBy("batch-a",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31)));
    }

    // ── User name mismatch ──────────────────────────────────────────────────

    [TestMethod]
    public void WasEditedBy_WrongUser_ReturnsFalse()
    {
        var song = LoadSong(TwoBlocksSameUser);
        Assert.IsFalse(song.WasEditedBy("other-user",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31)));
    }

    [TestMethod]
    public void WasEditedBy_UserNameIsCaseInsensitive()
    {
        var song = LoadSong(CreateOnly);
        Assert.IsTrue(song.WasEditedBy("DWGRAY",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31)));
        Assert.IsTrue(song.WasEditedBy("DwGrAy",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31)));
    }

    // ── Date range mismatch ─────────────────────────────────────────────────

    [TestMethod]
    public void WasEditedBy_DateRangeTooEarly_ReturnsFalse()
    {
        var song = LoadSong(CreateOnly);
        // Range ends before the block's 2015-06-01
        Assert.IsFalse(song.WasEditedBy("dwgray",
            new DateTime(2014, 1, 1), new DateTime(2014, 12, 31)));
    }

    [TestMethod]
    public void WasEditedBy_DateRangeTooLate_ReturnsFalse()
    {
        var song = LoadSong(CreateOnly);
        // Range starts after the block's 2015-06-01
        Assert.IsFalse(song.WasEditedBy("dwgray",
            new DateTime(2016, 1, 1), new DateTime(2016, 12, 31)));
    }

    [TestMethod]
    public void WasEditedBy_DateRangeContainsOnlyOtherUser_ReturnsFalse()
    {
        var song = LoadSong(TwoBlocksSameUser);
        // Only batch-a has a block in 2015 (dwgray has 2015-create + 2016-edit)
        // Ask for dwgray in the narrow window of 2015-03-12 to 2015-03-12 — that's the batch-a edit day
        Assert.IsFalse(song.WasEditedBy("dwgray",
            new DateTime(2015, 3, 12), new DateTime(2015, 3, 12)));
    }

    // ── Boundary conditions ─────────────────────────────────────────────────

    [TestMethod]
    public void WasEditedBy_ExactFromBoundary_Inclusive()
    {
        var song = LoadSong(CreateOnly); // time = 2015-06-01 09:00:00
        Assert.IsTrue(song.WasEditedBy("dwgray",
            new DateTime(2015, 6, 1, 9, 0, 0), new DateTime(2015, 6, 1, 9, 0, 0)));
    }

    [TestMethod]
    public void WasEditedBy_OneSecondBeforeFrom_ReturnsFalse()
    {
        var song = LoadSong(CreateOnly); // time = 2015-06-01 09:00:00
        Assert.IsFalse(song.WasEditedBy("dwgray",
            new DateTime(2015, 6, 1, 9, 0, 1), new DateTime(2015, 12, 31)));
    }

    // ── Edge cases ──────────────────────────────────────────────────────────

    [TestMethod]
    public void WasEditedBy_NoBlocks_ReturnsFalse()
    {
        var song = LoadSong(NoBlocks);
        Assert.IsFalse(song.WasEditedBy("nobody",
            new DateTime(2000, 1, 1), new DateTime(2030, 12, 31)));
    }

    [TestMethod]
    public void WasEditedBy_EmptySong_ReturnsFalse()
    {
        var song = new Song();
        Assert.IsFalse(song.WasEditedBy("dwgray",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31)));
    }
}
