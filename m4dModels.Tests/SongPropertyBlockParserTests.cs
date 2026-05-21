using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Tests for SongPropertyBlockParser - the shared block parsing logic used by
/// both SimpleMergeSongs and ChunkedSong.
/// </summary>
[TestClass]
public class SongPropertyBlockParserTests
{
    [TestMethod]
    public void ParseBlocks_CUT_Order_ParsesCorrectly()
    {
        // Arrange: Create-User-Time order
        var properties = new List<SongProperty>
        {
            new(Song.CreateCommand, ""),
            new(Song.UserField, "dwgray"),
            new(Song.TimeField, "01/01/2020 10:00:00 AM"),
            new(Song.TitleField, "Test Song"),
            new(Song.ArtistField, "Artist")
        };

        // Act
        var blocks = SongPropertyBlockParser.ParseBlocks(properties);

        // Assert
        Assert.AreEqual(1, blocks.Count, "Should have 1 block");
        Assert.AreEqual(Song.CreateCommand, blocks[0].ActionCommand);
        Assert.AreEqual("dwgray", blocks[0].User);
        Assert.IsNotNull(blocks[0].Timestamp);
        Assert.AreEqual(4, blocks[0].Properties.Count, "Should have 4 properties (User, Time, Title, Artist)");
    }

    [TestMethod]
    public void ParseBlocks_CTU_Order_ParsesCorrectly()
    {
        // Arrange: Create-Time-User order
        var properties = new List<SongProperty>
        {
            new(Song.CreateCommand, ""),
            new(Song.TimeField, "01/01/2020 10:00:00 AM"),
            new(Song.UserField, "dwgray"),
            new(Song.TitleField, "Test Song")
        };

        // Act
        var blocks = SongPropertyBlockParser.ParseBlocks(properties);

        // Assert
        Assert.AreEqual(1, blocks.Count);
        Assert.AreEqual("dwgray", blocks[0].User, "Should capture User in CTU order");
        Assert.IsNotNull(blocks[0].Timestamp, "Should capture Timestamp in CTU order");
    }

    [TestMethod]
    public void ParseBlocks_MultipleBlocks_SplitsCorrectly()
    {
        // Arrange
        var properties = new List<SongProperty>
        {
            new(Song.CreateCommand, ""),
            new(Song.UserField, "dwgray"),
            new(Song.TimeField, "01/01/2020 10:00:00 AM"),
            new(Song.TitleField, "Test Song"),
            new(Song.EditCommand, ""),
            new(Song.UserField, "user2"),
            new(Song.TimeField, "01/02/2020 11:00:00 AM"),
            new(Song.TempoField, "120")
        };

        // Act
        var blocks = SongPropertyBlockParser.ParseBlocks(properties);

        // Assert
        Assert.AreEqual(2, blocks.Count, "Should have 2 blocks");
        Assert.AreEqual(Song.CreateCommand, blocks[0].ActionCommand);
        Assert.AreEqual(Song.EditCommand, blocks[1].ActionCommand);
        Assert.AreEqual("dwgray", blocks[0].User);
        Assert.AreEqual("user2", blocks[1].User);
    }

    [TestMethod]
    public void ParseBlocks_WithFilter_OnlyIncludesFilteredActions()
    {
        // Arrange: Mix of .Create, .Edit, and .Delete
        var properties = new List<SongProperty>
        {
            new(Song.CreateCommand, ""),
            new(Song.UserField, "dwgray"),
            new(Song.TimeField, "01/01/2020 10:00:00 AM"),
            new(Song.DeleteCommand, ""),  // This should be ignored with CreateEditFilter
            new(Song.UserField, "admin"),
            new(Song.TimeField, "01/02/2020 11:00:00 AM"),
            new(Song.EditCommand, ""),
            new(Song.UserField, "user2"),
            new(Song.TimeField, "01/03/2020 12:00:00 PM")
        };

        // Act - Use CreateEditFilter to only split on .Create and .Edit
        var blocks = SongPropertyBlockParser.ParseBlocks(
            properties,
            SongPropertyBlockParser.CreateEditFilter());

        // Assert
        Assert.AreEqual(2, blocks.Count, "Should have 2 blocks (Create and Edit), Delete ignored as boundary");
        Assert.AreEqual(Song.CreateCommand, blocks[0].ActionCommand);
        Assert.AreEqual(Song.EditCommand, blocks[1].ActionCommand);

        // The .Delete and its properties should be part of the .Create block
        Assert.IsTrue(blocks[0].Properties.Any(p => p.Name == Song.DeleteCommand), 
            ".Delete should be included as property in Create block");
    }

