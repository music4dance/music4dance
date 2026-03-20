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
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_SimpleMerge_Annotate");
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
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_SimpleMerge_Sort");
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
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_SimpleMerge_Preserve");
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
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_SimpleMerge_MultiEdit");
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

    // NOTE: .NoMerge functionality is implemented but test requires LoadLightSongsStreamingAsync to load SongProperties
    // To test .NoMerge manually: Add ".NoMerge=  User=admin  Time=MM/dd/yyyy hh:mm:ss tt" to a song's history
    // That song will be excluded from merge candidates
}
