namespace m4dModels;

/// <summary>
/// In-memory <see cref="IArtistKnowledge"/> built from counts of songs per artist name
/// (case-insensitive). Used by batch/analysis runs that can see the whole catalog.
/// </summary>
public class ArtistKnowledge(int minimumSongs = 1) : IArtistKnowledge
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    public int MinimumSongs { get; } = minimumSongs;

    public void Add(string name, int count = 1)
    {
        var key = ArtistSplitter.CleanName(name);
        if (key.Length == 0)
        {
            return;
        }

        _counts[key] = _counts.GetValueOrDefault(key) + count;
    }

    public int Count(string name) => _counts.GetValueOrDefault(ArtistSplitter.CleanName(name));

    public bool IsKnownArtist(string name) => Count(name) >= MinimumSongs;

    // Rules that don't depend on corpus evidence; names they produce are trustworthy evidence
    private static readonly HashSet<string> StrongRules =
        ["feat", "title-feat", "semicolon", "slash", "comma-list", "list-names"];

    /// <summary>
    /// Builds catalog-wide knowledge from every song's (Title, Artist): each credit counts as a
    /// standalone artist, plus every individual artist produced by an evidence-free split.
    /// </summary>
    public static ArtistKnowledge FromCredits(
        IEnumerable<(string Title, string Artist)> songs, int minimumSongs = 1)
    {
        var knowledge = new ArtistKnowledge(minimumSongs);
        foreach (var (title, artist) in songs)
        {
            knowledge.Add(artist);
            var split = ArtistSplitter.Split(artist, title);
            if (!split.IsSplit(artist) || !split.Rules.Any(StrongRules.Contains))
            {
                continue;
            }

            var credit = ArtistSplitter.CleanName(artist);
            foreach (var name in split.Artists.Where(a =>
                         !string.Equals(a, credit, StringComparison.OrdinalIgnoreCase)))
            {
                knowledge.Add(name);
            }
        }
        return knowledge;
    }
}

/// <summary>
/// Records every name <see cref="ArtistSplitter"/> asks about, so the evidence can be fetched in
/// one query before splitting for real.
/// </summary>
public class RecordingArtistKnowledge : IArtistKnowledge
{
    public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsKnownArtist(string name)
    {
        var clean = ArtistSplitter.CleanName(name);
        if (clean.Length > 0)
        {
            _ = Names.Add(clean);
        }
        return false;
    }
}
