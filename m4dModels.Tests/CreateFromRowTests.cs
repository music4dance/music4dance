namespace m4dModels.Tests;

/// <summary>
/// Covers every column type in Song.PropertyMap / CreatePropertiesFromRow.
/// </summary>
[TestClass]
public class CreateFromRowTests
{
    private static DanceMusicService _service;
    private static readonly ApplicationUser AdminUser = new("dwgray");

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
        _service = await DanceMusicTester.CreateServiceWithUsers("CreateFromRow");
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static async Task<Song> Row(string[] fields, string[] cells)
        => await Song.CreateFromRow(AdminUser, fields, cells, _service);

    /// Returns the first SongProperty whose BaseName matches.
    private static SongProperty Prop(Song song, string baseName)
        => song.SongProperties.FirstOrDefault(p => p.BaseName == baseName);

    /// Returns all SongProperties whose BaseName matches.
    private static IEnumerable<SongProperty> Props(Song song, string baseName)
        => song.SongProperties.Where(p => p.BaseName == baseName);

    // ─── CUT ordering ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CutOrdering_NoUserColumn_CommandThenUserThenTime()
    {
        var song = await Row(
            [Song.TitleField, Song.ArtistField],
            ["Test Song", "Test Artist"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(Song.CreateCommand, song.SongProperties[0].BaseName);
        Assert.AreEqual(Song.UserField,     song.SongProperties[1].BaseName);
        Assert.AreEqual(Song.TimeField,     song.SongProperties[2].BaseName);
    }

    [TestMethod]
    public async Task CutOrdering_WithUserColumn_TsvUserUsedInCorrectPosition()
    {
        var song = await Row(
            [Song.UserField, Song.TitleField, Song.ArtistField],
            ["Charlie", "Test Song", "Test Artist"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(Song.CreateCommand, song.SongProperties[0].BaseName);
        Assert.AreEqual(Song.UserField,     song.SongProperties[1].BaseName);
        Assert.AreEqual("Charlie",          song.SongProperties[1].Value);
        Assert.AreEqual(Song.TimeField,     song.SongProperties[2].BaseName);
    }

    [TestMethod]
    public async Task CutOrdering_WithUserProxyColumn_UserProxyFieldNameUsed()
    {
        var song = await Row(
            [Song.UserProxy, Song.TitleField],
            ["batch|P", "Test Song"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(Song.CreateCommand, song.SongProperties[0].BaseName);
        Assert.AreEqual(Song.UserProxy,     song.SongProperties[1].BaseName);
        Assert.AreEqual("batch|P",          song.SongProperties[1].Value);
    }

    [TestMethod]
    public async Task CutOrdering_AdminUser_DecoratedNameUsed()
    {
        var song = await Row(
            [Song.TitleField],
            ["Test Song"]);

        Assert.IsNotNull(song);
        var userProp = song.SongProperties[1];
        Assert.AreEqual(Song.UserField, userProp.BaseName);
        Assert.AreEqual(AdminUser.DecoratedName, userProp.Value);
    }

    // ─── Time always inserted ─────────────────────────────────────────────────

    [TestMethod]
    public async Task TimeProperty_AlwaysInsertedWhenNoTimeColumn()
    {
        var song = await Row(
            [Song.TitleField],
            ["Test Song"]);

        Assert.IsNotNull(song);
        Assert.IsNotNull(Prop(song, Song.TimeField), "Time property must always be present");
    }

    // ─── Title / Artist ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Title_SetCorrectly()
    {
        var song = await Row(
            [Song.TitleField, Song.ArtistField],
            ["My Song", "My Artist"]);

        Assert.IsNotNull(song);
        Assert.AreEqual("My Song", song.Title);
    }

    [TestMethod]
    public async Task Artist_SetCorrectly()
    {
        var song = await Row(
            [Song.TitleField, Song.ArtistField],
            ["Title", "Some Artist"]);

        Assert.IsNotNull(song);
        Assert.AreEqual("Some Artist", song.Artist);
    }

    [TestMethod]
    public async Task EmptyTitle_ReturnsNull()
    {
        var song = await Row(
            [Song.TitleField, Song.ArtistField],
            ["", "Artist"]);

        Assert.IsNull(song);
    }

    [TestMethod]
    public async Task WhitespaceOnlyTitle_ReturnsNull()
    {
        var song = await Row(
            [Song.TitleField],
            ["   "]);

        Assert.IsNull(song);
    }

    // ─── Tempo ────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Tempo_FromTempoColumn()
    {
        var song = await Row(
            [Song.TitleField, Song.TempoField],
            ["Title", "128"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(128m, song.Tempo);
    }

    [TestMethod]
    public async Task Tempo_FromBpmColumn_MapsToTempoField()
    {
        // BPM → TempoField (via PropertyMap: "BPM" → TempoField)
        var fields = Song.BuildHeaderMap("TITLE\tBPM");
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", "130"], _service);

        Assert.IsNotNull(song);
        Assert.AreEqual(130m, song.Tempo);
    }

    [TestMethod]
    public async Task Tempo_FromBeatsPerMinuteColumn_MapsToTempoField()
    {
        var fields = Song.BuildHeaderMap("TITLE\tBEATS-PER-MINUTE");
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", "120"], _service);

        Assert.IsNotNull(song);
        Assert.AreEqual(120m, song.Tempo);
    }

    // ─── Length ───────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Length_FromRawSeconds_StoredAsInteger()
    {
        var song = await Row(
            [Song.TitleField, Song.LengthField],
            ["Title", "195"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(195, song.Length);
    }

    [TestMethod]
    public async Task Length_FromMinutesSeconds_ConvertedToSeconds()
    {
        var song = await Row(
            [Song.TitleField, Song.LengthField],
            ["Title", "3:15"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(195, song.Length);
    }

    [TestMethod]
    public async Task Length_AsMilliseconds_DividedByThousand()
    {
        // Values > 1000 are treated as milliseconds
        var song = await Row(
            [Song.TitleField, Song.LengthField],
            ["Title", "195000"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(195, song.Length);
    }

    [TestMethod]
    public async Task Length_FromTimeColumn_MapsToLengthField()
    {
        // "TIME" header maps to LengthField via PropertyMap
        var fields = Song.BuildHeaderMap("TITLE\tTIME");
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", "200"], _service);

        Assert.IsNotNull(song);
        Assert.AreEqual(200, song.Length);
    }

    // ─── Album / Track / Publisher ────────────────────────────────────────────

    [TestMethod]
    public async Task Album_PropertyHasIndexZero()
    {
        var song = await Row(
            [Song.TitleField, Song.AlbumField],
            ["Title", "Greatest Hits"]);

        Assert.IsNotNull(song);
        var albumProp = song.SongProperties.FirstOrDefault(p => p.BaseName == Song.AlbumField);
        Assert.IsNotNull(albumProp);
        Assert.AreEqual(0, albumProp.Index, "Album properties must have index 0");
    }

    [TestMethod]
    public async Task Track_PropertyHasIndexZero()
    {
        var song = await Row(
            [Song.TitleField, Song.TrackField],
            ["Title", "5"]);

        Assert.IsNotNull(song);
        var trackProp = song.SongProperties.FirstOrDefault(p => p.BaseName == Song.TrackField);
        Assert.IsNotNull(trackProp);
        Assert.AreEqual(0, trackProp.Index, "Track properties must have index 0");
    }

    [TestMethod]
    public async Task Publisher_FromPublisherColumn()
    {
        var song = await Row(
            [Song.TitleField, Song.PublisherField],
            ["Title", "Sony Music"]);

        Assert.IsNotNull(song);
        var prop = song.SongProperties.FirstOrDefault(p => p.BaseName == Song.PublisherField);
        Assert.IsNotNull(prop);
        Assert.AreEqual("Sony Music", prop.Value);
    }

    [TestMethod]
    public async Task Publisher_FromLabelColumn_MapsToPublisher()
    {
        var fields = Song.BuildHeaderMap("TITLE\tLABEL");
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", "Atlantic Records"], _service);

        Assert.IsNotNull(song);
        var prop = song.SongProperties.FirstOrDefault(p => p.BaseName == Song.PublisherField);
        Assert.IsNotNull(prop);
        Assert.AreEqual("Atlantic Records", prop.Value);
    }

    [TestMethod]
    public async Task ContributingArtists_FromColumn_MapsToArtist()
    {
        var fields = Song.BuildHeaderMap("TITLE\tCONTRIBUTING ARTISTS");
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", "Various Artists"], _service);

        Assert.IsNotNull(song);
        Assert.AreEqual("Various Artists", song.Artist);
    }

    // ─── Purchase fields ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task PurchaseAS_AddsDownloadPrefix_WhenNonePresent()
    {
        var fields = Song.BuildHeaderMap("TITLE\tAMAZON");
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", "B07EXAMPLE"], _service);

        Assert.IsNotNull(song);
        var prop = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.PurchaseField && p.Qualifier == "AS");
        Assert.IsNotNull(prop);
        Assert.IsTrue(prop.Value.StartsWith("D:"), $"Expected D: prefix, got: {prop.Value}");
    }

    [TestMethod]
    public async Task PurchaseAS_NoDoublePrefix_WhenColonAlreadyPresent()
    {
        var fields = Song.BuildHeaderMap("TITLE\tAMAZON");
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", "D:B07EXAMPLE"], _service);

        Assert.IsNotNull(song);
        var prop = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.PurchaseField && p.Qualifier == "AS");
        Assert.IsNotNull(prop);
        Assert.AreEqual("D:B07EXAMPLE", prop.Value);
    }

    [TestMethod]
    public async Task PurchaseIS_SplitsOnPipe_CreatesIAProperty()
    {
        var fields = Song.BuildHeaderMap("TITLE\tITUNES");
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", "123456789|987654321"], _service);

        Assert.IsNotNull(song);
        var isProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.PurchaseField && p.Qualifier == "IS");
        var iaProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.PurchaseField && p.Qualifier == "IA");
        Assert.IsNotNull(isProp, "IS property should exist");
        Assert.IsNotNull(iaProp, "IA property should exist");
        Assert.AreEqual("123456789", isProp.Value);
        Assert.AreEqual("987654321", iaProp.Value);
    }

    [TestMethod]
    public async Task PurchaseSS_StoredAsIs()
    {
        var fields = Song.BuildHeaderMap("TITLE\tSPOTIFY");
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", "4uLU6hMCjMI75M1A2tKUQC"], _service);

        Assert.IsNotNull(song);
        var prop = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.PurchaseField && p.Qualifier == "SS");
        Assert.IsNotNull(prop);
        Assert.AreEqual("4uLU6hMCjMI75M1A2tKUQC", prop.Value);
    }

    // ─── Dance rating ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DanceRating_SingleDance_CreatesRating()
    {
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField],
            ["Title", "CHA"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(1, song.DanceRatings.Count);
        Assert.AreEqual("CHA", song.DanceRatings[0].DanceId);
    }

    [TestMethod]
    public async Task DanceRating_WithRColumn_UsesExplicitWeight()
    {
        // Use a batch user (exempt from the ±1 per-user contribution cap)
        var batchUser = new ApplicationUser("batch-a", true);
        var song = await Song.CreateFromRow(batchUser,
            [Song.TitleField, Song.DanceRatingField, "R"],
            ["Title", "CHA", "3"],
            _service);

        Assert.IsNotNull(song);
        Assert.AreEqual(1, song.DanceRatings.Count);
        Assert.AreEqual(3, song.DanceRatings[0].Weight);
    }

    [TestMethod]
    public async Task DanceRating_InvalidDanceId_NoRatingCreated()
    {
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField],
            ["Title", "NOTADANCE"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(0, song.DanceRatings.Count);
    }

    // ─── MultiDance ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task MultiDance_TwoDances_BothRatingsCreated()
    {
        var song = await Row(
            [Song.TitleField, Song.MultiDance],
            ["Title", "CHA||SLS"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(2, song.DanceRatings.Count);
        Assert.IsTrue(song.DanceRatings.Any(r => r.DanceId == "CHA"), "Expected CHA rating");
        Assert.IsTrue(song.DanceRatings.Any(r => r.DanceId == "SLS"), "Expected SLS rating");
    }

    [TestMethod]
    public async Task MultiDance_WithDanceTag_TagAppliedToCorrectDance()
    {
        var song = await Row(
            [Song.TitleField, Song.MultiDance],
            ["Title", "CHA|International:Style||SLS"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(2, song.DanceRatings.Count);
        // The CHA dance should have a style tag
        var chaTagProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.AddedTags && p.DanceQualifier == "CHA");
        Assert.IsNotNull(chaTagProp, "Expected dance-specific tag for CHA");
    }

    // ─── COMMENT / COMMENTS columns ───────────────────────────────────────────

    [TestMethod]
    public async Task Comment_InstrumentalKeyword_CreatesInstrumentalTag()
    {
        var song = await Row(
            [Song.TitleField, Song.AddedTags],
            ["Title", "INSTRUMENTAL"]);

        Assert.IsNotNull(song);
        var tagProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.AddedTags && p.DanceQualifier == null);
        Assert.IsNotNull(tagProp);
        Assert.IsTrue(tagProp.Value.Contains("Instrumental:Other"),
            $"Expected Instrumental:Other in {tagProp.Value}");
    }

    [TestMethod]
    public async Task Comment_EnglishLanguageKeyword_CreatesTag()
    {
        var song = await Row(
            [Song.TitleField, Song.AddedTags],
            ["Title", "ENGLISH LANGUAGE"]);

        Assert.IsNotNull(song);
        var tagProp = Prop(song, Song.AddedTags);
        Assert.IsNotNull(tagProp);
        Assert.IsTrue(tagProp.Value.Contains("English:Other"),
            $"Expected English:Other in {tagProp.Value}");
    }

    [TestMethod]
    public async Task Comment_ChristmasKeyword_CreatesBothChristmasAndHolidayTags()
    {
        var song = await Row(
            [Song.TitleField, Song.AddedTags],
            ["Title", "CHRISTMAS song"]);

        Assert.IsNotNull(song);
        var tagProp = Prop(song, Song.AddedTags);
        Assert.IsNotNull(tagProp);
        Assert.IsTrue(tagProp.Value.Contains("Christmas:Other"),
            $"Expected Christmas:Other in {tagProp.Value}");
        Assert.IsTrue(tagProp.Value.Contains("Holiday:Other"),
            $"Expected Holiday:Other in {tagProp.Value}");
    }

    [TestMethod]
    public async Task Comment_TraditionalKeyword_WithDance_CreatesDanceStyleTag()
    {
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField, Song.AddedTags],
            ["Title", "CHA", "TRADITIONAL feel"]);

        Assert.IsNotNull(song);
        var danceTagProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.AddedTags && p.DanceQualifier == "CHA");
        Assert.IsNotNull(danceTagProp, "Expected dance-level style tag for CHA");
        Assert.IsTrue(danceTagProp.Value.Contains("Traditional:Style"),
            $"Expected Traditional:Style in {danceTagProp.Value}");
    }

    [TestMethod]
    public async Task Comment_TagsKeyword_ExtractsCustomTags()
    {
        var song = await Row(
            [Song.TitleField, Song.AddedTags],
            ["Title", "TAGS: Jazz, Blues"]);

        Assert.IsNotNull(song);
        var tagProp = Prop(song, Song.AddedTags);
        Assert.IsNotNull(tagProp);
        Assert.IsTrue(tagProp.Value.Contains("Jazz:Other") || tagProp.Value.Contains("Blues:Other"),
            $"Expected extracted tags in {tagProp.Value}");
    }

    [TestMethod]
    public async Task CommentsColumn_MapsToAddedTags()
    {
        // "COMMENTS" (plural) also maps to AddedTags
        var fields = Song.BuildHeaderMap("TITLE\tCOMMENTS");
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", "HIGH ENERGY"], _service);

        Assert.IsNotNull(song);
        var tagProp = Prop(song, Song.AddedTags);
        Assert.IsNotNull(tagProp);
        Assert.IsTrue(tagProp.Value.Contains("High Energy:Other"),
            $"Expected High Energy:Other in {tagProp.Value}");
    }

    // ─── SongTags / GenreTags / Year ──────────────────────────────────────────

    [TestMethod]
    public async Task SongTags_DefaultCategory_NormalizedToOther()
    {
        var song = await Row(
            [Song.TitleField, Song.SongTags],
            ["Title", "Traditional"]);

        Assert.IsNotNull(song);
        var tagProp = Prop(song, Song.AddedTags);
        Assert.IsNotNull(tagProp, "SongTags should produce a Tag+ property");
        Assert.IsTrue(tagProp.Value.Contains("Traditional:Other"),
            $"Expected Traditional:Other in {tagProp.Value}");
    }

    [TestMethod]
    public async Task GenreTags_CommaSeparated_NormalizedToMusicCategory()
    {
        var song = await Row(
            [Song.TitleField, Song.GenreTags],
            ["Title", "Rock, Pop"]);

        Assert.IsNotNull(song);
        var tagProp = Prop(song, Song.AddedTags);
        Assert.IsNotNull(tagProp, "GenreTags should produce a Tag+ property");
        // Normalized tags should use Music category
        Assert.IsTrue(tagProp.Value.Contains(":Music"),
            $"Expected :Music category in {tagProp.Value}");
    }

    [TestMethod]
    public async Task Year_ValidYear_CreatesOtherTag()
    {
        var song = await Row(
            [Song.TitleField, Song.SongYear],
            ["Title", "2019"]);

        Assert.IsNotNull(song);
        var tagProp = Prop(song, Song.AddedTags);
        Assert.IsNotNull(tagProp, "Year should produce a Tag+ property");
        Assert.IsTrue(tagProp.Value.Contains("2019:Other"),
            $"Expected 2019:Other in {tagProp.Value}");
    }

    [TestMethod]
    public async Task Year_InvalidValue_NoTagCreated()
    {
        var song = await Row(
            [Song.TitleField, Song.SongYear],
            ["Title", "not-a-year"]);

        Assert.IsNotNull(song);
        var tagProp = Prop(song, Song.AddedTags);
        Assert.IsNull(tagProp, "Invalid year should not create any tag");
    }

    [TestMethod]
    public async Task Year_OutOfRange_NoTagCreated()
    {
        var song = await Row(
            [Song.TitleField, Song.SongYear],
            ["Title", "1700"]);

        Assert.IsNotNull(song);
        var tagProp = Prop(song, Song.AddedTags);
        Assert.IsNull(tagProp, "Year 1700 is out of valid range");
    }

    // ─── Dance-specific tags ──────────────────────────────────────────────────

    [TestMethod]
    public async Task DanceTags_AppliedToPriorDanceRating()
    {
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField, Song.DanceTags],
            ["Title", "CHA", "International:Style"]);

        Assert.IsNotNull(song);
        var danceTagProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.AddedTags && p.DanceQualifier == "CHA");
        Assert.IsNotNull(danceTagProp, "DanceTags should create a dance-specific Tag+ property");
        Assert.IsTrue(danceTagProp.Value.Contains("International:Style"),
            $"Expected International:Style in {danceTagProp.Value}");
    }

    [TestMethod]
    public async Task Dancers_SplitOnAmpersand_CreatesTagsPerDancer()
    {
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField, Song.DancersCell],
            ["Title", "CHA", "Alice & Bob"]);

        Assert.IsNotNull(song);
        var danceTagProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.AddedTags && p.DanceQualifier == "CHA");
        Assert.IsNotNull(danceTagProp, "Dancers should create a dance-specific Tag+ property");
        Assert.IsTrue(danceTagProp.Value.Contains("Alice:Other"),
            $"Expected Alice:Other in {danceTagProp.Value}");
        Assert.IsTrue(danceTagProp.Value.Contains("Bob:Other"),
            $"Expected Bob:Other in {danceTagProp.Value}");
    }

    [TestMethod]
    public async Task DanceComment_AppliedToPriorDanceRating()
    {
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField, Song.AddCommentField],
            ["Title", "CHA", "Great cha cha rhythm"]);

        Assert.IsNotNull(song);
        var commentProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.AddCommentField && p.DanceQualifier == "CHA");
        Assert.IsNotNull(commentProp, "DanceComment should be stored with dance qualifier");
        Assert.AreEqual("Great cha cha rhythm", commentProp.Value);
    }

    [TestMethod]
    public async Task DanceComment_WithoutPriorRating_NotStored()
    {
        var song = await Row(
            [Song.TitleField, Song.AddCommentField],
            ["Title", "Some comment"]);

        Assert.IsNotNull(song);
        var commentProp = Prop(song, Song.AddCommentField);
        Assert.IsNull(commentProp, "DanceComment without a prior dance rating should be dropped");
    }

    [TestMethod]
    public async Task Choreographer_AppliedToPriorDanceRating()
    {
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField, Song.AddChoreographerField],
            ["Title", "CHA", "Jane Smith"]);

        Assert.IsNotNull(song);
        var choreoProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.AddChoreographerField && p.DanceQualifier == "CHA");
        Assert.IsNotNull(choreoProp, "Choreographer should be stored with dance qualifier");
        Assert.AreEqual("Jane Smith", choreoProp.Value);
    }

    [TestMethod]
    public async Task StepSheetUrl_AppliedToPriorDanceRating()
    {
        const string url = "https://example.com/stepsheet";
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField, Song.AddStepSheetUrlField],
            ["Title", "CHA", url]);

        Assert.IsNotNull(song);
        var sheetProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.AddStepSheetUrlField && p.DanceQualifier == "CHA");
        Assert.IsNotNull(sheetProp, "StepSheetUrl should be stored with dance qualifier");
        Assert.AreEqual(url, sheetProp.Value);
    }

    [TestMethod]
    public async Task PatternName_AppliedToPriorDanceRating()
    {
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField, Song.AddPatternNameField],
            ["Title", "PTN", "Ring"]);

        Assert.IsNotNull(song);
        var patternProp = song.SongProperties.FirstOrDefault(p =>
            p.BaseName == Song.AddPatternNameField && p.DanceQualifier == "PTN");
        Assert.IsNotNull(patternProp, "PatternName should be stored with dance qualifier");
        Assert.AreEqual("Ring", patternProp.Value);
    }

    // ─── MPM (measures per minute) ────────────────────────────────────────────

    [TestMethod]
    public async Task MPM_FourFourTime_MultipliedByFour()
    {
        // CHA is 4/4 (numerator = 4): 30 MPM × 4 = 120 BPM
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField, Song.MeasureTempo],
            ["Title", "CHA", "30"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(120m, song.Tempo, "CHA (4/4): 30 MPM should yield 120 BPM");
    }

    [TestMethod]
    public async Task MPM_ThreeFourTime_MultipliedByThree()
    {
        // SWZ (Slow Waltz) is 3/4 (numerator = 3): 30 MPM × 3 = 90 BPM
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField, Song.MeasureTempo],
            ["Title", "SWZ", "30"]);

        Assert.IsNotNull(song);
        Assert.AreEqual(90m, song.Tempo, "SWZ (3/4): 30 MPM should yield 90 BPM");
    }

    [TestMethod]
    public async Task MPM_InvalidValue_TempoNotSet()
    {
        var song = await Row(
            [Song.TitleField, Song.DanceRatingField, Song.MeasureTempo],
            ["Title", "CHA", "not-a-number"]);

        Assert.IsNotNull(song);
        Assert.IsNull(song.Tempo, "Non-numeric MPM should not set tempo");
    }

    // ─── TitleArtist combined column ──────────────────────────────────────────

    [TestMethod]
    public async Task TitleArtist_EmDashFormat_ParsesTitleAndArtist()
    {
        // No spaces around the em dash to avoid trailing whitespace in the title capture group
        var song = await Row(
            [Song.TitleArtistCell],
            ["My Song\u2014My Artist"]);

        Assert.IsNotNull(song);
        Assert.AreEqual("My Song", song.Title);
        Assert.AreEqual("My Artist", song.Artist);
    }

    [TestMethod]
    public async Task TitleArtist_QuotedTitleFormat_ParsesTitleAndArtist()
    {
        var song = await Row(
            [Song.TitleArtistCell],
            ["\u201cQuoted Title\u201d Test Artist"]);

        Assert.IsNotNull(song);
        Assert.AreEqual("Quoted Title", song.Title);
    }

    [TestMethod]
    public async Task TitleArtist_InvalidFormat_ReturnsNull()
    {
        // Cannot be parsed → null
        var song = await Row(
            [Song.TitleArtistCell],
            ["No dash or separator here at all"]);

        Assert.IsNull(song);
    }

    // ─── SongId override ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task SongId_ValidGuid_StoredAsProperty()
    {
        var guid = Guid.NewGuid();
        var song = await Row(
            [Song.TitleField, Song.SongIdOverride],
            ["Title", guid.ToString()]);

        Assert.IsNotNull(song);
        var idProp = Prop(song, Song.SongIdOverride);
        Assert.IsNotNull(idProp, "Valid GUID should be stored as SongIdOverride");
        Assert.AreEqual(guid.ToString(), idProp.Value);
    }

    [TestMethod]
    public async Task SongId_InvalidGuid_PropertyDiscarded()
    {
        var song = await Row(
            [Song.TitleField, Song.SongIdOverride],
            ["Title", "not-a-guid"]);

        Assert.IsNotNull(song);
        var idProp = Prop(song, Song.SongIdOverride);
        Assert.IsNull(idProp, "Invalid GUID should be silently discarded");
    }

    // ─── OwnerHash (PATH column) ──────────────────────────────────────────────

    [TestMethod]
    public async Task OwnerHash_FromPathColumn_IsHexHashedValue()
    {
        var fields = Song.BuildHeaderMap("TITLE\tPATH");
        var originalPath = "C:\\Music\\MySong.mp3";
        var song = await Song.CreateFromRow(AdminUser, fields, ["Title", originalPath], _service);

        Assert.IsNotNull(song);
        var hashProp = Prop(song, Song.OwnerHash);
        Assert.IsNotNull(hashProp, "PATH should produce an OwnerHash property");
        // Value should be a hex string (GetHashCode().ToString("X"))
        Assert.IsTrue(
            int.TryParse(hashProp.Value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out _),
            $"OwnerHash value '{hashProp.Value}' should be a valid hex integer");
    }

    // ─── Quoted cells ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task QuotedCell_QuotesStripped()
    {
        var song = await Row(
            [Song.TitleField, Song.ArtistField],
            ["\"My Song\"", "\"My Artist\""]);

        Assert.IsNotNull(song);
        Assert.AreEqual("My Song", song.Title);
        Assert.AreEqual("My Artist", song.Artist);
    }

    // ─── Unknown / null columns ───────────────────────────────────────────────

    [TestMethod]
    public async Task NullField_UnknownColumn_Skipped()
    {
        // null in the fields list means an unrecognized TSV column header
        var song = await Row(
            [Song.TitleField, null, Song.ArtistField],
            ["Title", "ignored value", "Artist"]);

        Assert.IsNotNull(song);
        Assert.AreEqual("Title", song.Title);
        Assert.AreEqual("Artist", song.Artist);
    }

    [TestMethod]
    public async Task UnknownHeaderColumn_MapsToNull_Skipped()
    {
        // BuildHeaderMap maps unrecognized headers to null
        var fields = Song.BuildHeaderMap("TITLE\tUNKNOWNCOL\tARTIST");
        var song = await Song.CreateFromRow(AdminUser, fields,
            ["Title", "should be ignored", "Artist"], _service);

        Assert.IsNotNull(song);
        Assert.AreEqual("Title", song.Title);
        Assert.AreEqual("Artist", song.Artist);
    }

    // ─── Playlist ─────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Playlist_StoredAsProperty()
    {
        var song = await Row(
            [Song.TitleField, Song.PlaylistField],
            ["Title", "My Playlist"]);

        Assert.IsNotNull(song);
        var playlistProp = Prop(song, Song.PlaylistField);
        Assert.IsNotNull(playlistProp);
        Assert.AreEqual("My Playlist", playlistProp.Value);
    }

    [TestMethod]
    public async Task Playlist_WhitespaceOnly_PropertyDiscarded()
    {
        var song = await Row(
            [Song.TitleField, Song.PlaylistField],
            ["Title", "   "]);

        Assert.IsNotNull(song);
        Assert.IsNull(Prop(song, Song.PlaylistField), "Whitespace-only Playlist should be discarded");
    }

    // ─── BuildHeaderMap integration ───────────────────────────────────────────

    [TestMethod]
    public void BuildHeaderMap_AllKnownHeaders_MapsCorrectly()
    {
        var knownHeaders = new (string header, string expected)[]
        {
            ("TITLE",               Song.TitleField),
            ("ARTIST",              Song.ArtistField),
            ("TEMPO",               Song.TempoField),
            ("BPM",                 Song.TempoField),
            ("BEATS-PER-MINUTE",    Song.TempoField),
            ("DANCE",               Song.DanceRatingField),
            ("LENGTH",              Song.LengthField),
            ("TRACK",               Song.TrackField),
            ("ALBUM",               Song.AlbumField),
            ("#",                   Song.TrackField),
            ("PUBLISHER",           Song.PublisherField),
            ("LABEL",               Song.PublisherField),
            ("USER",                Song.UserField),
            // Purchase fields use double-colon format: "Purchase::AS" (no index, qualifier only)
            ("AMAZON",              Song.PurchaseField + ":" + ":AS"),
            ("AMAZONTRACK",         Song.PurchaseField + ":" + ":AS"),
            ("ITUNES",              Song.PurchaseField + ":" + ":IS"),
            ("SPOTIFY",             Song.PurchaseField + ":" + ":SS"),
            ("PATH",                Song.OwnerHash),
            ("COMMENT",             Song.AddedTags),
            ("COMMENTS",            Song.AddedTags),
            ("DANCERS",             Song.DancersCell),
            ("TITLE+ARTIST",        Song.TitleArtistCell),
            ("DANCETAGS",           Song.DanceTags),
            ("SONGTAGS",            Song.SongTags),
            ("GENRE",               Song.GenreTags),
            ("YEAR",                Song.SongYear),
            ("MPM",                 Song.MeasureTempo),
            ("MULTIDANCE",          Song.MultiDance),
            ("DANCECOMMENT",        Song.AddCommentField),
            ("CHOREOGRAPHER",       Song.AddChoreographerField),
            ("STEPSHEETURL",        Song.AddStepSheetUrlField),
            ("PATTERNNAME",         Song.AddPatternNameField),
            ("SONGID",              Song.SongIdOverride),
        };

        foreach (var (header, expected) in knownHeaders)
        {
            var mapped = Song.BuildHeaderMap(header);
            Assert.AreEqual(1, mapped.Count);
            Assert.AreEqual(expected, mapped[0], $"Header '{header}' should map to '{expected}'");
        }
    }

    [TestMethod]
    public void BuildHeaderMap_UnknownHeader_MapsToNull()
    {
        var mapped = Song.BuildHeaderMap("NOTAREALCOLUMN");
        Assert.AreEqual(1, mapped.Count);
        Assert.IsNull(mapped[0], "Unknown header should map to null");
    }
}
