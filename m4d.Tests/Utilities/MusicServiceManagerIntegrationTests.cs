using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using m4d.Utilities;
using m4dModels;
using m4dModels.Tests;
using DanceLibrary;

namespace m4d.Tests.Utilities;

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
        // Load the dance database once for all tests
        await DanceMusicTester.LoadDances();
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
}
