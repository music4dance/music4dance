using System.Text.RegularExpressions;

namespace m4dModels;

/// <summary>
/// The title, artist credit, individual artists and tempo a song should end up with. Artists is
/// null when the individual artists shouldn't change, and Tempo when the tempo shouldn't.
/// </summary>
public record SongNames(string Title, string Artist, IReadOnlyList<string> Artists = null, decimal? Tempo = null);

/// <summary>
/// Moves featured artists out of a merged song's title and into its credit and individual
/// artists: "Song (feat. Crazy Man)" by "Y" becomes "Song" by "Y feat. Crazy Man" with artists Y
/// and Crazy Man. Used by
/// the shared-id review, on its page and in its Merge all tasks
/// (architecture/songs/shared-id-review.md).
/// </summary>
public static class FeaturedCleanup
{
    /// <summary>
    /// The names <paramref name="merged"/> should land on: its title without a feat. clause or
    /// the remaster and single-version notes at its end (SharedIdMatcher.WithoutVersionNotes),
    /// cut back to the shortest of the songs' titles it starts with ("Song - Salsa Version" and
    /// "Song" give "Song"), an
    /// artist credit, and its individual artists followed by the artists the songs it was merged
    /// from feature (in their titles or credits), each artist listed once; when the songs'
    /// credits only leave artists out of the fullest one ("X" and "X &amp; Y"), the fullest one's
    /// artists come after the merged song's. The credit is the merged song's unless it leaves
    /// some of those artists out and one of the songs it was merged from credits them all: "X"
    /// becomes "X feat. Y" when one song has that credit and another features Y in its title.
    /// Artists a title features (the merged song's or a source's) that the credit still leaves
    /// out are then added to it with the title's own word: "Song (ft. Y)" by "X" gives "X ft. Y",
    /// and "X feat. Y" with Z featured gives "X feat. Y &amp; Z".
    /// A leader's backing group isn't an artist of its own: "Benny Goodman and His Orchestra" (or
    /// "Charlie Parker Quartet", when another song is credited to Charlie Parker) is Benny Goodman
    /// in the individual artists, and a credit naming only the leader takes the group from the
    /// song that credits it ("Benny Goodman and His Orchestra").
    /// <paramref name="knowledge"/> is evidence for splitting credits like "X &amp; Y", which
    /// otherwise stay one artist (a music service's list of the track's artists, say).
    /// </summary>
    public static SongNames Propose(Song merged, IEnumerable<Song> sources, IArtistKnowledge knowledge = null)
    {
        var songs = sources?.ToList() ?? [];
        knowledge = WithGroupLeaders(knowledge, [merged, .. songs]);
        var artists = new List<string>();
        // A leader's group is the leader: "Benny Goodman and His Orchestra" is Benny Goodman
        void Add(IEnumerable<string> names) =>
            artists.AddRange(names.Select(n => ArtistSplitter.RemoveBackingGroup(n, knowledge))
                .Where(n => !artists.Any(a => SameArtist(a, n))));

        Add(merged.Artists ?? ArtistSplitter.Split(merged.Artist, merged.Title, knowledge).Artists);
        // When the songs' credits only leave artists out ("X" and "X & Y"), all of them
        if (songs.Count > 0)
        {
            var sets = songs.Select(s => IndividualArtists(s, knowledge)).ToList();
            var fullest = sets.MaxBy(a => a.Count);
            if (sets.All(set => set.All(n => fullest.Any(f => SameArtist(f, n)))))
            {
                Add(fullest);
            }
        }

        foreach (var song in songs)
        {
            Add(FeaturedArtists(song, knowledge));
        }

        var credit = WithBackingGroup(Credit(merged.Artist, songs, artists, knowledge), songs, knowledge);
        return new SongNames(Title(merged, songs), WithTitleFeaturing(credit, [merged, .. songs], knowledge),
            artists);
    }

