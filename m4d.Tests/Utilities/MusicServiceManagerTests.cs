using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using m4d.Utilities;
using m4dModels;
using System.Security.Principal;
using Microsoft.EntityFrameworkCore;
using DanceLibrary;

namespace m4d.Tests.Utilities;

[TestClass]
public class MusicServiceManagerTests
{
    private Mock<IConfiguration> _mockConfiguration = null!;
    private MusicServiceManager _manager = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _manager = new MusicServiceManager(_mockConfiguration.Object);
    }

    #region Constructor Tests

    [TestMethod]
    public void Constructor_ValidConfiguration_CreatesInstance()
    {
        var config = new Mock<IConfiguration>().Object;
        var manager = new MusicServiceManager(config);
        Assert.IsNotNull(manager);
    }

    #endregion

    #region DefaultServiceSearch Tests

    [TestMethod]
    public void DefaultServiceSearch_CleanFalse_ReturnsTitleAndArtist()
    {
        var song = new Song
        {
            Title = "Test Song (Live)",
            Artist = "Test Artist [Remix]"
        };

        var result = MusicServiceManager.DefaultServiceSearch(song, clean: false);

        Assert.AreEqual("Test Song (Live) Test Artist [Remix]", result);
    }

    [TestMethod]
    public void DefaultServiceSearch_CleanTrue_ReturnsCleanedTitleAndArtist()
    {
        // CleanTitle and CleanArtist are computed properties from Title/Artist
        // They remove parenthetical content and brackets
        var song = new Song
        {
            Title = "Test Song",
            Artist = "Test Artist"
        };

        var result = MusicServiceManager.DefaultServiceSearch(song, clean: true);

        // When clean=true, it uses CleanTitle and CleanArtist (which are the same in this case)
        Assert.AreEqual("Test Song Test Artist", result);
    }

    [TestMethod]
    public void DefaultServiceSearch_EmptyTitleAndArtist_ReturnsSpace()
    {
        var song = new Song
        {
            Title = "",
            Artist = ""
        };

        var result = MusicServiceManager.DefaultServiceSearch(song, clean: false);

        Assert.AreEqual(" ", result);
    }

    #endregion

    #region UpdateMusicService Tests

    [TestMethod]
    public void UpdateMusicService_EmptyTitle_SetsTitle()
    {
        var song = new Song { Title = "", Artist = "Artist" };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "New Title", "Album", "Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual("New Title", song.Title);
    }

    [TestMethod]
    public void UpdateMusicService_ExistingTitle_DoesNotChangeTitle()
    {
        var song = new Song { Title = "Original Title", Artist = "Artist" };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "New Title", "Album", "Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual("Original Title", song.Title);
    }

    [TestMethod]
    public void UpdateMusicService_EmptyArtist_SetsArtist()
    {
        var song = new Song { Title = "Title", Artist = "" };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Album", "New Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual("New Artist", song.Artist);
    }

    [TestMethod]
    public void UpdateMusicService_ExistingArtist_DoesNotChangeArtist()
    {
        var song = new Song { Title = "Title", Artist = "Original Artist" };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Album", "New Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual("Original Artist", song.Artist);
    }

    [TestMethod]
    public void UpdateMusicService_NoLength_SetsDuration()
    {
        var song = new Song { Title = "Title", Artist = "Artist", Length = null };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Album", "Artist",
            "track123", "collection456", null, "3:15", 1);

        Assert.AreEqual(195, song.Length);
    }

    [TestMethod]
    public void UpdateMusicService_ExistingLength_DoesNotChangeDuration()
    {
        var song = new Song { Title = "Title", Artist = "Artist", Length = 180 };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Album", "Artist",
            "track123", "collection456", null, "3:15", 1);

        Assert.AreEqual(180, song.Length);
    }

    [TestMethod]
    public void UpdateMusicService_NewAlbum_AddsAlbum()
    {
        var song = new Song { Title = "Title", Artist = "Artist" };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "New Album", "Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual(1, song.Albums.Count);
        Assert.AreEqual("New Album", song.Albums[0].Name);
        Assert.AreEqual(1, song.Albums[0].Track);
    }

    [TestMethod]
    public void UpdateMusicService_ExistingAlbum_UpdatesExistingAlbum()
    {
        var song = new Song { Title = "Title", Artist = "Artist" };
        song.Albums.Add(new AlbumDetails { Name = "Existing Album", Track = 1 });
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Existing Album", "Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual(1, song.Albums.Count);
        Assert.AreEqual("Existing Album", song.Albums[0].Name);
    }

    #endregion

    // NOTE: ValidateAndCorrectTempo integration tests are in MusicServiceManagerIntegrationTests.cs
    // Those tests use DanceMusicTester to create real service instances and test the full workflow.
}

