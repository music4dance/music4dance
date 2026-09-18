using Newtonsoft.Json;

namespace m4dModels;

public record ArtistIndexEntry(string Name, int Songs);

public record ArtistIndexBucket(string Bucket, int Artists);

/// <summary>
/// Snapshot of every individual artist in the catalog with a song count, grouped by
/// <see cref="ArtistSplitter.ArtistKey"/> (so case and diacritic variants merge, displayed with the
/// most common spelling) and bucketed A-Z for browsing. Built from one streaming pass over the
/// index and cached - see architecture/individual-artists.md §9.
/// </summary>
public class ArtistIndex
{
    public const string OtherBucket = "#";

    // Credits that aren't browsable artists
    private static readonly HashSet<string> ExcludedKeys =
        ["various artists", "various", "unknown", "unknown artist", "va"];

    private static readonly string[] LeadingArticles =
        ["the ", "a ", "an ", "el ", "la ", "los ", "las ", "le ", "les ", "die ", "der ", "das "];

    private readonly List<(ArtistIndexEntry Entry, string Key, string SortKey)> _entries;

    private ArtistIndex(List<(ArtistIndexEntry Entry, string Key, string SortKey)> entries, DateTime built)
    {
        _entries = entries;
        Built = built;
    }

    public DateTime Built { get; }

    public int Count => _entries.Count;

    public static ArtistIndex Build(IEnumerable<IReadOnlyList<string>> songArtists, DateTime? built = null)
    {
        var groups = NewGroups();
        foreach (var artists in songArtists)
        {
            Accumulate(groups, artists);
        }
        return FromGroups(groups, built ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Groups as the stream arrives rather than draining it into a list first. The catalog is
    /// ~104K songs and the site runs on a small instance: buffering the whole stream cost about
    /// twice the memory of the snapshot it was building. See architecture/individual-artists.md §9.5.
    /// </summary>
    public static async Task<ArtistIndex> BuildAsync(IAsyncEnumerable<IReadOnlyList<string>> songArtists,
        CancellationToken cancellationToken = default)
    {
        var groups = NewGroups();
        await foreach (var artists in songArtists.WithCancellation(cancellationToken))
        {
            Accumulate(groups, artists);
        }
        return FromGroups(groups, DateTime.UtcNow);
    }

    private static Dictionary<string, (Dictionary<string, int> Spellings, int Songs)> NewGroups() => [];

    private static void Accumulate(
        Dictionary<string, (Dictionary<string, int> Spellings, int Songs)> groups,
        IReadOnlyList<string> artists)
    {
        foreach (var name in artists
                     .Select(ArtistSplitter.CleanName)
                     .Where(n => n.Length > 0)
                     .DistinctBy(ArtistSplitter.ArtistKey))
        {
            var key = ArtistSplitter.ArtistKey(name);
            if (ExcludedKeys.Contains(key))
            {
                continue;
            }

            if (!groups.TryGetValue(key, out var group))
            {
                group = (new Dictionary<string, int>(StringComparer.Ordinal), 0);
            }

            group.Spellings[name] = group.Spellings.GetValueOrDefault(name) + 1;
            groups[key] = (group.Spellings, group.Songs + 1);
        }
    }

    private static ArtistIndex FromGroups(
        Dictionary<string, (Dictionary<string, int> Spellings, int Songs)> groups, DateTime built)
    {
        var entries = groups
            .Select(g =>
            {
                var name = g.Value.Spellings
                    .OrderByDescending(s => s.Value)
                    .ThenBy(s => s.Key, StringComparer.Ordinal)
                    .First().Key;
                return (Entry: new ArtistIndexEntry(name, g.Value.Songs), g.Key, SortKey: SortKey(name));
            })
            .OrderBy(e => e.SortKey, StringComparer.Ordinal)
            .ThenBy(e => e.Entry.Name, StringComparer.Ordinal)
            .ToList();

        return new ArtistIndex(entries, built);
    }

    /// <summary>
    /// The snapshot as JSON: the display name and song count per artist, plus when it was built.
    /// The match and sort keys aren't stored because they're derived from the name, which keeps
    /// the file at roughly half a megabyte for the whole catalog.
    /// </summary>
    public string SaveToJson() =>
        JsonConvert.SerializeObject(new ArtistIndexFile
        {
            Built = Built,
            Artists = _entries.ToDictionary(e => e.Entry.Name, e => e.Entry.Songs, StringComparer.Ordinal),
        }, FileSettings);

    /// <summary>
    /// Reads back a <see cref="SaveToJson"/> snapshot, keeping its original <see cref="Built"/>
    /// time so an aged file is recognized as stale and triggers a rebuild. Returns null for
    /// anything it can't read - a snapshot is a cache, and a bad one is worth no more than a miss.
    /// </summary>
    public static ArtistIndex LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        ArtistIndexFile file;
        try
        {
            file = JsonConvert.DeserializeObject<ArtistIndexFile>(json, FileSettings);
        }
        catch (JsonException)
        {
            return null;
        }

        if (file?.Artists == null || file.Artists.Count == 0)
        {
            return null;
        }

        // Already grouped when it was written, so this re-derives the keys and re-sorts rather
        // than re-grouping - the same ordering FromGroups produces.
        var entries = file.Artists
            .Select(a => (Entry: new ArtistIndexEntry(a.Key, a.Value),
                Key: ArtistSplitter.ArtistKey(a.Key), SortKey: SortKey(a.Key)))
            .OrderBy(e => e.SortKey, StringComparer.Ordinal)
            .ThenBy(e => e.Entry.Name, StringComparer.Ordinal)
            .ToList();

        return new ArtistIndex(entries, file.Built);
    }

    // Built is compared against DateTime.UtcNow to decide staleness, so the round trip has to
    // keep it in UTC. Left to Newtonsoft's defaults it comes back as local time, which would
    // shift the snapshot's age by the server's offset - silently, and only outside UTC.
    private static readonly JsonSerializerSettings FileSettings = new()
    {
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
    };

    private class ArtistIndexFile
    {
        [JsonProperty("built")]
        public DateTime Built { get; set; }

        [JsonProperty("artists")]
        public Dictionary<string, int> Artists { get; set; }
    }

    /// <summary>
    /// Alphabetizing key: artist key without a leading article ("The Beatles" sorts under B).
    /// </summary>
    public static string SortKey(string name)
    {
        var key = ArtistSplitter.ArtistKey(name);
        var article = LeadingArticles.FirstOrDefault(a => key.StartsWith(a, StringComparison.Ordinal) && key.Length > a.Length);
        return article == null ? key : key[article.Length..];
    }

    public static string BucketOf(string name)
    {
        var sortKey = SortKey(name);
        return sortKey.Length > 0 && sortKey[0] is >= 'a' and <= 'z'
            ? char.ToUpperInvariant(sortKey[0]).ToString()
            : OtherBucket;
    }

    public static string NormalizeBucket(string bucket)
    {
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return null;
        }

        var c = char.ToUpperInvariant(bucket.Trim()[0]);
        return c is >= 'A' and <= 'Z' ? c.ToString() : OtherBucket;
    }

