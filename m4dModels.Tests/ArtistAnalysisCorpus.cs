namespace m4dModels.Tests;

/// <summary>
/// One song as seen in a production index backup (/Admin/IndexBackup ->
/// local/index-YYYY-MM-DD.txt): the effective title and artist after replaying the property log,
/// plus the first Spotify track id so <see cref="ArtistSplitterGroundTruth"/> can look it up.
/// </summary>
internal record CorpusSong(Guid SongId, string Title, string Artist, string SpotifyTrackId);

/// <summary>
/// Loads an index backup for the manual artist analyses (see architecture/individual-artists.md §12).
/// Shared so the accuracy sample and the report harness always replay a backup line the same way.
/// </summary>
internal static class ArtistAnalysisCorpus
{
    public const string IndexVariable = "M4D_ARTIST_ANALYSIS_INDEX";

    /// <summary>
    /// The backup file named by <see cref="IndexVariable"/>, or null when it isn't configured.
    /// </summary>
    public static string IndexPath
    {
        get
        {
            var path = Environment.GetEnvironmentVariable(IndexVariable);
            return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : path;
        }
    }

    public static List<CorpusSong> Load(string path) =>
        [.. File.ReadLines(path).Select(ParseLine).Where(s => s != null)];

    public static ArtistKnowledge Knowledge(List<CorpusSong> songs, int minimumSongs) =>
        ArtistKnowledge.FromCredits(songs.Select(s => (s.Title, s.Artist)), minimumSongs);

    /// <summary>
    /// Effective title/artist from one backup line, applying the scalar rule that a pseudo-user
    /// write never overrides a real user's. Returns null for deleted songs.
    /// </summary>
    private static CorpusSong ParseLine(string line)
    {
        var ich = Song.TryParseId(line, out var songId);
        var properties = SongProperty.Load(ich > 0 ? line[ich..] : line);

        string title = null;
        string artist = null;
        var spotify = new List<string>();
        var userTitle = false;
        var userArtist = false;
        var pseudo = false;

        foreach (var prop in properties)
        {
            switch (prop.BaseName)
            {
                case Song.UserField:
                case Song.UserProxy:
                    pseudo = prop.Value?.EndsWith("|P", StringComparison.OrdinalIgnoreCase) ?? false;
                    break;
                case Song.TitleField:
                    if (!pseudo || !userTitle)
                    {
                        title = prop.Value;
                        userTitle |= !pseudo;
                    }
                    break;
                case Song.ArtistField:
                    if (!pseudo || !userArtist)
                    {
                        artist = prop.Value;
                        userArtist |= !pseudo;
                    }
                    break;
                // Purchase:NN:SS is the Spotify track id for album NN
                case Song.PurchaseField:
                    if (IsSpotifyTrack(prop) && !spotify.Contains(prop.Value))
                    {
                        spotify.Add(prop.Value);
                    }
                    break;
                case Song.RemovedPurchaseField:
                    if (IsSpotifyTrack(prop))
                    {
                        _ = spotify.Remove(prop.Value);
                    }
                    break;
                case Song.DeleteCommand:
                    if (string.IsNullOrEmpty(prop.Value) ||
                        string.Equals(prop.Value, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        title = null;
                    }
                    break;
            }
        }

        return string.IsNullOrWhiteSpace(title)
            ? null
            : new CorpusSong(songId, title, artist, spotify.FirstOrDefault());
    }

    private static bool IsSpotifyTrack(SongProperty prop) =>
        !string.IsNullOrWhiteSpace(prop.Value) &&
        string.Equals(prop.Qualifier, "SS", StringComparison.OrdinalIgnoreCase);
}
