using System.Text.RegularExpressions;

namespace m4dModels;

/// <summary>
/// How closely the titles and artists of songs that share an id agree (SharedIdMatch), for the
/// shared-id review's tiers and Merge all (architecture/songs/shared-id-review.md).
/// </summary>
public static class SharedIdMatcher
{
    /// <summary>
    /// Songs whose lengths are further apart than this, in seconds, are almost certainly
    /// different recordings (a live take, an extended mix), so they aren't merged without review.
    /// The title-based merge candidates use a looser 20 seconds from the median
    /// (MergeManager.FilterLength).
    /// </summary>
    public const int LengthTolerance = 15;

    /// <summary>
    /// The longest of the songs' lengths minus the shortest, in seconds; 0 when fewer than two of
    /// them have a length.
    /// </summary>
    public static int LengthSpread(IReadOnlyList<Song> songs)
    {
        var lengths = songs.Where(s => s?.Length is > 0).Select(s => s.Length.Value).ToList();
        return lengths.Count < 2 ? 0 : lengths.Max() - lengths.Min();
    }

    public static SharedIdMatch Match(IReadOnlyList<Song> songs) =>
        TitleArtistMatch(songs) ? SharedIdMatch.Exact
        : FeaturedCleanup.MatchWithoutFeaturing(songs) ? SharedIdMatch.WithoutFeaturing
        : ArtistsMatch(songs) is { } artists ? artists
        : CloseMatch(songs) ? SharedIdMatch.Close
        : SharedIdMatch.Different;

    /// <summary>
    /// For songs whose titles match once their feat. clauses come out: SameArtists when every
    /// song names the same individual artists (FeaturedCleanup.IndividualArtists; the splitter
    /// drops backing groups, so "X and His Orchestra" is X), FewerArtists when they only share
    /// their lead artist ("X" and "X &amp; Y": without evidence the splitter can't tell "X &amp; Y"
    /// from a duo's name, so a music service has to confirm it); otherwise null.
    /// </summary>
    public static SharedIdMatch? ArtistsMatch(IReadOnlyList<Song> songs)
    {
        if (!AllHaveArtists(songs) || !TitlesMatchWithoutFeaturing(songs))
        {
            return null;
        }

        var sets = songs.Select(s => ArtistKeys(s)).ToList();
        if (sets.All(s => s.SetEquals(sets[0])))
        {
            return SharedIdMatch.SameArtists;
        }

        var lead = LeadKey(songs[0]);
        return songs.All(s => LeadKey(s) == lead) ? SharedIdMatch.FewerArtists : null;
    }

    /// <summary>
    /// The song's individual artists (FeaturedCleanup.IndividualArtists) by
    /// ArtistSplitter.ArtistKey.
    /// </summary>
    public static HashSet<string> ArtistKeys(Song song, IArtistKnowledge knowledge = null) =>
        [.. FeaturedCleanup.IndividualArtists(song, knowledge).Select(ArtistSplitter.ArtistKey)];

    // Anything that can join two artists in a credit
    private static readonly Regex LeadSeparator = new(
        @"\s*[,;/]\s*|\s+(?:&|\+|and|y|e|et|und|x|vs\.?|with)\s+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// The song's first individual artist, or the start of its credit up to anything that could
    /// join another artist to it.
    /// </summary>
    public static string Lead(Song song) =>
        song.Artists is { Count: > 0 } ? song.Artists[0]
            : LeadSeparator.Split(ArtistSplitter.RemoveArtistFeaturing(song.Artist ?? ""))[0].Trim();

    /// <summary>The song's Lead, by ArtistSplitter.ArtistKey.</summary>
    public static string LeadKey(Song song) => ArtistSplitter.ArtistKey(Lead(song));

    /// <summary>
    /// The title without its feat. clause or version notes (WithoutVersionNotes), as the title
    /// merge candidates compare titles (Song.CreateTitleHash).
    /// </summary>
    public static int TitleKey(string title) =>
        Song.CreateTitleHash(WithoutVersionNotes(ArtistSplitter.RemoveTitleFeaturing(title)));

    // A note at the end of a title that a service adds to the same recording: "- Remastered
    // 2011", "(2011 Remaster)", "[Digitally Remastered]", "- Remastered Version", "- Single
    // Version", after a dash, a colon or in brackets
    private static readonly Regex VersionNote = new(
        @"\s*(?:[-\u2013\u2014:]\s*|[\(\[]\s*)(?:(?:19|20)\d{2}\s+)?(?:digital(?:ly)?\s+)?" +
        @"(?:remaster(?:ed)?|single\s+version)(?:\s+(?:version|edition))?(?:\s+(?:19|20)\d{2})?" +
        @"(?:\s+version)?\s*[\)\]]?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// The title without the remaster and single-version notes at its end (VersionNote), which
    /// don't make a different recording; a title that is only a note comes back unchanged.
    /// </summary>
    public static string WithoutVersionNotes(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        var stripped = title;
        var next = VersionNote.Replace(stripped, "");
        while (next != stripped && next.Trim().Length > 0)
        {
            stripped = next;
            next = VersionNote.Replace(stripped, "");
        }

        return stripped;
    }

    private static bool TitlesMatchWithoutFeaturing(IReadOnlyList<Song> songs)
    {
        var title = TitleKey(songs[0].Title);
        return songs.All(s => TitleKey(s.Title) == title);
    }

    /// <summary>
    /// Every song has the same title (without version notes) and the same (non-empty) artist,
    /// compared the way the title merge candidates are (Song.CreateTitleHash).
    /// </summary>
    public static bool TitleArtistMatch(IReadOnlyList<Song> songs)
    {
        if (!AllHaveArtists(songs))
        {
            return false;
        }

        var title = Song.CreateTitleHash(WithoutVersionNotes(songs[0].Title));
        var artist = Song.CreateTitleHash(songs[0].Artist);
        return songs.All(s =>
            Song.CreateTitleHash(WithoutVersionNotes(s.Title)) == title && Song.CreateTitleHash(s.Artist) == artist);
    }

    /// <summary>
    /// Every pair of songs passes Song.TitleArtistMatch (the fuzzy match that filters service
    /// tracks), as given or with their feat. clauses removed.
    /// </summary>
    public static bool CloseMatch(IReadOnlyList<Song> songs)
    {
        if (!AllHaveArtists(songs))
        {
            return false;
        }

        static Song WithoutFeaturing(Song s) => new()
        {
            Title = WithoutVersionNotes(ArtistSplitter.RemoveTitleFeaturing(s.Title)),
            Artist = ArtistSplitter.RemoveArtistFeaturing(s.Artist)
        };

        var stripped = songs.Select(WithoutFeaturing).ToList();
        return Enumerable.Range(0, songs.Count).All(i => Enumerable.Range(i + 1, songs.Count - i - 1).All(j =>
            songs[i].TitleArtistMatch(songs[j].Title, songs[j].Artist) ||
            stripped[i].TitleArtistMatch(stripped[j].Title, stripped[j].Artist)));
    }

    private static bool AllHaveArtists(IReadOnlyList<Song> songs) =>
        songs.Count >= 2 && songs.All(s => s != null && !s.IsNull && !string.IsNullOrWhiteSpace(s.Artist));
}
