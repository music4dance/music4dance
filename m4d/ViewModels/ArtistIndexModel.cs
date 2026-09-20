namespace m4d.ViewModels;

public class ArtistIndexModel
{
    /// <summary>Selected A-Z (or "#") bucket; null when showing search results or the top artists</summary>
    public string Letter { get; set; }

    public string Query { get; set; }

    public int MinSongs { get; set; }

    /// <summary>True while the first snapshot is still being built; everything else is empty</summary>
    public bool Building { get; set; }

    public int TotalArtists { get; set; }

    public DateTime Built { get; set; }

    public List<ArtistIndexBucket> Buckets { get; set; }

    public List<ArtistIndexEntry> Artists { get; set; }

    /// <summary>
    /// The one artist a search matched, or null when it matched none, several, or wasn't a search.
    /// Landing on a single match means the visitor was looking someone up rather than browsing, so
    /// the page redirects to that artist instead of listing one entry.
    /// </summary>
    /// <remarks>
    /// A method rather than a property so it stays out of the JSON serialized to the Vue page -
    /// the client never sees this case, because the redirect happens before the page renders.
    /// </remarks>
    public string SoleSearchMatch() =>
        !string.IsNullOrWhiteSpace(Query) && Artists.Count == 1 ? Artists[0].Name : null;

    public static ArtistIndexModel Create(ArtistIndex index, string letter, string query, int minSongs)
    {
        if (index == null)
        {
            return new ArtistIndexModel
            {
                Letter = ArtistIndex.NormalizeBucket(letter),
                Query = query?.Trim(),
                MinSongs = minSongs,
                Building = true,
                Buckets = [],
                Artists = [],
            };
        }

        const int topCount = 100;

        var bucket = ArtistIndex.NormalizeBucket(letter);
        var artists = !string.IsNullOrWhiteSpace(query)
            ? index.Search(query, minSongs)
            : bucket != null
                ? index.InBucket(bucket, minSongs)
                : index.Top(topCount);

        return new ArtistIndexModel
        {
            Letter = string.IsNullOrWhiteSpace(query) ? bucket : null,
            Query = query?.Trim(),
            MinSongs = minSongs,
            TotalArtists = index.Count,
            Built = index.Built,
            Buckets = [.. index.Buckets(minSongs)],
            Artists = [.. artists],
        };
    }
}
