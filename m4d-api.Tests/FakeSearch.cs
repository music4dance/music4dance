using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using DanceLibrary;
using Moq;

namespace m4dApi.Tests;

/// <summary>
/// A SearchClient that returns queued documents, one batch per query, and records each query.
/// </summary>
internal sealed class FakeSearch
{
    private static readonly Lazy<Dances> s_dances = new(() => Dances.Load(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dances.json")),
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dancegroups.json"))));

    private readonly Queue<SearchDocument[]> _batches = new();

    public FakeSearch()
    {
        Client
            .Setup(c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string search, SearchOptions options, CancellationToken _) =>
            {
                Queries.Add((search, options));
                var docs = _batches.Count > 0 ? _batches.Dequeue() : [];
                var results = SearchModelFactory.SearchResults(
                    docs.Select(d => SearchModelFactory.SearchResult(d, 1.0, null)).ToList(),
                    docs.Length, null, null, Mock.Of<Response>());
                return Response.FromValue(results, Mock.Of<Response>());
            });
    }

    public static Dances Dances => s_dances.Value;

    public Mock<SearchClient> Client { get; } = new();

    public List<(string Search, SearchOptions Options)> Queries { get; } = [];

    public SongResolver Resolver => new(Client.Object, Dances);

    public FakeSearch Returns(params SearchDocument[] docs)
    {
        _batches.Enqueue(docs);
        return this;
    }

    public FakeSearch Throws(int status)
    {
        Client
            .Setup(c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(status, "Search failed"));
        return this;
    }

    /// <summary>
    /// A document shaped like a search response: complex fields as nested SearchDocuments and
    /// collections as object arrays.
    /// </summary>
    public static SearchDocument Song(
        string id, string title = "Blue Bayou", string artist = "Linda Ronstadt", int? length = 231,
        double? bpm = 120, string[] serviceIds = null, string[] tempoTags = null,
        params (string Dance, int Votes, double? Bpm, string[] Styles)[] dances)
    {
        var doc = new SearchDocument
        {
            [IndexFields.SongId] = id,
            [IndexFields.Title] = title,
            [IndexFields.Artist] = artist,
            [IndexFields.Length] = length,
            [IndexFields.Tempo] = bpm,
            [IndexFields.TempoTags] = (tempoTags ?? ["4/4"]).Cast<object>().ToArray(),
            [IndexFields.ServiceIds] = (serviceIds ?? []).Cast<object>().ToArray(),
        };
        foreach (var (dance, votes, danceBpm, styles) in dances)
        {
            doc[IndexFields.Dance(dance)] = new SearchDocument
            {
                [IndexFields.Votes] = votes,
                [IndexFields.DanceTempo] = danceBpm,
                [IndexFields.StyleTags] = (styles ?? []).Cast<object>().ToArray(),
            };
        }
        var confirmed = dances.Where(d => d.Votes > 0).Sum(d => d.Votes);
        doc[IndexFields.Dance(IndexFields.AllDances)] = new SearchDocument
        {
            [IndexFields.Votes] = confirmed == 0 ? null : confirmed,
        };
        return doc;
    }
}