    [TestMethod]
    public void ParseBlocks_NoFilter_IncludesAllActions()
    {
        // Arrange
        var properties = new List<SongProperty>
        {
            new(Song.CreateCommand, ""),
            new(Song.UserField, "dwgray"),
            new(Song.TimeField, "01/01/2020 10:00:00 AM"),
            new(Song.DeleteCommand, ""),
            new(Song.UserField, "admin"),
            new(Song.TimeField, "01/02/2020 11:00:00 AM")
        };

        // Act - No filter (all actions are boundaries)
        var blocks = SongPropertyBlockParser.ParseBlocks(properties);

        // Assert
        Assert.AreEqual(2, blocks.Count, "Should have 2 blocks (Create and Delete)");
        Assert.AreEqual(Song.CreateCommand, blocks[0].ActionCommand);
        Assert.AreEqual(Song.DeleteCommand, blocks[1].ActionCommand);
    }

    [TestMethod]
    public void ParseAndSortBlocks_SortsChronologically()
    {
        // Arrange: Properties in reverse chronological order
        var properties = new List<SongProperty>
        {
            new(Song.CreateCommand, ""),
            new(Song.UserField, "user3"),
            new(Song.TimeField, "01/03/2020 12:00:00 PM"),
            new(Song.CreateCommand, ""),
            new(Song.UserField, "user1"),
            new(Song.TimeField, "01/01/2020 10:00:00 AM"),
            new(Song.CreateCommand, ""),
            new(Song.UserField, "user2"),
            new(Song.TimeField, "01/02/2020 11:00:00 AM")
        };

        // Act
        var blocks = SongPropertyBlockParser.ParseAndSortBlocks(properties);

        // Assert
        Assert.AreEqual(3, blocks.Count);
        Assert.AreEqual("user1", blocks[0].User, "Should be sorted: user1 (Jan 1) first");
        Assert.AreEqual("user2", blocks[1].User, "Should be sorted: user2 (Jan 2) second");
        Assert.AreEqual("user3", blocks[2].User, "Should be sorted: user3 (Jan 3) third");
    }

    [TestMethod]
    public void ParseBlocks_InvalidTimestamp_UsesNull()
    {
        // Arrange
        var properties = new List<SongProperty>
        {
            new(Song.CreateCommand, ""),
            new(Song.UserField, "dwgray"),
            new(Song.TimeField, "invalid-date")
        };

        // Act
        var blocks = SongPropertyBlockParser.ParseBlocks(properties);

        // Assert
        Assert.AreEqual(1, blocks.Count);
        Assert.IsNull(blocks[0].Timestamp, "Invalid timestamp should result in null");
    }

    [TestMethod]
    public void ParseAndSortBlocks_NullTimestamp_ComesFirst()
    {
        // Arrange: One block with timestamp, one without
        var properties = new List<SongProperty>
        {
            new(Song.CreateCommand, ""),
            new(Song.UserField, "user-with-time"),
            new(Song.TimeField, "01/01/2020 10:00:00 AM"),
            new(Song.CreateCommand, ""),
            new(Song.UserField, "user-no-time"),
            new(Song.TimeField, "invalid")
        };

        // Act
        var blocks = SongPropertyBlockParser.ParseAndSortBlocks(properties);

        // Assert
        Assert.AreEqual(2, blocks.Count);
        Assert.AreEqual("user-no-time", blocks[0].User, "Block without valid timestamp should come first (MinValue)");
        Assert.AreEqual("user-with-time", blocks[1].User, "Block with timestamp should come second");
    }

