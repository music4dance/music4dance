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
}
