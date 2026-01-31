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
    /// </summary>
    private static async Task<(DanceMusicService service, TestSongIndex songIndex)> CreateServiceWithTestIndex(string dbName)
    {
        // Create a temporary service first
        var tempService = await DanceMusicTester.CreateService(dbName + "_temp");
        
        // Create TestSongIndex with the temp service
        var testIndex = new TestSongIndex(tempService, dbName);
        
        // Create the real service with our TestSongIndex
        var service = await DanceMusicTester.CreateService(dbName, customSongIndex: testIndex);
        
        // Add users
        await DanceMusicTester.AddUser(service, "dwgray", false);
        await DanceMusicTester.AddUser(service, "batch", true);
        
        return (service, testIndex);
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
    public async Task ValidateAndCorrectTempo_MultipleDances_ReturnsFalse()
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
        song.DanceRatings.Add(new DanceRating { DanceId = "SLS", Weight = 1 });
        song.DanceRatings.Add(new DanceRating { DanceId = "CHA", Weight = 1 });

        // Act
        var result = await _manager.ValidateAndCorrectTempo(dms, song);

        // Assert
        Assert.IsFalse(result, "Should return false when song has multiple dances");
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
        var (dms, testIndex) = await CreateServiceWithTestIndex("TestDb_LowTempo");
        
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
        var (dms, testIndex) = await CreateServiceWithTestIndex("TestDb_HighTempo");
        
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
        var (dms, testIndex) = await CreateServiceWithTestIndex("TestDb_InvalidMeter");
        
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
        var (dms, testIndex) = await CreateServiceWithTestIndex("TestDb_InvalidBoth");
        
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