    [TestMethod]
    public void FlattenBlocks_ReconstructsProperties()
    {
        // Arrange
        var blocks = new List<SongPropertyBlock>
        {
            new()
            {
                ActionCommand = Song.CreateCommand,
                ActionValue = "guid1",
                Properties =
                [
                    new(Song.UserField, "dwgray"),
                    new(Song.TimeField, "01/01/2020 10:00:00 AM"),
                    new(Song.TitleField, "Song1")
                ]
            },
            new()
            {
                ActionCommand = Song.EditCommand,
                ActionValue = "guid2",
                Properties =
                [
                    new(Song.UserField, "user2"),
                    new(Song.TimeField, "01/02/2020 11:00:00 AM"),
                    new(Song.TempoField, "120")
                ]
            }
        };

        // Act
        var flattened = SongPropertyBlockParser.FlattenBlocks(blocks);

        // Assert
        Assert.AreEqual(8, flattened.Count, "Should have 8 properties (2 actions + 6 other properties)");
        Assert.AreEqual(Song.CreateCommand, flattened[0].Name);
        Assert.AreEqual("guid1", flattened[0].Value);
        Assert.AreEqual(Song.EditCommand, flattened[4].Name);
        Assert.AreEqual("guid2", flattened[4].Value);
    }

    [TestMethod]
    public void HasValidHeader_ValidCUT_ReturnsTrue()
    {
        // Arrange
        var block = new SongPropertyBlock
        {
            Properties =
            [
                new(Song.UserField, "dwgray"),
                new(Song.TimeField, "01/01/2020 10:00:00 AM"),
                new(Song.TitleField, "Song")
            ]
        };

        // Act & Assert
        Assert.IsTrue(block.HasValidHeader(), "CUT order should be valid header");
    }

    [TestMethod]
    public void HasValidHeader_ValidCTU_ReturnsTrue()
    {
        // Arrange
        var block = new SongPropertyBlock
        {
            Properties =
            [
                new(Song.TimeField, "01/01/2020 10:00:00 AM"),
                new(Song.UserField, "dwgray"),
                new(Song.TitleField, "Song")
            ]
        };

        // Act & Assert
        Assert.IsTrue(block.HasValidHeader(), "CTU order should be valid header");
    }

    [TestMethod]
    public void HasValidHeader_MissingUser_ReturnsFalse()
    {
        // Arrange
        var block = new SongPropertyBlock
        {
            Properties =
            [
                new(Song.TimeField, "01/01/2020 10:00:00 AM"),
                new(Song.TitleField, "Song")
            ]
        };

        // Act & Assert
        Assert.IsFalse(block.HasValidHeader(), "Missing User should be invalid header");
    }

    [TestMethod]
    public void HasValidHeader_MissingTime_ReturnsFalse()
    {
        // Arrange
        var block = new SongPropertyBlock
        {
            Properties =
            [
                new(Song.UserField, "dwgray"),
                new(Song.TitleField, "Song")
            ]
        };

        // Act & Assert
        Assert.IsFalse(block.HasValidHeader(), "Missing Time should be invalid header");
    }

    [TestMethod]
    public void ParseBlocks_EmptyProperties_ReturnsEmptyList()
    {
        // Arrange
        var properties = new List<SongProperty>();

        // Act
        var blocks = SongPropertyBlockParser.ParseBlocks(properties);

        // Assert
        Assert.AreEqual(0, blocks.Count, "Empty properties should result in no blocks");
    }

    [TestMethod]
    public void ParseBlocks_NoActionCommands_ReturnsEmptyList()
    {
        // Arrange: Only content properties, no action commands
        var properties = new List<SongProperty>
        {
            new(Song.TitleField, "Song"),
            new(Song.ArtistField, "Artist")
        };

        // Act
        var blocks = SongPropertyBlockParser.ParseBlocks(properties);

        // Assert
        Assert.AreEqual(0, blocks.Count, "No action commands should result in no blocks");
    }

    [TestMethod]
    public void AllProperties_IncludesActionAndContent()
    {
        // Arrange
        var block = new SongPropertyBlock
        {
            ActionCommand = Song.CreateCommand,
            ActionValue = "test-guid",
            Properties =
            [
                new(Song.UserField, "dwgray"),
                new(Song.TimeField, "01/01/2020 10:00:00 AM")
            ]
        };

        // Act
        var allProps = block.AllProperties.ToList();

        // Assert
        Assert.AreEqual(3, allProps.Count, "Should include action + 2 properties");
        Assert.AreEqual(Song.CreateCommand, allProps[0].Name);
        Assert.AreEqual("test-guid", allProps[0].Value);
        Assert.AreEqual(Song.UserField, allProps[1].Name);
        Assert.AreEqual(Song.TimeField, allProps[2].Name);
    }
}
