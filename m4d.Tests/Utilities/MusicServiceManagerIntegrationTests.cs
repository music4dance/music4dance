using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using m4d.Utilities;
using m4dModels;
using m4dModels.Tests;
using DanceLibrary;
using Microsoft.Extensions.Logging;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace m4d.Tests.Utilities;

/// <summary>
/// Assembly-level initialization for all tests.
/// Sets up ApplicationLogging to prevent TypeInitializationException in MusicServiceManager.
/// </summary>
[TestClass]
public class AssemblyInitializer
{
    [AssemblyInitialize]
    public static void AssemblySetup(TestContext context)
    {
        // Setup ApplicationLogging for ALL tests (must run before any MusicServiceManager is created)
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger>();
        mockLoggerFactory
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(mockLogger.Object);
        ApplicationLogging.LoggerFactory = mockLoggerFactory.Object;
    }
}

/// <summary>
/// Integration tests for MusicServiceManager that test the full validation workflow
/// using DanceMusicTester to create properly configured services.
/// </summary>
[TestClass]
public class MusicServiceManagerIntegrationTests
{
    private Mock<IConfiguration> _mockConfiguration = null!;
    private MusicServiceManager _manager = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _manager = new MusicServiceManager(_mockConfiguration.Object);
    }

    [ClassInitialize]
    public static async Task ClassSetup(TestContext context)
    {
        // Load the dance database once for all tests (includes validation rules)
        await DanceMusicTester.LoadDances();
    }

    /// <summary>
    /// Helper to create a service with TestSongIndex that captures EditSong calls.
    /// TestSongIndex is created and attached automatically by DanceMusicTester.
    /// </summary>
    private static async Task<DanceMusicService> CreateServiceWithTestIndex(string dbName)
    {
        // Create service with TestSongIndex (DanceMusicTester creates and attaches it)
        var service = await DanceMusicTester.CreateService(dbName, useTestSongIndex: true);
        
        // Add users
        await DanceMusicTester.AddUser(service, "dwgray", false);
        await DanceMusicTester.AddUser(service, "batch", true);
        
        return service;
    }

    #region Real Integration Tests

    [TestMethod]
    public async Task ValidateAndCorrectTempo_NoDances_ReturnsFalse()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_NoDances");
        var song = new Song
        {
            SongId = Guid.NewGuid(),
            Title = "Test Song",
            Artist = "Test Artist",
            Tempo = 100
        };
        // No dance ratings

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsFalse(result, "Should return false when song has no dances");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_MultipleDances_NeitherHasValidationRules_ReturnsFalse()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_MultipleDances");
        var song = new Song
        {
            SongId = Guid.NewGuid(),
            Title = "Test Song",
            Artist = "Test Artist",
            Tempo = 100
        };
        song.DanceRatings.Add(new DanceRating { DanceId = "WLZ", Weight = 1 });
        song.DanceRatings.Add(new DanceRating { DanceId = "CHA", Weight = 1 });

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsFalse(result, "Should return false when neither dance has validation rules");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_NoTempo_ReturnsFalse()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_NoTempo");
        var song = new Song
        {
            SongId = Guid.NewGuid(),
            Title = "Test Song",
            Artist = "Test Artist",
            Tempo = null
        };
        song.DanceRatings.Add(new DanceRating { DanceId = "SLS", Weight = 1 });

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsFalse(result, "Should return false when song has no tempo");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_UnknownDance_ReturnsFalse()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_UnknownDance");
        var song = new Song
        {
            SongId = Guid.NewGuid(),
            Title = "Test Song",
            Artist = "Test Artist",
            Tempo = 100
        };
        song.DanceRatings.Add(new DanceRating { DanceId = "UNKNOWN", Weight = 1 });

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsFalse(result, "Should return false when dance is not found in database");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_NoValidationRules_ReturnsFalse()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_NoValidation");
        
        var song = new Song
        {
            SongId = Guid.NewGuid(),
            Title = "Waltz Song",
            Artist = "Test Artist",
            Tempo = 90m // Valid waltz tempo
        };
        song.DanceRatings.Add(new DanceRating { DanceId = "WLZ", Weight = 1 });

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        // Waltz doesn't have validation rules in the dance database,
        // so validation should return false (no corrections needed)
        Assert.IsFalse(result, "Should return false when dance has no validation rules");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_ValidTempoNoMeter_ReturnsFalse()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_ValidTempo");
        
        // Create a dance with validation rules for testing
        // Note: This test will only work if dances.json has validation rules configured
        // For now, we test the scenario where validation would be skipped
        
        var song = new Song
        {
            SongId = Guid.NewGuid(),
            Title = "Test Song",
            Artist = "Test Artist",
            Tempo = 180m // Valid tempo (assuming Social Salsa range 160-220)
        };
        song.DanceRatings.Add(new DanceRating { DanceId = "SLS", Weight = 1 });
        
        // No meter tag, so validation will return no corrections needed

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        // Without meter information, validation may still run but find no issues
        // The exact behavior depends on whether dances.json has validation rules
        Assert.IsFalse(result, "Should return false when no corrections are needed");
    }

    #endregion

    #region Validation and Correction Tests (Real Tests with TestSongIndex)

    [TestMethod]
    public async Task ValidateAndCorrectTempo_LowTempo_DoublesTo160()
    {
        // Arrange
        var dms = await CreateServiceWithTestIndex("TestDb_LowTempo");
        var testIndex = (TestSongIndex)dms.SongIndex;
        
        // Create song properly using serialized properties (like SongDetailTests)
        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Low Tempo Salsa	Artist=Test Artist	Tempo=80.0	Tag+=Salsa:Dance	DanceRating=SLS+1";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsTrue(result, "Should return true when tempo is corrected");
        Assert.AreEqual(1, testIndex.EditCalls.Count, "EditSong should have been called once");
        
        var call = testIndex.EditCalls[0];
        Assert.AreEqual("tempo-bot", call.User.UserName, "Should use tempo-bot user");
        Assert.IsTrue(call.User.IsPseudo, "tempo-bot should be a pseudo user");
        Assert.AreEqual(160m, call.Edit.Tempo, "Tempo should be doubled from 80 to 160");
        Assert.AreEqual("Low Tempo Salsa", call.Edit.Title, "Title should be preserved");
        Assert.AreEqual("Test Artist", call.Edit.Artist, "Artist should be preserved");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_HighTempo_HalvesTo150()
    {
        // Arrange
        var dms = await CreateServiceWithTestIndex("TestDb_HighTempo");
        var testIndex = (TestSongIndex)dms.SongIndex;
        
        // Create song properly using serialized properties
        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=High Tempo Salsa	Artist=Test Artist	Tempo=300.0	Tag+=Salsa:Dance	DanceRating=SLS+1";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsTrue(result, "Should return true when tempo is corrected");
        Assert.AreEqual(1, testIndex.EditCalls.Count, "EditSong should have been called once");
        
        var call = testIndex.EditCalls[0];
        Assert.AreEqual("tempo-bot", call.User.UserName, "Should use tempo-bot user");
        Assert.AreEqual(150m, call.Edit.Tempo, "Tempo should be halved from 300 to 150");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_MultipleDances_OnlyDanceWithRuleIsCorrected()
    {
        // Arrange: SLS has validation rules and an out-of-range effective tempo (inherited
        // from song.Tempo, no override of its own); CHA has no validation rules at all.
        var dms = await CreateServiceWithTestIndex("TestDb_MultiDance_OnlyOneCorrected");
        var testIndex = (TestSongIndex)dms.SongIndex;

        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Two Dance Salsa	Artist=Test Artist	Tempo=100.0	Tag+=Salsa:Dance|Cha Cha:Dance	DanceRating=SLS+1	DanceRating=CHA+1";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsTrue(result, "Should return true when at least one dance is corrected");
        Assert.AreEqual(1, testIndex.EditCalls.Count, "EditSong should have been called once");

        var slsRating = song.DanceRatings.First(dr => dr.DanceId == "SLS");
        Assert.AreEqual(200m, slsRating.Tempo, "SLS should get its own corrected tempo override");

        var chaRating = song.DanceRatings.First(dr => dr.DanceId == "CHA");
        Assert.IsNull(chaRating.Tempo, "CHA has no validation rules and should be left untouched");

        Assert.AreEqual(
            100m, song.Tempo,
            "Song-level tempo should be unchanged since the dances don't converge on one value");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_MultipleDances_ConvergingCorrections_PromoteSongTempo()
    {
        // Arrange: SLS and QST both have validation rules with the same thresholds, and both
        // inherit the same out-of-range song-level tempo, so both get doubled to the same value.
        // The song's tempo is attributed to the "batch" pseudo user (mirroring a Spotify-style
        // service import) rather than a real user - song-level Tempo has a guard that keeps
        // pseudo users like tempo-bot from silently overwriting a value a real user explicitly
        // set, so promotion wouldn't be observable here if the tempo already carried a real
        // user's fingerprint.
        var dms = await CreateServiceWithTestIndex("TestDb_MultiDance_Converge");
        var testIndex = (TestSongIndex)dms.SongIndex;

        var songData = @".Create=	User=batch|P	Time=00/00/0000 0:00:00 PM	Title=Converging Dances	Artist=Test Artist	Tempo=100.0	Tag+=Salsa:Dance|Quickstep:Dance	DanceRating=SLS+1	DanceRating=QST+1";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsTrue(result, "Should return true when corrections are made");
        Assert.AreEqual(1, testIndex.EditCalls.Count, "EditSong should have been called once");

        var call = testIndex.EditCalls[0];
        Assert.AreEqual(
            200m, call.Edit.Tempo,
            "Song-level tempo should be promoted once every dance converges on 200");

        Assert.AreEqual(200m, song.DanceRatings.First(dr => dr.DanceId == "SLS").Tempo);
        Assert.AreEqual(200m, song.DanceRatings.First(dr => dr.DanceId == "QST").Tempo);
        Assert.AreEqual(200m, song.Tempo, "Song-level tempo should be promoted to match");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_ConvergingCorrections_RealUserTempo_SongLevelNotOverwritten()
    {
        // Arrange: same convergence scenario as above, but the song's tempo was set by a real
        // user (dwgray) rather than a service/pseudo account. Song-level Tempo has a guard
        // (Song.LoadProperties) that keeps pseudo users like tempo-bot from silently
        // overwriting a value a real user explicitly set. Neither SLS nor QST has an explicit
        // per-dance override of its own here (only the song-level tempo was set), so the
        // equivalent per-dance guard never engages and the per-dance corrections still apply.
        var dms = await CreateServiceWithTestIndex("TestDb_MultiDance_ConvergeRealUser");
        var testIndex = (TestSongIndex)dms.SongIndex;

        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Converging Dances Real User	Artist=Test Artist	Tempo=100.0	Tag+=Salsa:Dance|Quickstep:Dance	DanceRating=SLS+1	DanceRating=QST+1";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsTrue(result, "Should return true when corrections are made");
        Assert.AreEqual(1, testIndex.EditCalls.Count, "EditSong should have been called once");

        Assert.AreEqual(200m, song.DanceRatings.First(dr => dr.DanceId == "SLS").Tempo,
            "Per-dance override should still apply regardless of who set the song tempo");
        Assert.AreEqual(200m, song.DanceRatings.First(dr => dr.DanceId == "QST").Tempo,
            "Per-dance override should still apply regardless of who set the song tempo");
        Assert.AreEqual(100m, song.Tempo,
            "Song-level tempo should NOT be overwritten - a real user already set it explicitly");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_RealUserDanceTempoOverride_NotOverwritten()
    {
        // Arrange: a real user (dwgray) has explicitly set SLS's own per-dance tempo override
        // to an out-of-range value. Per-dance Tempo is guarded the same way as song-level
        // Tempo (Song.LoadProperties), so tempo-bot's correction for SLS should be absorbed on
        // replay, while CHA - which has no validation rules - is untouched either way.
        var dms = await CreateServiceWithTestIndex("TestDb_RealUserDanceOverride");
        var testIndex = (TestSongIndex)dms.SongIndex;

        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Explicit Dance Override	Artist=Test Artist	Tag+=Salsa:Dance|Cha Cha:Dance	DanceRating=SLS+1	DanceRating=CHA+1	Tempo:SLS=100.0";
        var song = await Song.Create(songData, dms);

        // Sanity check: the per-dance override promoted the song-level tempo since none was set.
        Assert.AreEqual(100m, song.Tempo);
        Assert.AreEqual(100m, song.DanceRatings.First(dr => dr.DanceId == "SLS").Tempo);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert: EditSong still reports a change (the meter-free tempo correction is attempted
        // and the property gets appended to history), but the real user's explicit SLS override
        // survives the replay unchanged.
        Assert.IsTrue(result, "Should return true - a correction is attempted even though it's absorbed on replay");
        Assert.AreEqual(1, testIndex.EditCalls.Count, "EditSong should have been called once");

        Assert.AreEqual(100m, song.DanceRatings.First(dr => dr.DanceId == "SLS").Tempo,
            "A real user's explicit per-dance override should not be overwritten by tempo-bot");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_BoundaryTempo_120_NoCorrection()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_Boundary120");
        
        // Create song properly using serialized properties
        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Boundary 120 Salsa	Artist=Test Artist	Tempo=120.0	Tag+=Salsa:Dance	DanceRating=SLS+1";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsFalse(result, "Tempo at boundary (120) should not be corrected");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_BoundaryTempo_250_NoCorrection()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_Boundary250");
        
        // Create song properly using serialized properties
        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Boundary 250 Salsa	Artist=Test Artist	Tempo=250.0	Tag+=Salsa:Dance	DanceRating=SLS+1";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsFalse(result, "Tempo at boundary (250) should not be corrected");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_ValidTempo_180_NoCorrection()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_Valid180");
        
        // Create song properly using serialized properties
        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Perfect Tempo Salsa	Artist=Test Artist	Tempo=180.0	Tag+=Salsa:Dance	DanceRating=SLS+1";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsFalse(result, "Valid tempo (180) should not be corrected");
    }

    #endregion

    #region Meter Validation Tests

    [TestMethod]
    public async Task ValidateAndCorrectTempo_ValidMeter_NoCorrection()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_ValidMeter");
        
        // Create song with valid tempo (180) and valid meter (4/4) for Salsa
        // 4/4 is NOT in flagInvalidMeters, so it should be valid
        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Valid Meter Salsa	Artist=Test Artist	Tempo=180.0	Tag+=Salsa:Dance	DanceRating=SLS+1	Tag+:SLS=4/4:Tempo";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsFalse(result, "Should return false when both tempo and meter are valid (no corrections needed)");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_InvalidMeter_AddsCheckAccuracyTag()
    {
        // Arrange
        var dms = await CreateServiceWithTestIndex("TestDb_InvalidMeter");
        var testIndex = (TestSongIndex)dms.SongIndex;
        
        // Create song with valid tempo (180) but invalid meter (3/4) for Salsa
        // 3/4 IS in flagInvalidMeters, so it should trigger a flag
        // NOTE: Meter tag is at song level (Tag+=), not dance level (Tag+:SLS=)
        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Invalid Meter Salsa	Artist=Test Artist	Tempo=180.0	Tag+=3/4:Tempo|Salsa:Dance	DanceRating=SLS+1";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsTrue(result, "Should return true when meter is invalid (flag added)");
        Assert.AreEqual(1, testIndex.EditCalls.Count, "EditSong should have been called once");
        
        var call = testIndex.EditCalls[0];
        Assert.AreEqual("tempo-bot", call.User.UserName, "Should use tempo-bot user");
        Assert.AreEqual(180m, call.Edit.Tempo, "Tempo should not be changed (was already valid)");
        
        // Verify check-accuracy:Tempo tag was added
        Assert.IsNotNull(call.Tags, "Tags should be provided");
        var allTags = call.Tags.SelectMany(ut => ut.Tags.Tags).ToList();
        Assert.IsTrue(allTags.Contains("check-accuracy:Tempo"), 
            $"Should add 'check-accuracy:Tempo' tag for invalid meter. Found tags: {string.Join(", ", allTags)}");
    }

    [TestMethod]
    public async Task ValidateAndCorrectTempo_InvalidMeter_And_InvalidTempo_BothCorrections()
    {
        // Arrange
        var dms = await CreateServiceWithTestIndex("TestDb_InvalidBoth");
        var testIndex = (TestSongIndex)dms.SongIndex;
        
        // Create song with invalid tempo (80) AND invalid meter (3/4) for Salsa
        // NOTE: Meter tag is at song level (Tag+=), not dance level (Tag+:SLS=)
        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Invalid Both Salsa	Artist=Test Artist	Tempo=80.0	Tag+=3/4:Tempo|Salsa:Dance	DanceRating=SLS+1";
        var song = await Song.Create(songData, dms);

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsTrue(result, "Should return true when both tempo and meter need correction");
        Assert.AreEqual(1, testIndex.EditCalls.Count, "EditSong should have been called once");
        
        var call = testIndex.EditCalls[0];
        Assert.AreEqual("tempo-bot", call.User.UserName, "Should use tempo-bot user");
        Assert.AreEqual(160m, call.Edit.Tempo, "Tempo should be doubled from 80 to 160");
        
        // Verify check-accuracy:Tempo tag was added
        Assert.IsNotNull(call.Tags, "Tags should be provided");
        var allTags = call.Tags.SelectMany(ut => ut.Tags.Tags).ToList();
        Assert.IsTrue(allTags.Contains("check-accuracy:Tempo"), 
            $"Should add 'check-accuracy:Tempo' tag for invalid meter. Found tags: {string.Join(", ", allTags)}");
    }

    #endregion
}


