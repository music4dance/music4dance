using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Comprehensive tests for MergeManager covering all merge candidate selection levels,
/// AutoMerge functionality, .NoMerge filtering, and edge cases.
/// </summary>
[TestClass]
public class MergeManagerTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await DanceMusicTester.LoadDances();
    }

    #region GetMergeCandidates Tests

    [TestMethod]
    public async Task GetMergeCandidates_Level0_FindsEquivalentSongs()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_Merge_Level0", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        // Create 3 songs: 2 equivalent (same title, artist, tempo, length), 1 different
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Test Song	Artist=Test Artist	Tempo=120.0	Length=180";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Test Song	Artist=Test Artist	Tempo=120.0	Length=180";
        var song3Data = @".Create=	User=dwgray	Time=01/03/2020 12:00:00 PM	Title=Test Song	Artist=Test Artist	Tempo=125.0	Length=180";

        await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);
        await CreateAndSaveSong(song3Data, dms);

        // Act
        var candidates = await dms.MergeManager.GetMergeCandidates(n: 100, level: 0);

        // Assert
        Assert.AreEqual(2, candidates.Count, "Level 0 should find 2 equivalent songs (same title, artist, tempo, length)");
        Assert.IsTrue(candidates.All(s => s.Title == "Test Song"));
        Assert.IsTrue(candidates.All(s => s.Tempo == 120.0m));
    }

    [TestMethod]
    public async Task GetMergeCandidates_Level1_FindsWeakEquivalentSongs()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_Merge_Level1", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        // Create 3 songs: 2 with same title/artist (weak equivalent), 1 with different artist
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Shape of You	Artist=Ed Sheeran	Tempo=120.0	Length=180";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Shape of You	Artist=Ed Sheeran	Tempo=120.0	Length=182";
        var song3Data = @".Create=	User=dwgray	Time=01/03/2020 12:00:00 PM	Title=Shape of You	Artist=Different Artist	Tempo=120.0	Length=180";

        await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);
        await CreateAndSaveSong(song3Data, dms);

        // Act
        var candidates = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);

        // Assert
        Assert.AreEqual(2, candidates.Count, "Level 1 should find 2 songs with same title and artist");
        Assert.IsTrue(candidates.All(s => s.Artist == "Ed Sheeran"));
    }

    [TestMethod]
    public async Task GetMergeCandidates_Level2_FindsAllSimilarTitles()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_Merge_Level2", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        // Create 3 songs with same title but different artists
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Dancing Queen	Artist=ABBA	Tempo=120.0";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Dancing Queen	Artist=Cover Artist	Tempo=125.0";
        var song3Data = @".Create=	User=dwgray	Time=01/03/2020 12:00:00 PM	Title=Dancing Queen	Artist=Another Cover	Tempo=130.0";

        await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);
        await CreateAndSaveSong(song3Data, dms);

        // Act
        var candidates = await dms.MergeManager.GetMergeCandidates(n: 100, level: 2);

        // Assert
        Assert.AreEqual(3, candidates.Count, "Level 2 should find all songs with similar title regardless of artist");
        Assert.IsTrue(candidates.All(s => s.Title == "Dancing Queen"));
    }

    [TestMethod]
    public async Task GetMergeCandidates_Level3_FiltersLengthDivergence()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_Merge_Level3", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        // Create 3 songs with same title/artist but varying lengths
        // Song 1 & 2 within 20s, Song 3 outside 20s range
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Test Song	Artist=Artist	Tempo=120.0	Length=180";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Test Song	Artist=Artist	Tempo=120.0	Length=195";
        var song3Data = @".Create=	User=dwgray	Time=01/03/2020 12:00:00 PM	Title=Test Song	Artist=Artist	Tempo=120.0	Length=300";

        await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);
        await CreateAndSaveSong(song3Data, dms);

        // Act
        var candidates = await dms.MergeManager.GetMergeCandidates(n: 100, level: 3);

        // Assert
        Assert.AreEqual(2, candidates.Count, "Level 3 should filter out songs with length divergence > 20s");
        Assert.IsTrue(candidates.All(s => s.Length <= 195), "Should only include songs with similar lengths");
    }

    [TestMethod]
    public async Task GetMergeCandidates_EmptyArtist_IncludesAllSongsInCluster()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_Merge_EmptyArtist", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        // Create 3 songs with same title: 2 with artists, 1 without
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Unknown Song	Artist=Artist One	Tempo=120.0";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Unknown Song	Artist=Artist Two	Tempo=125.0";
        var song3Data = @".Create=	User=dwgray	Time=01/03/2020 12:00:00 PM	Title=Unknown Song	Tempo=130.0";

        await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);
        await CreateAndSaveSong(song3Data, dms);

        // Act
        var candidates = await dms.MergeManager.GetMergeCandidates(n: 100, level: 2);

        // Assert
        Assert.AreEqual(3, candidates.Count, "When any song has empty artist, should include all songs with same title");
    }

    [TestMethod]
    public async Task GetMergeCandidates_UsesCache_WhenSameLevelRequested()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_Merge_Cache", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Cached Song	Artist=Artist	Tempo=120.0";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Cached Song	Artist=Artist	Tempo=120.0";

        await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);

        // Act - First call
        var candidates1 = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);
        
        // Add another song
        var song3Data = @".Create=	User=dwgray	Time=01/03/2020 12:00:00 PM	Title=Cached Song	Artist=Artist	Tempo=120.0";
        await CreateAndSaveSong(song3Data, dms);

        // Act - Second call (should use cache)
        var candidates2 = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);

        // Assert
        Assert.AreEqual(candidates1.Count, candidates2.Count, "Should return cached results");
        Assert.IsTrue(ReferenceEquals(candidates1, candidates2), "Should be the same instance (cached)");
    }

    [TestMethod]
    public async Task GetMergeCandidates_ClearCache_InvalidatesCache()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_Merge_ClearCache", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Test Song	Artist=Artist	Tempo=120.0";
        await CreateAndSaveSong(song1Data, dms);

        // First call to populate cache
        var candidates1 = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);

        // Act - Clear cache
        dms.MergeManager.ClearMergeCandidateCache();

        // Add another song
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Test Song	Artist=Artist	Tempo=120.0";
        await CreateAndSaveSong(song2Data, dms);

        // Second call should rebuild cache
        var candidates2 = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);

        // Assert
        Assert.AreEqual(2, candidates2.Count, "Should have refreshed cache with new song");
        Assert.IsFalse(ReferenceEquals(candidates1, candidates2), "Should be different instances (cache cleared)");
    }

    [TestMethod]
    public async Task GetMergeCandidates_MaxN_LimitsResults()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_Merge_MaxN", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        // Create 5 pairs of songs (10 total candidates)
        for (int i = 0; i < 5; i++)
        {
            var song1Data = $@".Create=	User=dwgray	Time=01/0{i + 1}/2020 10:00:00 AM	Title=Song {i}	Artist=Artist	Tempo=120.0";
            var song2Data = $@".Create=	User=dwgray	Time=01/0{i + 1}/2020 11:00:00 AM	Title=Song {i}	Artist=Artist	Tempo=120.0";
            await CreateAndSaveSong(song1Data, dms);
            await CreateAndSaveSong(song2Data, dms);
        }

        // Act - Request only 4 songs
        var candidates = await dms.MergeManager.GetMergeCandidates(n: 4, level: 1);

        // Assert
        Assert.IsTrue(candidates.Count <= 4, "Should respect max n limit");
    }

    #endregion

    #region AutoMerge Tests

    [TestMethod]
    public async Task AutoMerge_Level1_MergesEquivalentClusters()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_AutoMerge_Level1", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");
        var botUser = new ApplicationUser("test-bot", true);

        // Create 4 songs: 2 pairs of equivalent songs
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Song A	Artist=Artist	Tempo=120.0	Length=180";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Song A	Artist=Artist	Tempo=120.0	Length=180";
        var song3Data = @".Create=	User=dwgray	Time=01/03/2020 12:00:00 PM	Title=Song B	Artist=Artist	Tempo=130.0	Length=200";
        var song4Data = @".Create=	User=dwgray	Time=01/04/2020 01:00:00 PM	Title=Song B	Artist=Artist	Tempo=130.0	Length=200";

        await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);
        await CreateAndSaveSong(song3Data, dms);
        await CreateAndSaveSong(song4Data, dms);

        var candidates = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);

        // Act
        var merged = await dms.MergeManager.AutoMerge(candidates, level: 1, user: botUser);

        // Assert
        Assert.AreEqual(2, merged.Count, "Should create 2 merged songs (one per cluster)");
        Assert.IsTrue(merged.All(s => s.SongProperties.Any(p => p.Name == Song.MergeCommand)), 
            "All merged songs should have .Merge command");
    }

    [TestMethod]
    public async Task AutoMerge_WithNoMerge_ExcludesSong()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_AutoMerge_NoMerge", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");
        var botUser = new ApplicationUser("test-bot", true);

        // Create 2 equivalent songs, add .NoMerge to one
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=No Merge Test	Artist=Artist	Tempo=120.0	Length=180";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=No Merge Test	Artist=Artist	Tempo=120.0	Length=180	.NoMerge=	User=admin	Time=01/02/2020 12:00:00 PM";

        var song1 = await CreateAndSaveSong(song1Data, dms);
        var song2 = await CreateAndSaveSong(song2Data, dms);

        var candidates = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);

        // Act
        var merged = await dms.MergeManager.AutoMerge(candidates, level: 1, user: botUser);

        // Assert
        Assert.AreEqual(0, merged.Count, "Should not merge when .NoMerge is present");
    }

    [TestMethod]
    public async Task AutoMerge_FinalCluster_IsMerged()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_AutoMerge_FinalCluster", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");
        var botUser = new ApplicationUser("test-bot", true);

        // Create 2 songs that will be in the final cluster
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Final Song	Artist=Artist	Tempo=120.0	Length=180";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Final Song	Artist=Artist	Tempo=120.0	Length=180";

        await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);

        var candidates = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);

        // Act
        var merged = await dms.MergeManager.AutoMerge(candidates, level: 1, user: botUser);

        // Assert
        Assert.AreEqual(1, merged.Count, "Should merge the final cluster");
        Assert.IsNotNull(merged.First().SongProperties.FirstOrDefault(p => p.Name == Song.MergeCommand),
            "Merged song should have .Merge command");
    }

    [TestMethod]
    public async Task AutoMerge_SingleSongCluster_DoesNotMerge()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_AutoMerge_SingleSong", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");
        var botUser = new ApplicationUser("test-bot", true);

        // Create single songs (no duplicates)
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Unique Song A	Artist=Artist	Tempo=120.0";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Unique Song B	Artist=Artist	Tempo=130.0";

        await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);

        // Manually create candidate list (since GetMergeCandidates won't return singles)
        var song1 = await dms.SongIndex.FindSongs(new[] { (await CreateAndSaveSong(song1Data, dms)).SongId });
        var candidates = song1.ToList();

        // Act
        var merged = await dms.MergeManager.AutoMerge(candidates, level: 1, user: botUser);

        // Assert
        Assert.AreEqual(0, merged.Count, "Should not merge single songs");
    }

    [TestMethod]
    public async Task AutoMerge_AnnotatesWithBotUser()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_AutoMerge_BotUser", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");
        var botUser = new ApplicationUser("automerge", true);

        // Create 2 equivalent songs
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Test Song	Artist=Artist	Tempo=120.0";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Test Song	Artist=Artist	Tempo=120.0";

        await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);

        var candidates = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);

        // Act
        var merged = await dms.MergeManager.AutoMerge(candidates, level: 1, user: botUser);

        // Assert
        Assert.AreEqual(1, merged.Count);
        var mergedSong = merged.First();
        
        // Check for bot user in merge history
        var userProps = mergedSong.SongProperties.Where(p => p.Name == Song.UserField).ToList();
        Assert.IsTrue(userProps.Any(p => p.Value.Contains("automerge")), 
            "Merged song should be attributed to automerge bot user");
    }

    [TestMethod]
    public async Task RemoveMergeCandidate_RemovesFromCache()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_RemoveCandidate", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Test Song	Artist=Artist	Tempo=120.0";
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Test Song	Artist=Artist	Tempo=120.0";

        var song1 = await CreateAndSaveSong(song1Data, dms);
        await CreateAndSaveSong(song2Data, dms);

        var candidates = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);
        Assert.AreEqual(2, candidates.Count);

        // Act
        dms.MergeManager.RemoveMergeCandidate(song1);

        // Get cached results (should use cache)
        var candidatesAfter = await dms.MergeManager.GetMergeCandidates(n: 100, level: 1);

        // Assert
        Assert.AreEqual(1, candidatesAfter.Count, "Should have removed song from cache");
        Assert.IsFalse(candidatesAfter.Any(s => s.SongId == song1.SongId), "Removed song should not be in cache");
    }

    #endregion

    #region Helper Methods

    private static async Task<Song> CreateAndSaveSong(string songData, DanceMusicCoreService dms)
    {
        var song = await Song.Create(songData, dms);
        await dms.SongIndex.SaveSong(song);
        return song;
    }

    #endregion
}
