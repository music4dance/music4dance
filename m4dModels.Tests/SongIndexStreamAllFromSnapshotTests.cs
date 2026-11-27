using System.Text.RegularExpressions;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

namespace m4dModels.Tests;

// SongIndex.StreamAllFromSnapshot lists the matching SongIds before a batch edits anything, then
// loads the songs by id in that order. So a batch whose edits take songs out of its own filter (the
// ISRC backfills, over "on iTunes, no ISRC") or move them in the sort (Modified changes on every
// edit) still visits every song once, in the order the admin chose. With $skip paging over the
// songs themselves, each edit shifts the later pages.
[TestClass]
public partial class SongIndexStreamAllFromSnapshotTests
{
    private class FakeSongIndex(SearchClient client, ISearchServiceManager manager) : SongIndex
    {
        protected override SearchClient Client => client;
        protected override ISearchServiceManager Manager => manager;

        protected override Task<Song> CreateSong(SearchDocument document) =>
            Task.FromResult(new Song { SongId = Guid.Parse(document.GetString(SongIdField)) });
    }

    [GeneratedRegex(@"search\.in\(SongId, '([^']*)', ','\)")]
    private static partial Regex IdListPattern();

    private record Request(string Filter, int? Skip, int? Size, string OrderBy, string Select);

