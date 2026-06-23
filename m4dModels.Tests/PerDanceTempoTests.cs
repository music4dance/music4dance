using System.Diagnostics;
using Azure.Search.Documents.Models;

namespace m4dModels.Tests;

/// <summary>
/// Tests for the per-dance tempo feature (Phase 1 data model, Phase 2 index, Phase 3 filter/sort).
/// </summary>
[TestClass]
public class PerDanceTempoTests
{
    private static DanceMusicService _service;
    private static DanceMusicCoreService _dms;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
        _service = await DanceMusicTester.CreateServiceWithUsers("PerDanceTempo");
        _dms = _service;
    }

    // -------------------------------------------------------------------------
    // Phase 1: DanceRating.Tempo data model
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DanceRating_TempoOverride_IsSetFromSerialization()
    {
        // Arrange: song with two dances; CHA gets a tempo override, SLS does not.
        const string songData =
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\t" +
            "Title=Per Dance Tempo Test\tArtist=Test Artist\tTempo=120.0\t" +
            "Tag+=Cha Cha:Dance|Salsa:Dance\t" +
            "DanceRating=CHA+1\tDanceRating=SLS+1\t" +
            "Tempo:CHA=128.0";

        // Act
        var song = await Song.Create(songData, _dms);

        // Assert
        Assert.IsNotNull(song, "Song should be created successfully");
        var chaRating = song.DanceRatings.FirstOrDefault(dr => dr.DanceId == "CHA");
        var slsRating = song.DanceRatings.FirstOrDefault(dr => dr.DanceId == "SLS");

        Assert.IsNotNull(chaRating, "CHA rating should exist");
        Assert.IsNotNull(slsRating, "SLS rating should exist");

        Assert.AreEqual(128.0m, chaRating.Tempo, "CHA should have the per-dance tempo override");
        Assert.IsNull(slsRating.Tempo, "SLS should have no override (inherits song tempo)");
    }

    [TestMethod]
    public async Task DanceRating_TempoOverride_RoundTrips()
    {
        // Arrange
        const string songData =
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\t" +
            "Title=Tempo RoundTrip\tArtist=Test Artist\tTempo=120.0\t" +
            "Tag+=Waltz:Dance\t" +
            "DanceRating=WLZ+1\t" +
            "Tempo:WLZ=90.0";

        var song = await Song.Create(songData, _dms);
        Assert.IsNotNull(song, "Initial song creation failed");

        // Serialize and reload
        var serialized = song.Serialize([Song.NoSongId]);
        Trace.WriteLine($"Serialized: {serialized}");

        var reloaded = await Song.Create(serialized, _dms);

        // Assert
        Assert.IsNotNull(reloaded, "Reloaded song should not be null");
        var wlzRating = reloaded.DanceRatings.FirstOrDefault(dr => dr.DanceId == "WLZ");
        Assert.IsNotNull(wlzRating, "WLZ rating should exist in reloaded song");
        Assert.AreEqual(90.0m, wlzRating.Tempo, "WLZ tempo should survive serialization round-trip");
    }

    [TestMethod]
    public async Task DanceRating_NoTempoOverride_TempoIsNull()
    {
        // Arrange: song with one dance, no Tempo+ line.
        const string songData =
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\t" +
            "Title=No Override\tArtist=Test Artist\tTempo=120.0\t" +
            "Tag+=Foxtrot:Dance\t" +
            "DanceRating=FXT+1";

        var song = await Song.Create(songData, _dms);

        Assert.IsNotNull(song, "Song should be created");
        var fxtRating = song.DanceRatings.FirstOrDefault(dr => dr.DanceId == "FXT");
        Assert.IsNotNull(fxtRating, "FXT rating should exist");
        Assert.IsNull(fxtRating.Tempo, "No tempo override => DanceRating.Tempo should be null");
    }

    [TestMethod]
    public async Task DanceRating_EditAddsTempoOverride()
    {
        // Song with create + edit that sets a per-dance tempo override.
        const string songData =
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\t" +
            "Title=Edit Adds Tempo\tArtist=Test Artist\tTempo=120.0\t" +
            "Tag+=Rumba:Dance\t" +
            "DanceRating=RMB+1\t" +
            ".Edit=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\t" +
            "Tempo:RMB=108.0";

        var song = await Song.Create(songData, _dms);
        Assert.IsNotNull(song);

        var rmbRating = song.DanceRatings.FirstOrDefault(dr => dr.DanceId == "RMB");
        Assert.IsNotNull(rmbRating, "RMB rating should exist");
        Assert.AreEqual(108.0m, rmbRating.Tempo, "Edit should set per-dance tempo override");
    }

    [TestMethod]
    public async Task DanceRating_DanceTempoPromotesSongTempo_WhenSongTempoIsNull()
    {
        // If a dance tempo override is set and song has no tempo, song.Tempo should be promoted.
        const string songData =
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\t" +
            "Title=Promote Test\tArtist=Test Artist\t" +
            "Tag+=Cha Cha:Dance\t" +
            "DanceRating=CHA+1\t" +
            "Tempo:CHA=128.0";

        var song = await Song.Create(songData, _dms);
        Assert.IsNotNull(song);
        Assert.AreEqual(128.0m, song.Tempo, "song.Tempo should be promoted from CHA override");
        Assert.AreEqual(128.0m, song.DanceRatings.First(dr => dr.DanceId == "CHA").Tempo,
            "CHA.Tempo override should be preserved");
    }

    [TestMethod]
    public async Task DanceRating_DanceTempoDoesNotOverrideSongTempo_WhenAlreadySet()
    {
        // If song.Tempo is already set, Tempo:DanceId should not change it.
        const string songData =
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\t" +
            "Title=No Promote Test\tArtist=Test Artist\tTempo=120.0\t" +
            "Tag+=Cha Cha:Dance\t" +
            "DanceRating=CHA+1\t" +
            "Tempo:CHA=128.0";

        var song = await Song.Create(songData, _dms);
        Assert.IsNotNull(song);
        Assert.AreEqual(120.0m, song.Tempo, "song.Tempo should NOT be changed by dance override");
        Assert.AreEqual(128.0m, song.DanceRatings.First(dr => dr.DanceId == "CHA").Tempo);
    }

    [TestMethod]
    public async Task DanceRating_SecondDanceInheritsPromotedSongTempo()
    {
        // With promoted song.Tempo, a second dance with no override inherits the promoted value.
        const string songData =
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\t" +
            "Title=Inherited Promote\tArtist=Test Artist\t" +
            "Tag+=Cha Cha:Dance|Salsa:Dance\t" +
            "DanceRating=CHA+1\tDanceRating=SLS+1\t" +
            "Tempo:CHA=128.0";

        var song = await Song.Create(songData, _dms);
        Assert.IsNotNull(song);
        Assert.AreEqual(128.0m, song.Tempo, "song.Tempo promoted from CHA");

        var sls = song.DanceRatings.FirstOrDefault(dr => dr.DanceId == "SLS");
        Assert.IsNotNull(sls);
        Assert.IsNull(sls.Tempo, "SLS has no explicit override — dr.Tempo is null");
        // Effective tempo for SLS = sls.Tempo ?? song.Tempo = 128.0 (verified at index time)
        var effectiveSls = sls.Tempo ?? song.Tempo;
        Assert.AreEqual(128.0m, effectiveSls, "SLS effective tempo should inherit promoted song.Tempo");
    }

    // -------------------------------------------------------------------------
    // Phase 3: SongFilter — per-dance tempo filter
    // -------------------------------------------------------------------------

    [TestMethod]
    public void SongFilter_TempoRange_SingleDance_UsesPerDanceTempoField()
    {
        // A filter with a single dance and a tempo range should reference dance_{id}/Tempo.
        // Filter format: Action-Dances-Sort-Search-Purchase-User-TempoMin-TempoMax-Page-Tags
        var filter = SongFilter.Create(false, "Index-CHA-.-.-.-.-100-120-1");

        Assert.IsFalse(filter is SongFilterNext, "Current-version filter should use base SongFilter");
        Assert.IsTrue(filter.IsSingleDance, "Should be single dance");

        var odata = filter.GetOdataFilter(_dms);
        Trace.WriteLine($"OData: {odata}");

        Assert.IsNotNull(odata, "OData filter should not be null");
        StringAssert.Contains(odata, "dance_CHA/Tempo", "Should reference per-dance Tempo sub-field");
        StringAssert.Contains(odata, "ge 99.5", "TempoMin 100 → adjusted to 99.5 for whole-number values");
        StringAssert.Contains(odata, "le 120.5", "TempoMax 120 → adjusted to 120.5 for whole-number values");
    }

    [TestMethod]
    public void SongFilterNext_TempoRange_SingleDance_UsesPerDanceTempoField()
    {
        // Next-version filter should currently match the deployed base behavior.
        var filter = SongFilter.Create(true, "Index-CHA-.-.-.-.-100-120-1");

        Assert.IsTrue(filter is SongFilterNext, "Should still produce SongFilterNext in next-version mode");
        Assert.IsTrue(filter.IsSingleDance, "Should be single dance");

        var odata = filter.GetOdataFilter(_dms);
        Trace.WriteLine($"OData: {odata}");

        Assert.IsNotNull(odata);
        StringAssert.Contains(odata, "dance_CHA/Tempo", "Current next filter should match base per-dance field behavior");
    }

    [TestMethod]
    public void SongFilter_TempoRange_SingleDanceGroup_UsesTopLevelTempoField()
    {
        // Single dance-group filter (e.g. Foxtrot group): no per-dance tempo field available.
        var filter = SongFilter.Create(false, "Index-FXT-.-.-.-.-100-120-1");

        Assert.IsFalse(filter.IsSingleDance, "Single dance-group should not be treated as single dance");

        var odata = filter.GetOdataFilter(_dms);
        Trace.WriteLine($"OData: {odata}");

        Assert.IsNotNull(odata);
        StringAssert.Contains(odata, "(Tempo ge", "Dance-group filter should use top-level Tempo");
    }

    [TestMethod]
    public void SongFilter_TempoRange_MultiDance_UsesTopLevelTempoField()
    {
        // Multi-dance filter: per-dance tempo NOT applicable — should fall back to top-level.
        var filter = SongFilter.Create(false, "Index-CHA,SLS-.-.-.-.-100-120-1");

        Assert.IsFalse(filter is SongFilterNext, "Current-version filter should use base SongFilter");
        Assert.IsFalse(filter.IsSingleDance, "Should be multi-dance");

        var odata = filter.GetOdataFilter(_dms);
        Trace.WriteLine($"OData: {odata}");

        Assert.IsNotNull(odata);
        StringAssert.Contains(odata, "(Tempo ge", "Multi-dance filter should use top-level Tempo");
    }

    [TestMethod]
    public void SongFilter_TempoRange_NoDance_UsesTopLevelTempoField()
    {
        // No dance filter: should also use top-level Tempo.
        var filter = SongFilter.Create(false, "Index-.-.-.-.-.-100-120-1");

        Assert.IsFalse(filter is SongFilterNext, "Current-version filter should use base SongFilter");

        var odata = filter.GetOdataFilter(_dms);
        Trace.WriteLine($"OData: {odata}");

        Assert.IsNotNull(odata);
        StringAssert.Contains(odata, "(Tempo ge", "No-dance filter should use top-level Tempo");
    }

    // -------------------------------------------------------------------------
    // Phase 3: SongFilter — per-dance tempo sort
    // -------------------------------------------------------------------------

    [TestMethod]
    public void SongFilter_TempoSort_SingleDance_UsesPerDanceTempoField()
    {
        // Single dance + tempo sort → should sort by dance_{id}/Tempo.
        var filter = SongFilter.Create(false, "Index-RMB-Tempo-.-.-.-.-.-1");

        Assert.IsFalse(filter is SongFilterNext, "Current-version filter should use base SongFilter");
        Assert.IsTrue(filter.IsSingleDance, "Should be single dance");

        var sort = filter.ODataSort;
        Trace.WriteLine($"Sort: {string.Join(", ", sort)}");

        Assert.AreEqual(1, sort.Count, "Should produce exactly one sort clause");
        StringAssert.Contains(sort[0], "dance_RMB/Tempo", "Sort should reference dance-level Tempo");
    }

    [TestMethod]
    public void SongFilterNext_TempoSort_SingleDance_UsesPerDanceTempoField()
    {
        // Next-version filter should currently match the deployed base behavior.
        var filter = SongFilter.Create(true, "Index-RMB-Tempo-.-.-.-.-.-1");

        Assert.IsTrue(filter is SongFilterNext, "Should still produce SongFilterNext in next-version mode");

        var sort = filter.ODataSort;
        Assert.AreEqual(1, sort.Count, "Should produce exactly one sort clause");
        StringAssert.Contains(sort[0], "dance_RMB/Tempo", "Next filter sort should match base per-dance tempo sort");
    }

    [TestMethod]
    public void SongFilter_TempoSort_MultiDance_UsesTopLevelTempoField()
    {
        // Multi-dance + tempo sort → should fall back to top-level Tempo.
        var filter = SongFilter.Create(false, "Index-CHA,SLS-Tempo-.-.-.-.-.-1");

        Assert.IsFalse(filter is SongFilterNext, "Current-version filter should use base SongFilter");
        Assert.IsFalse(filter.IsSingleDance, "Should be multi-dance");

        var sort = filter.ODataSort;
        Assert.AreEqual(1, sort.Count, "Should produce exactly one sort clause");
        StringAssert.Contains(sort[0], "Tempo asc", "Multi-dance tempo sort should use top-level Tempo");
    }

    [TestMethod]
    public void SongFilter_DanceSort_SingleDance_UsesVotes()
    {
        // Single dance + dance (votes) sort → should NOT use per-dance Tempo.
        var filter = SongFilter.Create(false, "Index-CHA-.-.-.-.-.-.-1");

        Assert.IsFalse(filter is SongFilterNext, "Current-version filter should use base SongFilter");
        Assert.IsTrue(filter.IsSingleDance, "Should be single dance");

        var sort = filter.ODataSort;
        Assert.AreEqual(1, sort.Count, "Should produce exactly one sort clause");
        Assert.IsFalse(sort[0].Contains("Tempo"), "Dance sort should NOT reference Tempo");
        StringAssert.Contains(sort[0], "dance_", "Dance sort should reference a dance field");
        StringAssert.Contains(sort[0], "Votes", "Dance sort should reference votes");
    }

    // -------------------------------------------------------------------------
    // Phase 2: SongIndex schema
    // -------------------------------------------------------------------------

    [TestMethod]
    public void SongIndex_DanceTempoSubField_IsUsedInPerDanceSortPath()
    {
        var filter = SongFilter.Create(false, "Index-CHA-Tempo-.-.-.-.-.-1");

        Assert.AreEqual($"dance_CHA/{SongIndex.DanceTempoSubField} asc", filter.ODataSort[0],
            "Per-dance tempo sort should use the SongIndex dance tempo sub-field path");
    }

    [TestMethod]
    public void SongFilter_TempoFilter_ODataPathMatchesConstant()
    {
        // The per-dance tempo filter path should use the SongIndex constant.
        var filter = SongFilter.Create(false, "Index-CHA-.-.-.-.-100-120-1");
        var odata = filter.GetOdataFilter(_dms);

        var expectedField = $"dance_CHA/{SongIndex.DanceTempoSubField}";
        StringAssert.Contains(odata, expectedField,
            "OData filter should reference the path matching the SongIndex constant");
    }

    // -------------------------------------------------------------------------
    // Phase 2: dance_ALL/Tempo marks songs with a per-dance tempo override
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DocumentFromSong_DanceAllTempo_IsNull_WhenNoOverride()
    {
        const string songData =
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\t" +
            "Title=No Override Doc\tArtist=Test Artist\tTempo=120.0\t" +
            "Tag+=Cha Cha:Dance|Salsa:Dance\t" +
            "DanceRating=CHA+1\tDanceRating=SLS+1";

        var song = await Song.Create(songData, _dms);
        var index = new TestSongIndex();
        index.AttachToService(_dms);

        var doc = (SearchDocument)index.CallDocumentFromSong(song);
        var danceAll = (Dictionary<string, object>)doc["dance_ALL"];

        Assert.IsNull(danceAll[SongIndex.DanceTempoSubField],
            "dance_ALL/Tempo should be null when no dance overrides the song tempo");
    }

    [TestMethod]
    public async Task DocumentFromSong_DanceAllTempo_IsSongTempo_WhenAnyDanceOverrides()
    {
        const string songData =
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\t" +
            "Title=Override Doc\tArtist=Test Artist\tTempo=120.0\t" +
            "Tag+=Cha Cha:Dance|Salsa:Dance\t" +
            "DanceRating=CHA+1\tDanceRating=SLS+1\t" +
            "Tempo:CHA=128.0";

        var song = await Song.Create(songData, _dms);
        var index = new TestSongIndex();
        index.AttachToService(_dms);

        var doc = (SearchDocument)index.CallDocumentFromSong(song);
        var danceAll = (Dictionary<string, object>)doc["dance_ALL"];

        Assert.AreEqual(120.0f, danceAll[SongIndex.DanceTempoSubField],
            "dance_ALL/Tempo should equal song tempo when any dance overrides it");
    }
}
