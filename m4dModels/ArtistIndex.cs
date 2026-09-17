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
        var groups = new Dictionary<string, (Dictionary<string, int> Spellings, int Songs)>();
        foreach (var artists in songArtists)
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

        return new ArtistIndex(entries, built ?? DateTime.UtcNow);
    }

    public static async Task<ArtistIndex> BuildAsync(IAsyncEnumerable<IReadOnlyList<string>> songArtists,
        CancellationToken cancellationToken = default)
    {
        var all = new List<IReadOnlyList<string>>();
        await foreach (var artists in songArtists.WithCancellation(cancellationToken))
        {
            all.Add(artists);
        }
        return Build(all);
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
