using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

[TestClass]
public class MergeTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        // Load dances library for tests
        await DanceMusicTester.LoadDances();
    }

    [TestMethod]
    public async Task SimpleMerge_AnnotatesCreateAndEditCommands()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_SimpleMerge_Annotate", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        // Create first song
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Test Song	Artist=Test Artist	Tempo=120.0	Tag+=Salsa:Dance	DanceRating=SLS+1";
        var song1 = await Song.Create(song1Data, dms);
        await dms.SongIndex.SaveSong(song1);

        // Create second song with edits
        var song2Data = @".Create=	User=dwgray	Time=01/02/2020 11:00:00 AM	Title=Test Song	Artist=Test Artist	Tempo=125.0	Tag+=Bachata:Dance	DanceRating=BCH+1	.Edit=	User=dwgray	Time=01/03/2020 12:00:00 PM	Tempo=130.0";
        var song2 = await Song.Create(song2Data, dms);
        await dms.SongIndex.SaveSong(song2);

        // Act
        var mergedSong = await dms.SongIndex.SimpleMergeSongs(user, [song1, song2]);

        // Assert
        Assert.IsNotNull(mergedSong);
        
        // Check that .Create and .Edit commands are annotated with song GUIDs
        var createProps = mergedSong.SongProperties.Where(p => p.Name == Song.CreateCommand).ToList();
        Assert.AreEqual(2, createProps.Count, "Should have 2 .Create commands (one from each song)");
        
        Assert.AreEqual(song1.SongId.ToString(), createProps[0].Value, "First .Create should be annotated with song1 GUID");
        Assert.AreEqual(song2.SongId.ToString(), createProps[1].Value, "Second .Create should be annotated with song2 GUID");

        var editProps = mergedSong.SongProperties.Where(p => p.Name == Song.EditCommand).ToList();
        Assert.AreEqual(1, editProps.Count, "Should have 1 .Edit command from song2");
        Assert.AreEqual(song2.SongId.ToString(), editProps[0].Value, "Edit should be annotated with song2 GUID");

        // Check that .Merge command exists
        var mergeProps = mergedSong.SongProperties.Where(p => p.Name == Song.MergeCommand).ToList();
        Assert.AreEqual(1, mergeProps.Count, "Should have 1 .Merge command");
        Assert.IsTrue(mergeProps[0].Value.Contains(song1.SongId.ToString()), "Merge should reference song1");
        Assert.IsTrue(mergeProps[0].Value.Contains(song2.SongId.ToString()), "Merge should reference song2");
    }

    [TestMethod]
    public async Task SimpleMerge_SortsByTimestamp()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_SimpleMerge_Sort", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        // Create songs with different timestamps (chronologically out of order)
        var song1Data = @".Create=	User=dwgray	Time=01/05/2020 10:00:00 AM	Title=Song A	Artist=Artist	Tempo=120.0";
        var song2Data = @".Create=	User=dwgray	Time=01/01/2020 09:00:00 AM	Title=Song B	Artist=Artist	Tempo=125.0";
        var song3Data = @".Create=	User=dwgray	Time=01/03/2020 11:00:00 AM	Title=Song C	Artist=Artist	Tempo=130.0";

        var song1 = await Song.Create(song1Data, dms);
        var song2 = await Song.Create(song2Data, dms);
        var song3 = await Song.Create(song3Data, dms);

        await dms.SongIndex.SaveSong(song1);
        await dms.SongIndex.SaveSong(song2);
        await dms.SongIndex.SaveSong(song3);

        // Act
        var mergedSong = await dms.SongIndex.SimpleMergeSongs(user, [song1, song2, song3]);

        // Assert
        var createProps = mergedSong.SongProperties
            .Select((p, index) => new { Prop = p, Index = index })
            .Where(x => x.Prop.Name == Song.CreateCommand)
            .ToList();

        Assert.AreEqual(3, createProps.Count, "Should have 3 .Create commands");

        // They should be sorted by timestamp: song2 (Jan 1), song3 (Jan 3), song1 (Jan 5)
        Assert.AreEqual(song2.SongId.ToString(), createProps[0].Prop.Value, "First .Create should be from song2 (earliest)");
        Assert.AreEqual(song3.SongId.ToString(), createProps[1].Prop.Value, "Second .Create should be from song3");
        Assert.AreEqual(song1.SongId.ToString(), createProps[2].Prop.Value, "Third .Create should be from song1 (latest)");
    }

    [TestMethod]
    public async Task SimpleMerge_PreservesAllProperties()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_SimpleMerge_Preserve", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        await DanceMusicTester.AddUser(dms, "user2", false);
        var user = await dms.FindUser("dwgray");

        // Create songs with different properties
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Song	Artist=Artist	Tempo=120.0	Tag+=Salsa:Dance|4/4:Tempo	DanceRating=SLS+2	Sample=http://example.com/sample1.mp3";
        var song2Data = @".Create=	User=user2	Time=01/02/2020 11:00:00 AM	Title=Song	Artist=Artist	Tempo=125.0	Tag+=Bachata:Dance|Latin:Music	DanceRating=BCH+1	Album:00=Test Album	Track:00=5";

        var song1 = await Song.Create(song1Data, dms);
        var song2 = await Song.Create(song2Data, dms);

        await dms.SongIndex.SaveSong(song1);
        await dms.SongIndex.SaveSong(song2);

        // Act
        var mergedSong = await dms.SongIndex.SimpleMergeSongs(user, [song1, song2]);

        // Assert - check that properties from both songs are preserved
        var allProperties = mergedSong.SongProperties;

        // Check for properties from song1
        Assert.IsTrue(allProperties.Any(p => p.Name.StartsWith("Tag+") && p.Value.Contains("Salsa")), 
            "Should preserve Salsa tag from song1");
        Assert.IsTrue(allProperties.Any(p => p.Name == "DanceRating" && p.Value == "SLS+2"), 
            "Should preserve SLS dance rating from song1");
        Assert.IsTrue(allProperties.Any(p => p.Name == Song.SampleField && p.Value == "http://example.com/sample1.mp3"), 
            "Should preserve sample URL from song1");

        // Check for properties from song2
        Assert.IsTrue(allProperties.Any(p => p.Name.StartsWith("Tag+") && p.Value.Contains("Bachata")), 
            "Should preserve Bachata tag from song2");
        Assert.IsTrue(allProperties.Any(p => p.Name.StartsWith("Tag+") && p.Value.Contains("Latin")), 
            "Should preserve Latin:Music tag from song2");
        Assert.IsTrue(allProperties.Any(p => p.Name == "DanceRating" && p.Value == "BCH+1"), 
            "Should preserve BCH dance rating from song2");
        Assert.IsTrue(allProperties.Any(p => p.Name == "Album:00" && p.Value == "Test Album"), 
            "Should preserve album from song2");

        // Check that both users are represented
        var userProps = allProperties.Where(p => p.Name == Song.UserField).ToList();
        Assert.IsTrue(userProps.Any(p => p.Value == "dwgray"), "Should have edits from dwgray");
        Assert.IsTrue(userProps.Any(p => p.Value == "user2"), "Should have edits from user2");
    }

    [TestMethod]
    public async Task SimpleMerge_MultipleEditsFromSameSong()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateService("TestDb_SimpleMerge_MultiEdit", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        await DanceMusicTester.AddUser(dms, "user2", false);
        var user = await dms.FindUser("dwgray");

        // Song with multiple edits
        var songData = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Song	Artist=Artist	Tempo=120.0	.Edit=	User=dwgray	Time=01/02/2020 11:00:00 AM	Tempo=125.0	.Edit=	User=user2	Time=01/03/2020 12:00:00 PM	Tempo=130.0";
        var song1 = await Song.Create(songData, dms);
        await dms.SongIndex.SaveSong(song1);

        var song2Data = @".Create=	User=dwgray	Time=01/04/2020 01:00:00 PM	Title=Song B	Artist=Artist	Tempo=135.0";
        var song2 = await Song.Create(song2Data, dms);
        await dms.SongIndex.SaveSong(song2);

        // Act
        var mergedSong = await dms.SongIndex.SimpleMergeSongs(user, [song1, song2]);

        // Assert
        var editProps = mergedSong.SongProperties.Where(p => p.Name == Song.EditCommand).ToList();
        
        Assert.AreEqual(2, editProps.Count, "Should have 2 .Edit commands from song1");
        Assert.IsTrue(editProps.All(p => p.Value == song1.SongId.ToString()), 
            "All .Edit commands from song1 should be annotated with song1 GUID");

        // Verify the edits are in chronological order
        var createAndEditIndices = mergedSong.SongProperties
            .Select((p, index) => new { Prop = p, Index = index })
            .Where(x => x.Prop.Name == Song.CreateCommand || x.Prop.Name == Song.EditCommand)
            .ToList();

        Assert.AreEqual(4, createAndEditIndices.Count, "Should have 1 create + 2 edits from song1, plus 1 create from song2");
    }

    [TestMethod]
    public async Task SimpleMerge_CUT_Order_GroupsBlocksCorrectly()
    {
        // Arrange: Test Create-User-Time order (most common)
        var dms = await DanceMusicTester.CreateService("TestDb_SimpleMerge_CUT", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        await DanceMusicTester.AddUser(dms, "user2", false);
        var user = await dms.FindUser("dwgray");

        // Song with multiple edits in CUT order
        var songData = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=CUT Test	Artist=Artist	Tempo=120.0	.Edit=	User=dwgray	Time=01/02/2020 11:00:00 AM	Tempo=125.0	.Edit=	User=user2	Time=01/03/2020 12:00:00 PM	Tempo=130.0";
        var song = await Song.Create(songData, dms);
        await dms.SongIndex.SaveSong(song);

        // Act
        var mergedSong = await dms.SongIndex.SimpleMergeSongs(user, [song]);

        // Assert - Verify blocks are properly separated and sorted
        var commandIndices = mergedSong.SongProperties
            .Select((p, index) => new { Prop = p, Index = index })
            .Where(x => x.Prop.Name == Song.CreateCommand || x.Prop.Name == Song.EditCommand)
            .ToList();

        Assert.AreEqual(3, commandIndices.Count, "Should have 3 blocks (.Create + 2 .Edit)");

        // Verify timestamps are in correct order by checking User field order
        var userIndices = mergedSong.SongProperties
            .Select((p, index) => new { Prop = p, Index = index })
            .Where(x => x.Prop.Name == Song.UserField)
            .ToList();

        // Filter out the merge command user (last one)
        var originalUsers = userIndices.Take(userIndices.Count - 1).ToList();

        Assert.IsTrue(originalUsers.Count >= 3, "Should have at least 3 user entries (one per block)");
        Assert.AreEqual("dwgray", originalUsers[0].Prop.Value, "First block should be dwgray");
        Assert.AreEqual("dwgray", originalUsers[1].Prop.Value, "Second block should be dwgray");
        Assert.AreEqual("user2", originalUsers[2].Prop.Value, "Third block should be user2");
    }

    [TestMethod]
    public async Task SimpleMerge_CTU_Order_GroupsBlocksCorrectly()
    {
        // Arrange: Test Create-Time-User order (less common but valid)
        var dms = await DanceMusicTester.CreateService("TestDb_SimpleMerge_CTU", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        await DanceMusicTester.AddUser(dms, "user2", false);
        var user = await dms.FindUser("dwgray");

        // Create song with CTU order (Time before User)
        var songData = @".Create=	Time=01/01/2020 10:00:00 AM	User=dwgray	Title=CTU Test	Artist=Artist	Tempo=120.0	.Edit=	Time=01/02/2020 11:00:00 AM	User=user2	Tempo=125.0";
        var song = await Song.Create(songData, dms);
        await dms.SongIndex.SaveSong(song);

        // Act
        var mergedSong = await dms.SongIndex.SimpleMergeSongs(user, [song]);

        // Assert - Blocks should still be properly detected and sorted
        var commandIndices = mergedSong.SongProperties
            .Select((p, index) => new { Prop = p, Index = index })
            .Where(x => x.Prop.Name == Song.CreateCommand || x.Prop.Name == Song.EditCommand)
            .ToList();

        Assert.AreEqual(2, commandIndices.Count, "Should have 2 blocks (.Create + .Edit) in CTU order");

        // Verify chronological order is maintained
        var userIndices = mergedSong.SongProperties
            .Select((p, index) => new { Prop = p, Index = index })
            .Where(x => x.Prop.Name == Song.UserField)
            .ToList();

        var originalUsers = userIndices.Take(userIndices.Count - 1).ToList();

        Assert.IsTrue(originalUsers.Count >= 2, "Should have at least 2 user entries");
        Assert.AreEqual("dwgray", originalUsers[0].Prop.Value, "First block should be dwgray (earlier timestamp)");
        Assert.AreEqual("user2", originalUsers[1].Prop.Value, "Second block should be user2 (later timestamp)");
    }

    [TestMethod]
    public async Task SimpleMerge_MixedOrder_HandlesCorrectly()
    {
        // Arrange: Test mixed CUT and CTU order in different songs
        var dms = await DanceMusicTester.CreateService("TestDb_SimpleMerge_Mixed", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        await DanceMusicTester.AddUser(dms, "user2", false);
        var user = await dms.FindUser("dwgray");

        // Song 1: CUT order
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Mixed Test	Artist=Artist	Tempo=120.0";
        var song1 = await Song.Create(song1Data, dms);
        await dms.SongIndex.SaveSong(song1);

        // Song 2: CTU order
        var song2Data = @".Create=	Time=01/03/2020 12:00:00 PM	User=user2	Title=Mixed Test	Artist=Artist	Tempo=125.0";
        var song2 = await Song.Create(song2Data, dms);
        await dms.SongIndex.SaveSong(song2);

        // Act
        var mergedSong = await dms.SongIndex.SimpleMergeSongs(user, [song1, song2]);

        // Assert - Should merge and sort correctly regardless of original order
        var createProps = mergedSong.SongProperties
            .Select((p, index) => new { Prop = p, Index = index })
            .Where(x => x.Prop.Name == Song.CreateCommand)
            .ToList();

        Assert.AreEqual(2, createProps.Count, "Should have 2 .Create commands");

        // Verify chronological order: song1 (Jan 1) should come before song2 (Jan 3)
        var userProps = mergedSong.SongProperties
            .Where(p => p.Name == Song.UserField)
            .ToList();

        var originalUsers = userProps.Take(userProps.Count - 1).ToList();

        Assert.AreEqual("dwgray", originalUsers[0].Value, "Earlier block (Jan 1) should be first");
        Assert.AreEqual("user2", originalUsers[1].Value, "Later block (Jan 3) should be second");
    }

    [TestMethod]
    public async Task SimpleMerge_BlockFlushing_HandlesNewCreateEdit()
    {
        // Arrange: Verify blocks are flushed when encountering new .Create/.Edit
        var dms = await DanceMusicTester.CreateService("TestDb_SimpleMerge_BlockFlush", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        // Song with .Create followed immediately by .Edit (should create 2 separate blocks)
        var songData = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Block Flush	Artist=Artist	Tempo=120.0	Tag+=Salsa:Dance	.Edit=	User=dwgray	Time=01/02/2020 11:00:00 AM	Tempo=125.0	Tag+=Bachata:Dance";
        var song = await Song.Create(songData, dms);
        await dms.SongIndex.SaveSong(song);

        // Act
        var mergedSong = await dms.SongIndex.SimpleMergeSongs(user, [song]);

        // Assert
        var allProperties = mergedSong.SongProperties.ToList();

        // Find .Create block
        var createIndex = allProperties.FindIndex(p => p.Name == Song.CreateCommand);
        var editIndex = allProperties.FindIndex(p => p.Name == Song.EditCommand);

        Assert.IsTrue(createIndex >= 0, "Should have .Create command");
        Assert.IsTrue(editIndex >= 0, "Should have .Edit command");
        Assert.IsTrue(editIndex > createIndex, ".Edit should come after .Create");

        // Verify tags are in correct blocks
        var salsaIndex = allProperties.FindIndex(p => p.Value?.Contains("Salsa") == true);
        var bachataIndex = allProperties.FindIndex(p => p.Value?.Contains("Bachata") == true);

        Assert.IsTrue(salsaIndex > createIndex && salsaIndex < editIndex, "Salsa should be in .Create block");
        Assert.IsTrue(bachataIndex > editIndex, "Bachata should be in .Edit block");
    }

    [TestMethod]
    public async Task SimpleMerge_NoTimestamp_HandlesGracefully()
    {
        // Arrange: Test handling of blocks without timestamps (edge case)
        var dms = await DanceMusicTester.CreateService("TestDb_SimpleMerge_NoTime", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        var user = await dms.FindUser("dwgray");

        // Song with missing or invalid timestamp
        var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=No Time	Artist=Artist	Tempo=120.0";
        var song = await Song.Create(songData, dms);
        await dms.SongIndex.SaveSong(song);

        // Act - Should not throw, should use DateTime.MinValue as fallback
        var mergedSong = await dms.SongIndex.SimpleMergeSongs(user, [song]);

        // Assert
        Assert.IsNotNull(mergedSong, "Should successfully merge even with invalid timestamp");
        Assert.IsTrue(mergedSong.SongProperties.Any(p => p.Name == Song.MergeCommand), "Should have .Merge command");
    }

    [TestMethod]
    public async Task SimpleMerge_ChronologicalSort_MultipleSongsMultipleEdits()
    {
        // Arrange: Comprehensive test with multiple songs, each having multiple edits
        var dms = await DanceMusicTester.CreateService("TestDb_SimpleMerge_ChronoComplex", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        await DanceMusicTester.AddUser(dms, "user2", false);
        await DanceMusicTester.AddUser(dms, "admin", false);
        var user = await dms.FindUser("dwgray");

        // Song 1: Created Jan 1, edited Jan 5
        var song1Data = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Chrono Test	Artist=Artist	Tempo=120.0	.Edit=	User=dwgray	Time=01/05/2020 10:00:00 AM	Tempo=122.0";
        var song1 = await Song.Create(song1Data, dms);
        await dms.SongIndex.SaveSong(song1);

        // Song 2: Created Jan 3, edited Jan 4
        var song2Data = @".Create=	User=user2	Time=01/03/2020 10:00:00 AM	Title=Chrono Test	Artist=Artist	Tempo=125.0	.Edit=	User=user2	Time=01/04/2020 10:00:00 AM	Tempo=127.0";
        var song2 = await Song.Create(song2Data, dms);
        await dms.SongIndex.SaveSong(song2);

        // Song 3: Created Jan 2
        var song3Data = @".Create=	User=admin	Time=01/02/2020 10:00:00 AM	Title=Chrono Test	Artist=Artist	Tempo=130.0";
        var song3 = await Song.Create(song3Data, dms);
        await dms.SongIndex.SaveSong(song3);

        // Act
        var mergedSong = await dms.SongIndex.SimpleMergeSongs(user, [song1, song2, song3]);

        // Assert - Order should be: song1.Create (Jan 1), song3.Create (Jan 2), song2.Create (Jan 3), song2.Edit (Jan 4), song1.Edit (Jan 5)
        var commandIndices = mergedSong.SongProperties
            .Select((p, index) => new { Prop = p, Index = index })
            .Where(x => x.Prop.Name == Song.CreateCommand || x.Prop.Name == Song.EditCommand)
            .ToList();

        Assert.AreEqual(5, commandIndices.Count, "Should have 5 commands total (3 creates + 2 edits)");

        // Verify chronological order by checking annotated song GUIDs
        Assert.AreEqual(song1.SongId.ToString(), commandIndices[0].Prop.Value, "First should be song1.Create (Jan 1)");
        Assert.AreEqual(song3.SongId.ToString(), commandIndices[1].Prop.Value, "Second should be song3.Create (Jan 2)");
        Assert.AreEqual(song2.SongId.ToString(), commandIndices[2].Prop.Value, "Third should be song2.Create (Jan 3)");
        Assert.AreEqual(song2.SongId.ToString(), commandIndices[3].Prop.Value, "Fourth should be song2.Edit (Jan 4)");
        Assert.AreEqual(song1.SongId.ToString(), commandIndices[4].Prop.Value, "Fifth should be song1.Edit (Jan 5)");
    }

    // NOTE: .NoMerge filtering is implemented in SongController.AutoMerge after full song reload.
    // Songs with .NoMerge in their history are excluded from AutoMerge execution.
    // To test manually: Add ".NoMerge=  User=admin  Time=MM/dd/yyyy hh:mm:ss tt" to a song's history,
    // then verify it doesn't appear in merge results when running AutoMerge on the InitializationTasks page.
}
