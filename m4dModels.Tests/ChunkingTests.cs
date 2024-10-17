using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace m4dModels.Tests;

[TestClass]
public class ChunkingTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await DanceMusicTester.LoadDances();
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

    private static string _multiUser = @".Merge=c9d734f4-c0ce-4de7-b550-2c04aa9c544e;5465aeb1-473e-4cc0-8a21-49576bb8c17f	.Edit=	User=ArthurMurrays|P	Time=07/30/2023 09:48:04	Tag+=Cha Cha:Dance	DanceRating=CHA+1	Tag+:CHA=American:Style	.Edit=	User=dwgray	Time=02-Aug-2024 12:44:04 PM	DanceRating=CHA+1	Tag+=Cha Cha:Dance	.Edit=	User=ArthurMurrays|P	Time=08/30/2024 09:48:04	Tag+=Wedding:Other";
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

}