    /// <summary>
    /// <paramref name="knowledge"/>, also knowing the leaders of the songs' "Charlie Parker
    /// Quartet" credits that another song credits, on their own ("Charlie Parker") or with a
    /// possessive group ("Charlie Parker and His Orchestra"), so the
    /// splitter takes the group off (ArtistSplitter.RemoveBackingGroup).
    /// </summary>
    public static IArtistKnowledge WithGroupLeaders(IArtistKnowledge knowledge, IEnumerable<Song> songs)
    {
        var credits = (songs ?? []).Where(s => s != null)
            .Select(s => ArtistSplitter.RemoveArtistFeaturing(s.Artist ?? ""))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();
        // A leader is known when another song is credited to them, on their own or with their
        // group ("Benny Goodman and His Orchestra")
        var known = Knowing([.. credits, .. credits.Select(c => ArtistSplitter.RemoveBackingGroup(c))]);
        var leaders = credits
            .Where(c => ArtistSplitter.RemoveBackingGroup(c) == c)
            .Select(c => (Credit: c, Leader: ArtistSplitter.RemoveBackingGroup(c, known)))
            .Where(c => c.Leader != c.Credit)
            .Select(c => c.Leader)
            .ToList();
        return leaders.Count == 0 ? knowledge : new EitherKnowledge(knowledge, Knowing(leaders));
    }

    // Known to either
    private sealed class EitherKnowledge(IArtistKnowledge first, IArtistKnowledge second) : IArtistKnowledge
    {
        public bool IsKnownArtist(string name) =>
            first?.IsKnownArtist(name) == true || second.IsKnownArtist(name);
    }

