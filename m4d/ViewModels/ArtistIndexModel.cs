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
