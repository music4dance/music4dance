namespace m4dModels.Tests;

[TestClass]
public class ChunkingTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        _ = await DanceMusicTester.LoadDances();
    }

    [TestMethod]
    public void SingleChunk()
    {
        var chunked = new ChunkedSong(@".Edit=	User=dwgray	Time=02-Aug-2024 12:44:04 PM	DanceRating=CHA+1	Tag+=Cha Cha:Dance");
        Assert.AreEqual(1, chunked.Chunks.Count);
        Assert.AreEqual(1, chunked.UserChunks.Count);
        var userChunk = chunked.UserChunks["dwgray"];
        Assert.IsNotNull(userChunk);
        Assert.AreEqual(1, userChunk.Count);
        Assert.AreEqual(5, userChunk[0].Properties.Count);
    }

    [TestMethod]
    public void MultiChunk()
    {
        var chunked = new ChunkedSong(@".Merge=c9d734f4-c0ce-4de7-b550-2c04aa9c544e;5465aeb1-473e-4cc0-8a21-49576bb8c17f	.Edit=	User=ArthurMurrays|P	Time=07/30/2023 09:48:04	Tag+=Cha Cha:Dance	DanceRating=CHA+1	Tag+:CHA=American:Style	.Edit=	User=dwgray	Time=02-Aug-2024 12:44:04 PM	DanceRating=CHA+1	Tag+=Cha Cha:Dance");
        Assert.AreEqual(3, chunked.Chunks.Count);
        Assert.AreEqual(2, chunked.UserChunks.Count);
        Assert.AreEqual(1, chunked.UserChunks["dwgray"].Count);
        Assert.AreEqual(1, chunked.UserChunks["ArthurMurrays|P"].Count);
    }

    private static readonly string _multiUser = @".Merge=c9d734f4-c0ce-4de7-b550-2c04aa9c544e;5465aeb1-473e-4cc0-8a21-49576bb8c17f	.Edit=	User=ArthurMurrays|P	Time=07/30/2023 09:48:04	Tag+=Cha Cha:Dance	DanceRating=CHA+1	Tag+:CHA=American:Style	.Edit=	User=dwgray	Time=02-Aug-2024 12:44:04 PM	DanceRating=CHA+1	Tag+=Cha Cha:Dance	.Edit=	User=ArthurMurrays|P	Time=08/30/2024 09:48:04	Tag+=Wedding:Other";
    [TestMethod]
    public void UserRepeatChunk()
    {
        var chunked = new ChunkedSong(_multiUser);
        Assert.AreEqual(4, chunked.Chunks.Count);
        Assert.AreEqual(2, chunked.UserChunks.Count);
        var dwgray = chunked.UserChunks["dwgray"];
        var arthurMurray = chunked.UserChunks["ArthurMurrays|P"];
        Assert.AreEqual(1, dwgray.Count);
        Assert.AreEqual(2, arthurMurray.Count);
    }

    [TestMethod]
    public void TestSerialize()
    {
        var chunked = new ChunkedSong(_multiUser);
        var serialized = chunked.Serialize();
        Assert.AreEqual(_multiUser, serialized);
    }

    [TestMethod]
    public void ValidSongPassesInvalidBatch()
    {
        var chunked = new ChunkedSong(_multiUser);
        Assert.IsFalse(chunked.HasInvalidBatch());
    }

    [TestMethod]
    public void TestInvalidBatchDanceRating()
    {
        var chunked = new ChunkedSong(@".Edit=	User=batch|P	Time=02-Aug-2024 12:44:04 PM	DanceRating=CHA+1");
        Assert.IsTrue(chunked.HasInvalidBatch());
    }

    //[TestMethod]
    //public void TestInvalidBatchDanceTag()
    //{
    //    var chunked = new ChunkedSong(@".Edit=	User=batch-s	Time=02-Aug-2024 12:44:04 PM	Tag+=Cha Cha:Dance");
    //    Assert.IsTrue(chunked.HasInvalidBatch());
    //}

    [TestMethod]
    public void SimpleMatchRatingSuccess()
    {
        var chunked = new ChunkedSong(@".Edit=	User=batch	Time=02-Aug-2024 12:44:04 PM	Tag+=Cha Cha:Dance");
        var prop = chunked.Chunks.First().MatchRating("CHA");
        Assert.IsNotNull(prop);
        Assert.AreEqual("Cha Cha:Dance", prop.Value);
    }

    [TestMethod]
    public void SimpleMatchRatingFail()
    {
        var chunked = new ChunkedSong(@".Edit=	User=batch	Time=02-Aug-2024 12:44:04 PM");
        var prop = chunked.Chunks.First().MatchRating("CHA");
        Assert.IsNull(prop);
    }

    [TestMethod]
    public void SimpleMatchTagSuccess()
    {
        var chunked = new ChunkedSong(@".Edit=	User=batch	Time=02-Aug-2024 12:44:04 PM	DanceRating=CHA+1");
        var ratings = chunked.Chunks.First().MatchTags("Cha Cha:Dance");
        Assert.AreEqual(1, ratings.Count);
        Assert.AreEqual("CHA+1", ratings[0].Value);
    }

    [TestMethod]
    public void SimpleMatchTagFail()
    {
        var chunked = new ChunkedSong(@".Edit=	User=batch	Time=02-Aug-2024 12:44:04 PM");
        var ratings = chunked.Chunks.First().MatchTags("Cha Cha:Dance");
        Assert.AreEqual(0, ratings.Count);
    }

    [TestMethod]
    public void ComplexMatchTagSuccess()
    {
        var chunked = new ChunkedSong(@".Edit=	User=batch	Time=02-Aug-2024 12:44:04 PM	DanceRating=CHA+1	DanceRating=RMB+1");
        var ratings = chunked.Chunks.First().MatchTags("Cha Cha:Dance|Rumba:Dance|Pop:Music");
        Assert.AreEqual(2, ratings.Count);
        Assert.IsTrue(ratings.Any(r => r.Value == "CHA+1"));
        Assert.IsTrue(ratings.Any(r => r.Value == "RMB+1"));
    }

    [TestMethod]
    public void ComplexMatchTagFail()
    {
        var chunked = new ChunkedSong(@".Edit=	User=batch	Time=02-Aug-2024 12:44:04 PM");
        var ratings = chunked.Chunks.First().MatchTags("Cha Cha:Dance|Rumba:Dance|Pop:Music");
        Assert.AreEqual(0, ratings.Count);
    }

    [TestMethod]
    public void HasGroupTagSuccess()
    {
        var chunked = new ChunkedSong(@".Edit=	User=batch	Time=02-Aug-2024 12:44:04 PM	Tag+=East Coast Swing:Dance");
        Assert.IsTrue(chunked.Chunks.First().HasGroupTag("JIV"));
    }

    [TestMethod]
    public void HasGroupTagFail()
    {
        var chunked = new ChunkedSong(@".Edit=	User=batch	Time=02-Aug-2024 12:44:04 PM	Tag+=Rumba:Dance");
        Assert.IsFalse(chunked.Chunks.First().HasGroupTag("JIV"));
    }

    [TestMethod]
    public void HasGroupTagFailLoose()
    {
        var chunked = new ChunkedSong(@".Edit=	User=batch	Time=02-Aug-2024 12:44:04 PM	Tag+=Rumba:Dance");
        Assert.IsFalse(chunked.Chunks.First().HasGroupTag("CHA"));
    }

    #region Chunking Behavior Tests (Pre-Refactor Safety)

    [TestMethod]
    public void Chunking_CUT_Order_ParsesCorrectly()
    {
        // Arrange: Create-User-Time order (most common)
        var chunked = new ChunkedSong(@".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Test Song	Artist=Test Artist");

        // Assert
        Assert.AreEqual(1, chunked.Chunks.Count, "Should have 1 chunk");
        Assert.AreEqual("dwgray", chunked.Chunks[0].User, "Should capture user in CUT order");
        Assert.IsTrue(chunked.Chunks[0].Properties.Any(p => p.Name == Song.TimeField), "Should have Time property");
    }

    [TestMethod]
    public void Chunking_CTU_Order_ParsesCorrectly()
    {
        // Arrange: Create-Time-User order (less common)
        var chunked = new ChunkedSong(@".Create=	Time=01/01/2020 10:00:00 AM	User=dwgray	Title=Test Song");

        // Assert
        Assert.AreEqual(1, chunked.Chunks.Count, "Should have 1 chunk");
        Assert.AreEqual("dwgray", chunked.Chunks[0].User, "Should capture user in CTU order");
    }

    [TestMethod]
    public void Chunking_MultipleActions_SplitsCorrectly()
    {
        // Arrange: Multiple different actions
        var chunked = new ChunkedSong(@".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Song	.Edit=	User=user2	Time=01/02/2020 11:00:00 AM	Tempo=120	.Delete=	User=admin	Time=01/03/2020 12:00:00 PM");

        // Assert
        Assert.AreEqual(3, chunked.Chunks.Count, "Should have 3 chunks (Create, Edit, Delete)");
        Assert.AreEqual("dwgray", chunked.Chunks[0].User);
        Assert.AreEqual("user2", chunked.Chunks[1].User);
        Assert.AreEqual("admin", chunked.Chunks[2].User);
    }

    [TestMethod]
    public void Chunking_PreservesProperties()
    {
        // Arrange
        var input = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Test Song	Artist=Test Artist	Tempo=120.0	Tag+=Salsa:Dance";
        var chunked = new ChunkedSong(input);

        // Assert - Check all properties are preserved
        var chunk = chunked.Chunks[0];
        Assert.IsTrue(chunk.Properties.Any(p => p.Name == Song.CreateCommand));
        Assert.IsTrue(chunk.Properties.Any(p => p.Name == Song.UserField && p.Value == "dwgray"));
        Assert.IsTrue(chunk.Properties.Any(p => p.Name == Song.TimeField));
        Assert.IsTrue(chunk.Properties.Any(p => p.Name == Song.TitleField && p.Value == "Test Song"));
        Assert.IsTrue(chunk.Properties.Any(p => p.Name == Song.ArtistField && p.Value == "Test Artist"));
        Assert.IsTrue(chunk.Properties.Any(p => p.Name == Song.TempoField && p.Value == "120.0"));
        Assert.IsTrue(chunk.Properties.Any(p => p.Name.StartsWith("Tag+") && p.Value.Contains("Salsa")));
    }

    [TestMethod]
    public void Chunking_UserChunks_GroupsByUser()
    {
        // Arrange: Same user appears multiple times
        var chunked = new ChunkedSong(@".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Song	.Edit=	User=user2	Time=01/02/2020 11:00:00 AM	Tempo=120	.Edit=	User=dwgray	Time=01/03/2020 12:00:00 PM	Artist=Updated");

        // Assert
        Assert.AreEqual(3, chunked.Chunks.Count, "Should have 3 total chunks");
        Assert.AreEqual(2, chunked.UserChunks.Count, "Should have 2 users");
        Assert.AreEqual(2, chunked.UserChunks["dwgray"].Count, "dwgray should have 2 chunks");
        Assert.AreEqual(1, chunked.UserChunks["user2"].Count, "user2 should have 1 chunk");
    }

    [TestMethod]
    public void Chunking_ActionBoundary_FlushesOnNextAction()
    {
        // Arrange: Properties between actions should be in correct chunks
        var chunked = new ChunkedSong(@".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Song	Tag+=Dance1:Dance	.Edit=	User=user2	Time=01/02/2020 11:00:00 AM	Tag+=Dance2:Dance");

        // Assert
        var chunk1 = chunked.Chunks[0];
        var chunk2 = chunked.Chunks[1];

        Assert.IsTrue(chunk1.Properties.Any(p => p.Value?.Contains("Dance1") == true), "First chunk should have Dance1");
        Assert.IsFalse(chunk1.Properties.Any(p => p.Value?.Contains("Dance2") == true), "First chunk should NOT have Dance2");

        Assert.IsTrue(chunk2.Properties.Any(p => p.Value?.Contains("Dance2") == true), "Second chunk should have Dance2");
        Assert.IsFalse(chunk2.Properties.Any(p => p.Value?.Contains("Dance1") == true), "Second chunk should NOT have Dance1");
    }

    [TestMethod]
    public void Chunking_EmptyProperties_HandlesGracefully()
    {
        // Arrange: Just an action command with minimal properties
        var chunked = new ChunkedSong(@".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM");

        // Assert
        Assert.AreEqual(1, chunked.Chunks.Count);
        Assert.AreEqual("dwgray", chunked.Chunks[0].User);
    }

    [TestMethod]
    public void Chunking_CheckHeader_CUT_Valid()
    {
        // Arrange
        var chunked = new ChunkedSong(@".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Song");

        // Assert
        Assert.IsTrue(chunked.Chunks[0].CheckHeader(), "CUT order should have valid header");
    }

    [TestMethod]
    public void Chunking_CheckHeader_CTU_Valid()
    {
        // Arrange
        var chunked = new ChunkedSong(@".Create=	Time=01/01/2020 10:00:00 AM	User=dwgray	Title=Song");

        // Assert
        Assert.IsTrue(chunked.Chunks[0].CheckHeader(), "CTU order should have valid header");
    }

    [TestMethod]
    public void Chunking_Serialize_Roundtrip()
    {
        // Arrange: Complex song with multiple chunks
        var input = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Song	.Edit=	User=user2	Time=01/02/2020 11:00:00 AM	Tempo=120";
        var chunked = new ChunkedSong(input);

        // Act
        var serialized = chunked.Serialize();
        var rechunked = new ChunkedSong(serialized);

        // Assert - Check semantic equivalence
        Assert.AreEqual(chunked.Chunks.Count, rechunked.Chunks.Count, "Should have same number of chunks");
        Assert.AreEqual(chunked.UserChunks.Count, rechunked.UserChunks.Count, "Should have same number of users");
        Assert.AreEqual(chunked.SongProperties.Count, rechunked.SongProperties.Count, "Should have same number of properties");
    }

    [TestMethod]
    public void Chunking_AllActionTypes_AreRecognized()
    {
        // Arrange: Test various action types
        var chunked = new ChunkedSong(@".Create=	User=u1	Time=01/01/2020 10:00:00 AM	.Edit=	User=u2	Time=01/02/2020 11:00:00 AM	.Delete=	User=u3	Time=01/03/2020 12:00:00 PM	.Merge=guid1;guid2	User=u4	Time=01/04/2020 01:00:00 PM	.NoMerge=	User=u5	Time=01/05/2020 02:00:00 PM");

        // Assert
        Assert.AreEqual(5, chunked.Chunks.Count, "Should recognize all 5 action types");
        Assert.IsTrue(chunked.Chunks[0].Properties.Any(p => p.Name == Song.CreateCommand));
        Assert.IsTrue(chunked.Chunks[1].Properties.Any(p => p.Name == Song.EditCommand));
        Assert.IsTrue(chunked.Chunks[2].Properties.Any(p => p.Name == Song.DeleteCommand));
        Assert.IsTrue(chunked.Chunks[3].Properties.Any(p => p.Name == Song.MergeCommand));
        Assert.IsTrue(chunked.Chunks[4].Properties.Any(p => p.Name == Song.NoMergeCommand));
    }

    [TestMethod]
    public void Chunking_ConsecutiveActions_NoLostProperties()
    {
        // Arrange: Actions immediately following each other
        var chunked = new ChunkedSong(@".Create=	User=u1	Time=01/01/2020 10:00:00 AM	Title=Song1	.Edit=	User=u2	Time=01/02/2020 11:00:00 AM	.Edit=	User=u3	Time=01/03/2020 12:00:00 PM	Title=Song2");

        // Assert
        Assert.AreEqual(3, chunked.Chunks.Count);
        Assert.IsTrue(chunked.Chunks[0].Properties.Any(p => p.Value == "Song1"));
        Assert.IsTrue(chunked.Chunks[2].Properties.Any(p => p.Value == "Song2"));
    }

    [TestMethod]
    public void Chunking_PropertyCount_Matches()
    {
        // Arrange
        var input = @".Create=	User=dwgray	Time=01/01/2020 10:00:00 AM	Title=Song	Artist=Artist	Tempo=120	Tag+=Dance:Dance";
        var originalProps = SongProperty.Load(input);
        var chunked = new ChunkedSong(input);

        // Act
        var reconstructed = chunked.SongProperties;

        // Assert
        Assert.AreEqual(originalProps.Count(), reconstructed.Count, "Should preserve all properties");
    }

    #endregion

}
