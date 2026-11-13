namespace m4dModels;

/// <summary>
/// The title, artist credit and individual artists a song should end up with. Artists is null
/// when the individual artists shouldn't change.
/// </summary>
public record SongNames(string Title, string Artist, IReadOnlyList<string> Artists = null);

/// <summary>
/// Moves featured artists out of a merged song's title and into its individual artists:
/// "Song (feat. Crazy Man)" by "Y" becomes "Song" by "Y" with artists Y and Crazy Man. Used by
/// the shared-id review (architecture/songs/shared-id-review.md); meant to move into the
/// automatic merges once it has been refined there.
/// </summary>
public static class FeaturedCleanup
{
    /// <summary>
    /// The names <paramref name="merged"/> should land on: its title without a feat. clause, its
    /// artist credit, and its individual artists followed by the artists the songs it was merged
    /// from feature (in their titles or credits), each artist listed once.
    /// </summary>
    public static SongNames Propose(Song merged, IEnumerable<Song> sources)
    {
        var artists = new List<string>();
        void Add(IEnumerable<string> names) =>
            artists.AddRange(names.Where(n => !artists.Any(a => SameArtist(a, n))));

        Add(merged.Artists ?? ArtistSplitter.Split(merged.Artist, merged.Title).Artists);
        foreach (var song in sources ?? [])
        {
            Add(FeaturedArtists(song));
        }

        return new SongNames(ArtistSplitter.RemoveTitleFeaturing(merged.Title), merged.Artist, artists);
    }

    /// <summary>
    /// The artists a song features in a feat. clause of its title or artist credit, without its
    /// primary artists.
    /// </summary>
    public static IReadOnlyList<string> FeaturedArtists(Song song)
    {
        var primary = ArtistSplitter.Split(ArtistSplitter.RemoveArtistFeaturing(song.Artist)).Artists;
        return [.. ArtistSplitter.Split(song.Artist, song.Title).Artists
            .Where(n => !primary.Any(p => SameArtist(p, n)))];
    }

    /// <summary>
    /// Whether the titles and artists all match once their feat. clauses are removed, compared
    /// the way exact title merges compare them (Song.CreateTitleHash).
    /// </summary>
    public static bool MatchWithoutFeaturing(IReadOnlyList<Song> songs)
    {
        if (songs.Count < 2 || songs.Any(s => string.IsNullOrWhiteSpace(s.Artist)))
        {
            return false;
        }

        var keys = songs.Select(s => (
            Title: Song.CreateTitleHash(ArtistSplitter.RemoveTitleFeaturing(s.Title)),
            Artist: Song.CreateTitleHash(ArtistSplitter.RemoveArtistFeaturing(s.Artist)))).ToList();
        return keys.All(k => k == keys[0]);
    }

    private static bool SameArtist(string a, string b) =>
        ArtistSplitter.ArtistKey(a) == ArtistSplitter.ArtistKey(b);
}