    public IReadOnlyList<ArtistIndexBucket> Buckets(int minSongs = 1)
    {
        var counts = _entries
            .Where(e => e.Entry.Songs >= minSongs)
            .GroupBy(e => BucketOf(e.Entry.Name))
            .ToDictionary(g => g.Key, g => g.Count());

        return
        [
            .. Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).Append(OtherBucket)
                .Select(b => new ArtistIndexBucket(b, counts.GetValueOrDefault(b)))
        ];
    }

    public IReadOnlyList<ArtistIndexEntry> InBucket(string bucket, int minSongs = 1)
    {
        var normalized = NormalizeBucket(bucket);
        return
        [
            .. _entries
                .Where(e => e.Entry.Songs >= minSongs && BucketOf(e.Entry.Name) == normalized)
                .Select(e => e.Entry)
        ];
    }

    /// <summary>
    /// Case- and diacritic-insensitive substring search; prefix matches first, then by song count.
    /// </summary>
    public IReadOnlyList<ArtistIndexEntry> Search(string query, int minSongs = 1, int limit = 200)
    {
        var key = ArtistSplitter.ArtistKey(query);
        if (key.Length == 0)
        {
            return [];
        }

        return
        [
            .. _entries
                .Where(e => e.Entry.Songs >= minSongs && e.Key.Contains(key, StringComparison.Ordinal))
                .OrderByDescending(e => e.Key.StartsWith(key, StringComparison.Ordinal) ||
                    e.SortKey.StartsWith(key, StringComparison.Ordinal))
                .ThenByDescending(e => e.Entry.Songs)
                .ThenBy(e => e.SortKey, StringComparer.Ordinal)
                .Take(limit)
                .Select(e => e.Entry)
        ];
    }

    public IReadOnlyList<ArtistIndexEntry> Top(int count) =>
        [.. _entries.OrderByDescending(e => e.Entry.Songs).ThenBy(e => e.SortKey, StringComparer.Ordinal)
            .Take(count).Select(e => e.Entry)];
}
