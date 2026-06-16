using Azure.Search.Documents;

using m4d.Services;
using m4d.Tests.TestHelpers;
using m4dModels;
using m4dModels.Tests;

using Moq;

using System.Linq;

namespace m4d.Tests.Services;

[TestClass]
public class SongSearchVoteSearchTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async IAsyncEnumerable<Song> AsAsyncEnumerable(IEnumerable<Song> songs)
    {
        foreach (var song in songs)
        {
            yield return song;
        }

        await Task.CompletedTask;
    }

    private static Task<SongSearch> CreateSongSearchAsync(
        DanceMusicService dms,
        IEnumerable<Song> songsToReturn,
        SongFilter filter)
    {
        var mockSearchService = new Mock<ISearchServiceManager>();
        mockSearchService
            .Setup(m => m.GetSongFilter(It.IsAny<string>()))
            .Returns<string>(s => SongFilter.Create(false, s));

        var mockStats = new Mock<IDanceStatsManager>();
        var serviceForSongIndex = new DanceMusicService(
            dms.Context,
            dms.UserManager,
            mockSearchService.Object,
            mockStats.Object);

        var mockSongIndex = new Mock<SongIndex>();
        mockSongIndex.Setup(m => m.DanceMusicService).Returns(serviceForSongIndex);

        var songList = songsToReturn.ToList();
        mockSongIndex
            .Setup(m => m.StreamAll(
                It.IsAny<string>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CruftFilter>()))
            .Returns<string, SearchOptions, CruftFilter>((_, _, _) => AsAsyncEnumerable(songList));

        var queue = new TestBackgroundTaskQueue();
        return Task.FromResult(
            new SongSearch(filter, "dwgray", true, mockSongIndex.Object, dms.UserManager, queue, null));
    }

    private static async Task<Song> CreateSongAsync(DanceMusicCoreService dms, string raw)
    {
        return await Song.Create(raw, dms);
    }

    [TestMethod]
    public async Task VoteSearch_GroupSelection_ExpandsToMemberDanceVotes()
    {
        var dms = await DanceMusicTester.CreateServiceWithUsers("SongSearchVoteSearch_GroupUp");
        var matchedSong = await CreateSongAsync(
            dms,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Foxtrot Up\tArtist=Test\tTempo=120.0\tTag+=Slow Foxtrot:Dance\tDanceRating=SFT+1");
        var otherSong = await CreateSongAsync(
            dms,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Cha Cha Up\tArtist=Test\tTempo=120.0\tTag+=Cha Cha:Dance\tDanceRating=CHA+1");

        var filter = SongFilter.Create(false, "");
        filter.Action = "Index";
        filter.Dances = "FXT";
        filter.User = "+dwgray|d";

        var songSearch = await CreateSongSearchAsync(dms, [matchedSong, otherSong], filter);

        var results = await songSearch.VoteSearch(new SearchOptions { Size = 25, Skip = 0 });

        Assert.AreEqual(1, results.TotalCount, "Only the matching Foxtrot song should be returned");
        Assert.AreEqual(1, results.Songs.Count(), "Only one song should be in the result page");
        Assert.AreEqual("Foxtrot Up", results.Songs.First().Title);
    }

    [TestMethod]
    public async Task VoteSearch_GroupSelection_ExpandsToMemberDanceVotes_ForDownVotes()
    {
        var dms = await DanceMusicTester.CreateServiceWithUsers("SongSearchVoteSearch_GroupDown");
        var matchedSong = await CreateSongAsync(
            dms,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Foxtrot Down\tArtist=Test\tTempo=120.0\tTag+=Slow Foxtrot:Dance\tDanceRating=SFT-1");
        var otherSong = await CreateSongAsync(
            dms,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Rumba Down\tArtist=Test\tTempo=120.0\tTag+=Rumba:Dance\tDanceRating=RMB-1");

        var filter = SongFilter.Create(false, "");
        filter.Action = "Index";
        filter.Dances = "FXT";
        filter.User = "+dwgray|x";

        var songSearch = await CreateSongSearchAsync(dms, [matchedSong, otherSong], filter);

        var results = await songSearch.VoteSearch(new SearchOptions { Size = 25, Skip = 0 });

        Assert.AreEqual(1, results.TotalCount, "Only the matching Foxtrot song should be returned");
        Assert.AreEqual(1, results.Songs.Count(), "Only one song should be in the result page");
        Assert.AreEqual("Foxtrot Down", results.Songs.First().Title);
    }
}
