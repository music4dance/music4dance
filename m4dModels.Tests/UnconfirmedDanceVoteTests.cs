using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace m4dModels.Tests;

/// <summary>
/// Tests for the "unconfirmed dance votes" feature (architecture/unconfirmed-dance-votes.md):
/// a dance whose current weight traces back entirely to an unconfirmed vote source (currently
/// just "dgsnure", the Spotify-playlist auto-import account) is flagged via
/// DanceRating.IsUnconfirmedOnly, encoded in the index with a -1 Votes sentinel, and filtered
/// out of default search via the new CruftFilter.UnconfirmedDances bit.
/// </summary>
[TestClass]
public class UnconfirmedDanceVoteTests
{
    private static DanceMusicService _service;
    private static DanceMusicCoreService _dms;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
        _service = await DanceMusicTester.CreateServiceWithUsers("UnconfirmedDanceVotes");
        _dms = _service;
    }

    // -------------------------------------------------------------------------
    // Song.LoadProperties / SetRatingsFromProperties: attribution
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task UnconfirmedSourceOnly_Vote_IsMarkedUnconfirmedOnly()
    {
        var song = await Song.Create(
            ".Create=\tUser=dgsnure|P\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance",
            _dms);

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist");
        Assert.AreEqual(1, rating.Weight, "Weight should reflect the actual vote");
        Assert.IsTrue(rating.IsUnconfirmedOnly, "A dance voted only by an unconfirmed source should be flagged");
    }

    [TestMethod]
    public async Task ConfirmedUser_Vote_IsNotMarkedUnconfirmed()
    {
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance",
            _dms);

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist");
        Assert.IsFalse(rating.IsUnconfirmedOnly, "A real user's vote should not be flagged as unconfirmed-only");
    }

    [TestMethod]
    public async Task ConfirmedAndUnconfirmedVotes_IsNotMarkedUnconfirmed()
    {
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance\t" +
            ".Edit=\tUser=dgsnure|P\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA+1",
            _dms);

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist");
        Assert.AreEqual(2, rating.Weight, "Both votes should contribute to the total weight");
        Assert.IsFalse(rating.IsUnconfirmedOnly, "A dance with any confirmed net contribution should not be flagged");
    }

    [TestMethod]
    public async Task ConfirmedVotesNetZero_UnconfirmedPositive_IsMarkedUnconfirmed()
    {
        // alice +1, bob -1 (confirmed net = 0), dgsnure +1 -> weight 1, entirely unconfirmed-derived.
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance\t" +
            ".Edit=\tUser=bob\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA-1\t" +
            ".Edit=\tUser=dgsnure|P\tTime=03/01/2024 10:00:00 AM\tDanceRating=CHA+1",
            _dms);

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist");
        Assert.AreEqual(1, rating.Weight, "Net weight should be 1");
        Assert.IsTrue(
            rating.IsUnconfirmedOnly,
            "With confirmed contributions netting to zero, the surviving weight is entirely unconfirmed-derived");
    }

    [TestMethod]
    public async Task UnconfirmedOnly_DoesNotAffectOtherDances()
    {
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\t" +
            "DanceRating=CHA+1\tDanceRating=SLS+1\tTag+=Cha Cha:Dance|Salsa:Dance\t" +
            ".Edit=\tUser=dgsnure|P\tTime=02/01/2024 10:00:00 AM\tDanceRating=SLS+1",
            _dms);

        var chaRating = song.FindRating("CHA");
        var slsRating = song.FindRating("SLS");
        Assert.IsNotNull(chaRating);
        Assert.IsNotNull(slsRating);
        Assert.IsFalse(chaRating.IsUnconfirmedOnly, "CHA has only a confirmed vote");
        Assert.IsFalse(slsRating.IsUnconfirmedOnly, "SLS has a confirmed vote alongside the unconfirmed one");
    }

    [TestMethod]
    public async Task SetRatingsFromProperties_RecomputesSameUnconfirmedFlag()
    {
        var song = await Song.Create(
            ".Create=\tUser=dgsnure|P\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance",
            _dms);

        song.SetRatingsFromProperties();

        var rating = song.FindRating("CHA");
        Assert.IsNotNull(rating, "Dance rating should exist after recalculation");
        Assert.AreEqual(1, rating.Weight);
        Assert.IsTrue(rating.IsUnconfirmedOnly, "SetRatingsFromProperties should apply the same attribution as LoadProperties");
    }

    // -------------------------------------------------------------------------
    // SongIndex.DocumentFromSong: index encoding
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DocumentFromSong_UnconfirmedOnlyDance_VotesIsNegativeOne()
    {
        var song = await Song.Create(
            ".Create=\tUser=dgsnure|P\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance",
            _dms);

        var index = new TestSongIndex();
        index.AttachToService(_dms);
        var doc = (SearchDocument)index.CallDocumentFromSong(song);
        var chaDoc = (Dictionary<string, object>)doc["dance_CHA"];

        Assert.AreEqual(-1, chaDoc[SongIndex.Votes], "Unconfirmed-only dance should be encoded as -1 in the index");
    }

    [TestMethod]
    public async Task DocumentFromSong_ConfirmedDance_VotesIsRealWeight()
    {
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance",
            _dms);

        var index = new TestSongIndex();
        index.AttachToService(_dms);
        var doc = (SearchDocument)index.CallDocumentFromSong(song);
        var chaDoc = (Dictionary<string, object>)doc["dance_CHA"];

        Assert.AreEqual(1, chaDoc[SongIndex.Votes], "Confirmed dance should keep its real weight in the index");
    }

    [TestMethod]
    public async Task DocumentFromSong_DanceAllVotes_IsNull_WhenOnlyUnconfirmedDance()
    {
        var song = await Song.Create(
            ".Create=\tUser=dgsnure|P\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\tDanceRating=CHA+1\tTag+=Cha Cha:Dance",
            _dms);

        var index = new TestSongIndex();
        index.AttachToService(_dms);
        var doc = (SearchDocument)index.CallDocumentFromSong(song);
        var danceAll = (Dictionary<string, object>)doc["dance_ALL"];

        Assert.IsNull(
            danceAll[SongIndex.Votes],
            "dance_ALL/Votes should be null when the song's only dance rating is unconfirmed-only");
    }

    [TestMethod]
    public async Task DocumentFromSong_DanceAllVotes_ExcludesUnconfirmedOnlyDance()
    {
        var song = await Song.Create(
            ".Create=\tUser=alice\tTime=01/01/2024 10:00:00 AM\tTitle=Test Song\tArtist=Test Artist\t" +
            "DanceRating=CHA+1\tDanceRating=SLS+1\tTag+=Cha Cha:Dance|Salsa:Dance\t" +
            ".Edit=\tUser=bob\tTime=02/01/2024 10:00:00 AM\tDanceRating=CHA-1\t" +
            ".Edit=\tUser=dgsnure|P\tTime=03/01/2024 10:00:00 AM\tDanceRating=CHA+1",
            _dms);

        // CHA ends up unconfirmed-only (alice/bob cancel out, dgsnure supplies the weight);
        // SLS is a plain confirmed +1.
        var chaRating = song.FindRating("CHA");
        Assert.IsTrue(chaRating.IsUnconfirmedOnly, "Precondition: CHA should be unconfirmed-only");

        var index = new TestSongIndex();
        index.AttachToService(_dms);
        var doc = (SearchDocument)index.CallDocumentFromSong(song);
        var danceAll = (Dictionary<string, object>)doc["dance_ALL"];

        Assert.AreEqual(1, danceAll[SongIndex.Votes], "dance_ALL/Votes should only total the confirmed (SLS) weight");
    }

    // -------------------------------------------------------------------------
    // SongIndex.AddCruftInfo: new CruftFilter.UnconfirmedDances clause
    // -------------------------------------------------------------------------

    [TestMethod]
    public void AddCruftInfo_DefaultCruft_ExcludesUnconfirmedOnlySongs()
    {
        var options = new SearchOptions();
        var result = SongIndex.AddCruftInfo(options, CruftFilter.NoCruft);

        StringAssert.Contains(result.Filter, "dance_ALL/Votes ne null");
    }

    [TestMethod]
    public void AddCruftInfo_UnconfirmedDancesBitSet_OmitsClause()
    {
        var options = new SearchOptions();
        var result = SongIndex.AddCruftInfo(options, CruftFilter.UnconfirmedDances);

        Assert.IsFalse(
            (result.Filter ?? string.Empty).Contains("dance_ALL/Votes"),
            "Opting in to UnconfirmedDances should omit the dance_ALL/Votes restriction");
    }

    [TestMethod]
    public void AddCruftInfo_AllCruft_BypassesEveryClause()
    {
        var options = new SearchOptions();
        var result = SongIndex.AddCruftInfo(options, CruftFilter.AllCruft);

        Assert.IsNull(result.Filter, "AllCruft should bypass all cruft restrictions, including the new one");
    }

    // -------------------------------------------------------------------------
    // DanceQuery / SongFilter: opting unconfirmed votes back into a specific-dance query
    // -------------------------------------------------------------------------

    [TestMethod]
    public void GetODataFilter_IncludeUnconfirmedFalse_HasNoSentinelClause()
    {
        var q = new DanceQuery("CHA");
        var odata = q.GetODataFilter(_dms);

        Assert.IsFalse(odata.Contains("Votes eq -1"), "Default query should not reference the sentinel");
    }

    [TestMethod]
    public void GetODataFilter_IncludeUnconfirmedTrue_AddsSentinelOrClause()
    {
        var q = new DanceQuery("CHA");
        var odata = q.GetODataFilter(_dms, includeUnconfirmed: true);

        StringAssert.Contains(odata, "(dance_CHA/Votes ge 1 or dance_CHA/Votes eq -1)");
    }

    [TestMethod]
    public void SongFilter_GetOdataFilter_UnconfirmedDancesBitSet_BringsBackSpecificDance()
    {
        var filter = SongFilter.Create(false);
        filter.Dances = "CHA";
        filter.Level = (int)CruftFilter.UnconfirmedDances;
        var odata = filter.GetOdataFilter(_dms);

        StringAssert.Contains(odata, "dance_CHA/Votes eq -1");
    }

    [TestMethod]
    public void SongFilter_GetOdataFilter_DefaultLevel_DoesNotBringBackSpecificDance()
    {
        var filter = SongFilter.Create(false);
        filter.Dances = "CHA";
        var odata = filter.GetOdataFilter(_dms);

        Assert.IsFalse(odata.Contains("Votes eq -1"));
    }
}
