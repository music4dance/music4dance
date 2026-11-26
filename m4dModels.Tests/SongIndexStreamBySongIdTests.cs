using System.Text.RegularExpressions;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

namespace m4dModels.Tests;

// SongIndex.StreamAllBySongId pages with a SongId cursor so that a batch whose edits take songs
// out of its own filter (for example the ISRC backfills, over "on iTunes, no ISRC") still visits
// every song. With $skip paging, each later page would start past songs that were never visited.
[TestClass]
public partial class SongIndexStreamBySongIdTests
{
    private class FakeSongIndex(SearchClient client, ISearchServiceManager manager) : SongIndex
    {
        protected override SearchClient Client => client;
        protected override ISearchServiceManager Manager => manager;

        protected override Task<Song> CreateSong(SearchDocument document) =>
            Task.FromResult(new Song { SongId = Guid.Parse(document.GetString(SongIdField)) });
    }

    [GeneratedRegex("SongId gt '([0-9a-f-]+)'")]
    private static partial Regex CursorPattern();

    // An index whose matching set is `matching`: honors the SongId cursor, Skip and Size, and
    // records each request's filter.
    private static Mock<SearchClient> FakeClient(SortedSet<string> matching, List<string> filters)
    {
        var mock = new Mock<SearchClient>();
        mock.Setup(c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, SearchOptions options, CancellationToken _) =>
            {
                filters.Add(options.Filter);
                var cursor = options.Filter == null ? null : CursorPattern().Match(options.Filter);
                var ids = matching
                    .Where(id => cursor is not { Success: true } ||
                        string.CompareOrdinal(id, cursor.Groups[1].Value) > 0)
                    .Skip(options.Skip ?? 0)
                    .Take(options.Size ?? 50)
                    .ToList();
                var results = ids.Select(id => SearchModelFactory.SearchResult(
                    new SearchDocument { [SongIndex.SongIdField] = id, [SongIndex.PropertiesField] = "" },
                    1.0, null)).ToList();
                return Response.FromValue(
                    SearchModelFactory.SearchResults(results, null, null, null, null),
                    Mock.Of<Response>());
            });
        return mock;
    }

    private static SortedSet<string> Ids(int count) =>
        new(Enumerable.Range(0, count).Select(_ => Guid.NewGuid().ToString()), StringComparer.Ordinal);

    [TestMethod]
    public async Task StreamAllBySongId_SongsLeaveTheFilterAsTheyAreVisited_VisitsEverySong()
    {
        var matching = Ids(2500);
        var all = matching.ToHashSet();
        var filters = new List<string>();
        var index = new FakeSongIndex(FakeClient(matching, filters).Object, Mock.Of<ISearchServiceManager>());

        var visited = new List<Guid>();
        await foreach (var song in index.StreamAllBySongId(null, new SearchOptions(), CruftFilter.AllCruft))
        {
            visited.Add(song.SongId);
            // The edit took this song out of the filter, as an added ISRC does for "no ISRC"
            _ = matching.Remove(song.SongId.ToString());
        }

        Assert.HasCount(2500, visited);
        Assert.IsTrue(visited.All(id => all.Contains(id.ToString())));
        Assert.HasCount(2500, visited.Distinct());
        Assert.HasCount(3, filters);
    }

    [TestMethod]
    public async Task StreamAll_SongsLeaveTheFilterAsTheyAreVisited_SkipsSongs()
    {
        // The $skip behavior the cursor replaces, kept as a record of why batches don't use it
        var matching = Ids(2500);
        var index = new FakeSongIndex(FakeClient(matching, []).Object, Mock.Of<ISearchServiceManager>());

        var visited = 0;
        await foreach (var song in index.StreamAll(null, new SearchOptions(), CruftFilter.AllCruft))
        {
            visited += 1;
            _ = matching.Remove(song.SongId.ToString());
        }

        Assert.IsLessThan(2500, visited);
    }

    [TestMethod]
    public async Task StreamAllBySongId_KeepsTheCallersFilterAndRestoresTheOptions()
    {
        var filters = new List<string>();
        var index = new FakeSongIndex(FakeClient(Ids(1500), filters).Object, Mock.Of<ISearchServiceManager>());
        var options = new SearchOptions { Filter = "Purchase/any(t: t eq 'I')", Size = 25, Skip = 50 };
        options.OrderBy.Add("Modified desc");

        await foreach (var _ in index.StreamAllBySongId(null, options, CruftFilter.AllCruft))
        {
        }

        Assert.HasCount(2, filters);
        Assert.AreEqual("Purchase/any(t: t eq 'I')", filters[0]);
        StringAssert.StartsWith(filters[1], "(Purchase/any(t: t eq 'I')) and SongId gt '");

        Assert.AreEqual("Purchase/any(t: t eq 'I')", options.Filter);
        Assert.AreEqual(25, options.Size);
        Assert.AreEqual(50, options.Skip);
        CollectionAssert.AreEqual(new[] { "Modified desc" }, options.OrderBy.ToList());
    }
}