    // The credit with its backing group, when it names only the leader and one of the songs
    // credits the group too: "Benny Goodman" becomes "Benny Goodman and His Orchestra"
    private static string WithBackingGroup(string credit, IEnumerable<Song> songs, IArtistKnowledge knowledge)
    {
        if (string.IsNullOrWhiteSpace(credit) || ArtistSplitter.RemoveBackingGroup(credit, knowledge) != credit)
        {
            return credit;
        }

        return songs.Select(s => s.Artist).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) &&
            !SameArtist(c, credit) && SameArtist(ArtistSplitter.RemoveBackingGroup(c, knowledge), credit)) ?? credit;
    }

    // The merged title without its feat. clause or version notes, cut back to the shortest of the
    // songs' titles (cleaned the same way) that it starts with, word for word: "Song Name - Salsa
    // Version" merged with "Song Name" gives "Song Name"
    private static string Title(Song merged, IEnumerable<Song> songs)
    {
        static string Clean(string title) =>
            SharedIdMatcher.WithoutVersionNotes(ArtistSplitter.RemoveTitleFeaturing(title))?.Trim();

        var title = Clean(merged.Title);
        if (string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        var shortest = songs.Select(s => Clean(s.Title))
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length < title.Length &&
                title.StartsWith(t, StringComparison.OrdinalIgnoreCase) && char.IsLetterOrDigit(t[^1]) &&
                !char.IsLetterOrDigit(title[t.Length]))
            .MinBy(t => t.Length);
        return shortest == null ? title : title[..shortest.Length];
    }

    // The credit with the artists the songs' titles feature that it doesn't name, after the
    // first of those titles' feat. word
    private static string WithTitleFeaturing(string credit, IReadOnlyList<Song> songs, IArtistKnowledge knowledge)
    {
        if (string.IsNullOrWhiteSpace(credit))
        {
            return credit;
        }

        var featuring = songs
            .Select(s => (Song: s, Word: ArtistSplitter.TitleFeaturingWord(s.Title)))
            .Where(s => s.Word != null)
            .ToList();
        if (featuring.Count == 0)
        {
            return credit;
        }

        var named = ArtistSplitter.Split(credit, null, knowledge).Artists;
        var creditKey = ArtistSplitter.ArtistKey(credit);
        // Named by the credit, split or not: the splitter keeps "X & Y" whole without evidence
        bool Names(string name) => named.Any(n => SameArtist(n, name)) ||
            Regex.IsMatch(creditKey, $@"(?<!\w){Regex.Escape(ArtistSplitter.ArtistKey(name))}(?!\w)");

        var missing = new List<string>();
        foreach (var name in featuring.SelectMany(s => FeaturedArtists(s.Song, knowledge)))
        {
            if (!Names(name) && !missing.Any(m => SameArtist(m, name)))
            {
                missing.Add(name);
            }
        }

        if (missing.Count == 0)
        {
            return credit;
        }

        var names = missing.Count == 1 ? missing[0]
            : $"{string.Join(", ", missing.Take(missing.Count - 1))} & {missing[^1]}";
        var hasFeaturing = ArtistSplitter.RemoveArtistFeaturing(credit) != credit;
        return hasFeaturing ? $"{credit} & {names}" : $"{credit} {featuring[0].Word} {names}";
    }

    /// <summary>
    /// The song's individual artists: those recorded in its log (Song.Artists, by a person, a
    /// music service or artist-bot) and the ones its feat. clauses add, else its credit split
    /// with any feat. clause in its title.
    /// </summary>
    public static IReadOnlyList<string> IndividualArtists(Song song, IArtistKnowledge knowledge = null)
    {
        if (song.Artists is not { Count: > 0 })
        {
            return ArtistSplitter.Split(song.Artist, song.Title, knowledge).Artists;
        }

        var featured = FeaturedArtists(song, knowledge).Where(f => !song.Artists.Any(a => SameArtist(a, f)));
        return [.. song.Artists, .. featured];
    }

    /// <summary>Evidence that each of the names is an artist, for ArtistSplitter.Split.</summary>
    public static ArtistKnowledge Knowing(IEnumerable<string> names)
    {
        var knowledge = new ArtistKnowledge();
        foreach (var name in names ?? [])
        {
            knowledge.Add(name);
        }

        return knowledge;
    }

    // The credit when it names every one of the artists, otherwise the first of the songs'
    // credits that does, otherwise the credit
    private static string Credit(string credit, IEnumerable<Song> songs, IReadOnlyList<string> artists,
        IArtistKnowledge knowledge)
    {
        bool NamesAll(string c)
        {
            if (string.IsNullOrWhiteSpace(c))
            {
                return false;
            }

            var named = ArtistSplitter.Split(c, null, knowledge).Artists;
            return artists.All(a => named.Any(n => SameArtist(n, a)));
        }

        return NamesAll(credit) ? credit : songs.Select(s => s.Artist).FirstOrDefault(NamesAll) ?? credit;
    }

    /// <summary>
    /// The artists a song features in a feat. clause of its title or artist credit, without its
    /// primary artists.
    /// </summary>
    public static IReadOnlyList<string> FeaturedArtists(Song song, IArtistKnowledge knowledge = null)
    {
        var primary = ArtistSplitter.Split(ArtistSplitter.RemoveArtistFeaturing(song.Artist), null, knowledge).Artists;
        return [.. ArtistSplitter.Split(song.Artist, song.Title, knowledge).Artists
            .Where(n => !primary.Any(p => SameArtist(p, n)))];
    }

    /// <summary>
    /// Whether the titles and artists all match once their feat. clauses (and the artists'
    /// backing groups, ArtistSplitter.RemoveBackingGroup) are removed, compared
    /// the way exact title merges compare them (Song.CreateTitleHash), the titles also without
    /// their version notes (SharedIdMatcher.TitleKey).
    /// </summary>
    public static bool MatchWithoutFeaturing(IReadOnlyList<Song> songs)
    {
        if (songs.Count < 2 || songs.Any(s => string.IsNullOrWhiteSpace(s.Artist)))
        {
            return false;
        }

        var knowledge = WithGroupLeaders(null, songs);
        var keys = songs.Select(s => (
            Title: SharedIdMatcher.TitleKey(s.Title),
            Artist: Song.CreateTitleHash(
                ArtistSplitter.RemoveBackingGroup(ArtistSplitter.RemoveArtistFeaturing(s.Artist), knowledge))))
            .ToList();
        return keys.All(k => k == keys[0]);
    }

    private static bool SameArtist(string a, string b) =>
        ArtistSplitter.ArtistKey(a) == ArtistSplitter.ArtistKey(b);
}
