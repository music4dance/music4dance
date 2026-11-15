namespace m4dModels;

/// <summary>
/// The title, artist credit and individual artists a song should end up with. Artists is null
/// when the individual artists shouldn't change.
/// </summary>
public record SongNames(string Title, string Artist, IReadOnlyList<string> Artists = null);

/// <summary>
/// Moves featured artists out of a merged song's title and into its individual artists:
/// "Song (feat. Crazy Man)" by "Y" becomes "Song" by "Y" with artists Y and Crazy Man. Used by
/// the shared-id review, on its page and in its Merge all tasks
/// (architecture/songs/shared-id-review.md).
/// </summary>
public static class FeaturedCleanup
{
    /// <summary>
    /// The names <paramref name="merged"/> should land on: its title without a feat. clause, an
    /// artist credit, and its individual artists followed by the artists the songs it was merged
    /// from feature (in their titles or credits), each artist listed once. The credit is the
    /// merged song's unless it leaves some of those artists out and one of the songs it was
    /// merged from credits them all: "X" becomes "X feat. Y" when one song has that credit and
    /// another features Y in its title.
    /// </summary>
    public static SongNames Propose(Song merged, IEnumerable<Song> sources)
    {
        var songs = sources?.ToList() ?? [];
        var artists = new List<string>();
        void Add(IEnumerable<string> names) =>
            artists.AddRange(names.Where(n => !artists.Any(a => SameArtist(a, n))));

        Add(merged.Artists ?? ArtistSplitter.Split(merged.Artist, merged.Title).Artists);
        foreach (var song in songs)
        {
            Add(FeaturedArtists(song));
        }

        return new SongNames(ArtistSplitter.RemoveTitleFeaturing(merged.Title),
            Credit(merged.Artist, songs, artists), artists);
    }

    // The credit when it names every one of the artists, otherwise the first of the songs'
    // credits that does, otherwise the credit
    private static string Credit(string credit, IEnumerable<Song> songs, IReadOnlyList<string> artists)
    {
        bool NamesAll(string c)
        {
            if (string.IsNullOrWhiteSpace(c))
            {
                return false;
            }

            var named = ArtistSplitter.Split(c).Artists;
            return artists.All(a => named.Any(n => SameArtist(n, a)));
        }

        return NamesAll(credit) ? credit : songs.Select(s => s.Artist).FirstOrDefault(NamesAll) ?? credit;
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
