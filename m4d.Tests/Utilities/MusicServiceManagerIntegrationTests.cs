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
    public async Task ValidateAndCorrectTempo_RealService_NoDances_ReturnsFalse()
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
    public async Task ValidateAndCorrectTempo_RealService_MultipleDances_ReturnsFalse()
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
    public async Task ValidateAndCorrectTempo_RealService_NoTempo_ReturnsFalse()
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
    public async Task ValidateAndCorrectTempo_RealService_UnknownDance_ReturnsFalse()
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
    public async Task ValidateAndCorrectTempo_RealService_NoValidationRules_ReturnsFalse()
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
    public async Task ValidateAndCorrectTempo_RealService_ValidTempoNoMeter_ReturnsFalse()
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

    // TODO: Meter validation tests require adding meter tags to songs
    // This is more complex and requires understanding how SongProperties/TagSummary work
    // For now, documenting the expected behavior:
    //
    // [TestMethod]
    // public async Task ValidateAndCorrectTempo_InvalidMeter_AddsCheckAccuracyTag()
    // {
    //     var song = new Song { Tempo = 180m, DanceId = "SLS" };
    //     // Add meter tag: song.SongProperties.Add(new SongProperty { Name = "batch-s", Value = "3/4:Tempo" });
    //     // song.LoadProperties();
    //     
    //     var result = await _manager.ValidateAndCorrectTempo(dms, song);
    //     
    //     Assert.IsTrue(result);
    //     Assert.IsTrue(capturedTags.Any(t => t.Tags.Contains("check-accuracy:Tempo")));
    // }

    #endregion

    #region Future Test Implementation Notes

    // To implement REAL validation/correction tests (not just documentation):
    //
    // 1. **Add Validation Rules to Test Data**
    //    Create test-dances-with-validation.json:
    //    {
    //      "id": "SLS",
    //      "name": "Salsa",
    //      "instances": [{
    //        "style": "Social",
    //        "validation": {
    //          "doubleTempoIfBelow": 120,
    //          "halveTempoIfAbove": 250,
    //          "flagInvalidMeters": ["3/4", "6/8"]
    //        }
    //      }]
    //    }
    //
    // 2. **Mock SongIndex.EditSong**
    //    var mockSongIndex = new Mock<SongIndex>();
    //    Song capturedEdit = null;
    //    ApplicationUser capturedUser = null;
    //    ICollection<UserTag> capturedTags = null;
    //    
    //    mockSongIndex
    //        .Setup(x => x.EditSong(
    //            It.IsAny<ApplicationUser>(),
    //            It.IsAny<Song>(),
    //            It.IsAny<Song>(),
    //            It.IsAny<ICollection<UserTag>>()))
    //        .Callback((ApplicationUser user, Song orig, Song edit, ICollection<UserTag> tags) => {
    //            capturedUser = user;
    //            capturedEdit = edit;
    //            capturedTags = tags;
    //        })
    //        .ReturnsAsync(true);
    //
    // 3. **Inject Mock into Service**
    //    // Need to make DanceMusicCoreService.SongIndex settable or use constructor injection
    //    var service = await DanceMusicTester.CreateService("TestDb");
    //    service.SetSongIndex(mockSongIndex.Object); // Would need this method
    //
    // 4. **Verify Corrections**
    //    Assert.IsTrue(result);
    //    Assert.IsNotNull(capturedUser);
    //    Assert.AreEqual("tempo-bot", capturedUser.UserName);
    //    Assert.AreEqual(160m, capturedEdit.Tempo);
    //    Assert.IsTrue(capturedTags.Any(t => t.Tags.Contains("check-accuracy:Tempo")));

    #endregion
}