    // An index whose matching set is `modified`'s keys (SongId -> Modified tick). Honors the id
    // list filter, "Modified desc" (SongId breaking ties) or SongId order, Skip, Size and Select.
    private static Mock<SearchClient> FakeClient(Dictionary<string, int> modified, List<Request> requests)
    {
        var mock = new Mock<SearchClient>();
        mock.Setup(c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, SearchOptions options, CancellationToken _) =>
            {
                requests.Add(new Request(options.Filter, options.Skip, options.Size,
                    string.Join(", ", options.OrderBy), string.Join(",", options.Select)));

                IEnumerable<string> ids = modified.Keys;
                var list = options.Filter == null ? null : IdListPattern().Match(options.Filter);
                if (list is { Success: true })
                {
                    var wanted = list.Groups[1].Value.Split(',').ToHashSet();
                    ids = ids.Where(wanted.Contains);
                }

                var ordered = options.OrderBy.Contains("Modified desc")
                    ? ids.OrderByDescending(id => modified[id]).ThenBy(id => id, StringComparer.Ordinal)
                    : ids.OrderBy(id => id, StringComparer.Ordinal);
                var page = ordered.Skip(options.Skip ?? 0).Take(options.Size ?? 50).ToList();

                // Like the index, return only the selected fields
                var withId = options.Select.Count == 0 || options.Select.Contains(SongIndex.SongIdField);
                var results = page.Select(id => SearchModelFactory.SearchResult(
                    withId
                        ? new SearchDocument { [SongIndex.SongIdField] = id, [SongIndex.PropertiesField] = "" }
                        : new SearchDocument { [SongIndex.PropertiesField] = "" },
                    1.0, null)).ToList();
                return Response.FromValue(
                    SearchModelFactory.SearchResults(results, null, null, null, null),
                    Mock.Of<Response>());
            });
        return mock;
    }

    // `count` songs with distinct Modified ticks, so "Modified desc" is a known order
    private static Dictionary<string, int> Songs(int count) =>
        Enumerable.Range(0, count).ToDictionary(_ => Guid.NewGuid().ToString(), i => i);

    private static List<Guid> NewestFirst(Dictionary<string, int> modified) =>
        [.. modified.OrderByDescending(kv => kv.Value).Select(kv => Guid.Parse(kv.Key))];

    private static (FakeSongIndex Index, List<Request> Requests) Index(Dictionary<string, int> modified)
    {
        var requests = new List<Request>();
        return (new FakeSongIndex(FakeClient(modified, requests).Object, Mock.Of<ISearchServiceManager>()),
            requests);
    }

    private static SearchOptions ByModified()
    {
        var options = new SearchOptions();
        options.OrderBy.Add("Modified desc");
        return options;
    }

    [TestMethod]
    public async Task StreamAllFromSnapshot_EditsLeaveTheFilter_VisitsEverySongInTheChosenOrder()
    {
        var modified = Songs(2500);
        var expected = NewestFirst(modified);
        var (index, _) = Index(modified);

        var visited = new List<Guid>();
        await foreach (var song in index.StreamAllFromSnapshot(null, ByModified(), CruftFilter.AllCruft))
        {
            visited.Add(song.SongId);
            // The edit took this song out of the filter, as an added ISRC does for "no ISRC"
            _ = modified.Remove(song.SongId.ToString());
        }

        CollectionAssert.AreEqual(expected, visited);
    }

    [TestMethod]
    public async Task StreamAllFromSnapshot_EditsMoveSongsInTheSort_VisitsEachSongOnce()
    {
        var modified = Songs(2500);
        var expected = NewestFirst(modified);
        var (index, _) = Index(modified);

        var visited = new List<Guid>();
        var tick = modified.Count;
        await foreach (var song in index.StreamAllFromSnapshot(null, ByModified(), CruftFilter.AllCruft))
        {
            visited.Add(song.SongId);
            // The edit made this song the newest
            modified[song.SongId.ToString()] = tick++;
        }

        CollectionAssert.AreEqual(expected, visited);
    }

    [TestMethod]
    public async Task StreamAll_EditsLeaveTheFilter_SkipsSongs()
    {
        // The $skip behavior the snapshot replaces, kept as a record of why batches don't use it
        var modified = Songs(2500);
        var (index, _) = Index(modified);

        var visited = 0;
        await foreach (var song in index.StreamAll(null, ByModified(), CruftFilter.AllCruft))
        {
            visited += 1;
            _ = modified.Remove(song.SongId.ToString());
        }

        Assert.IsLessThan(2500, visited);
    }

    [TestMethod]
    public async Task StreamAllFromSnapshot_SongLeavesTheFilterBeforeItsLoad_IsSkipped()
    {
        var modified = Songs(600);
        var expected = NewestFirst(modified);
        var (index, _) = Index(modified);
        var oldest = expected[^1];

        var visited = new List<Guid>();
        await foreach (var song in index.StreamAllFromSnapshot(null, ByModified(), CruftFilter.AllCruft))
        {
            visited.Add(song.SongId);
            // Someone else's edit takes the oldest song out of the filter
            _ = modified.Remove(oldest.ToString());
        }

        CollectionAssert.AreEqual(expected[..^1], visited);
    }

    [TestMethod]
    public async Task StreamAllFromSnapshot_Limit_ListsOnlyThatManyIds()
    {
        var modified = Songs(2500);
        var expected = NewestFirst(modified);
        var (index, requests) = Index(modified);

        var visited = new List<Guid>();
        await foreach (var song in index.StreamAllFromSnapshot(null, ByModified(), CruftFilter.AllCruft, 100))
        {
            visited.Add(song.SongId);
        }

        CollectionAssert.AreEqual(expected[..100], visited);
        Assert.HasCount(1, requests.Where(r => r.Select == SongIndex.SongIdField), "One id page");
    }

    [TestMethod]
    public async Task StreamAllFromSnapshot_KeepsTheCallersFilterAndOrderAndRestoresTheOptions()
    {
        var (index, requests) = Index(Songs(1500));
        var options = new SearchOptions
        {
            Filter = "Purchase/any(t: t eq 'I')", Size = 25, Skip = 50, IncludeTotalCount = true,
        };
        options.OrderBy.Add("Modified desc");

        await foreach (var _ in index.StreamAllFromSnapshot(null, options, CruftFilter.AllCruft))
        {
        }

        // Two id pages, then the songs 250 at a time
        Assert.HasCount(2 + 6, requests);
        foreach (var r in requests.Take(2))
        {
            Assert.AreEqual("Purchase/any(t: t eq 'I')", r.Filter);
            Assert.AreEqual("Modified desc, SongId asc", r.OrderBy);
            Assert.AreEqual(SongIndex.SongIdField, r.Select);
        }

        Assert.AreEqual(0, requests[0].Skip);
        Assert.AreEqual(1000, requests[1].Skip);
        foreach (var r in requests.Skip(2))
        {
            StringAssert.StartsWith(r.Filter, "(Purchase/any(t: t eq 'I')) and search.in(SongId, '");
            Assert.AreEqual(250, r.Size);
        }

        Assert.AreEqual("Purchase/any(t: t eq 'I')", options.Filter);
        Assert.AreEqual(25, options.Size);
        Assert.AreEqual(50, options.Skip);
        Assert.IsTrue(options.IncludeTotalCount);
        CollectionAssert.AreEqual(new[] { "Modified desc" }, options.OrderBy.ToList());
        Assert.IsEmpty(options.Select);
    }

    [TestMethod]
    public async Task StreamAllFromSnapshot_NoOrder_KeepsRelevanceOrder()
    {
        var (index, requests) = Index(Songs(10));

        await foreach (var _ in index.StreamAllFromSnapshot("waltz", new SearchOptions(), CruftFilter.AllCruft))
        {
        }

        Assert.AreEqual("search.score() desc, SongId asc", requests[0].OrderBy);
    }

    [TestMethod]
    public async Task StreamAllFromSnapshot_SelectWithoutSongId_StillMatchesTheSongs()
    {
        var (index, _) = Index(Songs(300));
        var options = new SearchOptions();
        options.Select.Add(SongIndex.PropertiesField);

        var visited = 0;
        await foreach (var _ in index.StreamAllFromSnapshot(null, options, CruftFilter.AllCruft))
        {
            visited += 1;
        }

        Assert.AreEqual(300, visited);
        CollectionAssert.AreEqual(new[] { SongIndex.PropertiesField }, options.Select.ToList());
    }
}
