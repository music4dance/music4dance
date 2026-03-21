namespace m4dModels.Tests;

/// <summary>
/// Test implementation of SongIndex that captures EditSong calls for verification in tests.
/// Uses late-binding pattern to avoid circular dependency with DanceMusicService.
/// Create this first, pass to DanceMusicTester.CreateService, then call AttachToService.
/// </summary>
public class TestSongIndex : SongIndex
{
    private DanceMusicCoreService? _actualService;
    
    public List<EditSongCall> EditCalls { get; } = new();

    /// <summary>
    /// Creates a TestSongIndex with late-bound service.
    /// Must call AttachToService after the DanceMusicService is created.
    /// </summary>
    public TestSongIndex() : base()
    {
    }

    /// <summary>
    /// Attaches this TestSongIndex to the actual service after service creation.
    /// This resolves the circular dependency where TestSongIndex needs the service
    /// but the service needs TestSongIndex during construction.
    /// </summary>
    /// <param name="service">The DanceMusicService to attach to</param>
    public void AttachToService(DanceMusicCoreService service)
    {
        _actualService = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Override to use the actual service attached via AttachToService.
    /// This ensures all operations use the correct service context.
    /// </summary>
    public override DanceMusicCoreService DanceMusicService => 
        _actualService ?? throw new InvalidOperationException(
            "TestSongIndex.AttachToService must be called before using this index. " +
            "Pattern: var testIndex = new TestSongIndex(); var service = await CreateService(dbName, customSongIndex: testIndex); testIndex.AttachToService(service);");

    // Override the virtual EditSong method to capture calls
    public override async Task<bool> EditSong(ApplicationUser user, Song song, Song edit,
        IEnumerable<UserTag> tags = null)
    {
        // Capture the call
        EditCalls.Add(new EditSongCall(user, song, edit, tags?.ToList()));

        // Call the base implementation (which updates the song in memory)
        return await base.EditSong(user, song, edit, tags);
    }

    /// <summary>
    /// Override SaveSong to skip Azure Search updates in tests.
    /// Only updates DanceStats, does not call UpdateAzureIndex.
    /// </summary>
    public override async Task SaveSong(Song song, string id = "default")
    {
        // Update stats but skip Azure Search index update
        var stats = DanceMusicService.DanceStats;
        stats.UpdateSong(song);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Override SaveSongs to skip Azure Search updates in tests.
    /// Only updates DanceStats, does not call UpdateAzureIndex.
    /// </summary>
    public override async Task SaveSongs(IEnumerable<Song> songs, string id = "default")
    {
        if (songs == null || !songs.Any())
        {
            return;
        }

        // Update stats but skip Azure Search index update
        var stats = DanceMusicService.DanceStats;
        foreach (var song in songs)
        {
            stats.UpdateSong(song);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Override LoadLightSongsStreamingAsync to provide in-memory light songs for merge testing.
    /// Loads songs from DanceStats cache which is populated during testing.
    /// </summary>
    public override async IAsyncEnumerable<Song> LoadLightSongsStreamingAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Get all songs from DanceStats cache (populated by SaveSong/SaveSongs)
        var allSongs = DanceMusicService.DanceStats.GetAllSongs();

        foreach (var song in allSongs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Song properties might be null if song is a light song already
            var title = song.Title;
            var artist = song.Artist;

            // Skip songs without title
            if (string.IsNullOrEmpty(title))
            {
                continue;
            }

            // Create light song (same pattern as Song.CreateLightSong but using public properties)
            var lightSong = new Song
            {
                SongId = song.SongId,
                Title = title,
                Artist = artist ?? string.Empty,
                Tempo = song.Tempo,
                Length = song.Length
            };

            // Add minimal SongProperties for proper Title/Artist hashing
            lightSong.SongProperties.Add(new SongProperty(Song.TitleField, title));
            lightSong.SongProperties.Add(new SongProperty(Song.ArtistField, artist ?? string.Empty));

            if (song.Length.HasValue)
            {
                lightSong.SongProperties.Add(new SongProperty(Song.LengthField, song.Length.ToString()));
            }

            if (song.Tempo.HasValue)
            {
                lightSong.SongProperties.Add(new SongProperty(Song.TempoField, song.Tempo.ToString()));
            }

            yield return lightSong;
        }

        await Task.CompletedTask;
    }

    // Record for capturing EditSong calls
    public record EditSongCall(
        ApplicationUser User,
        Song Original,
        Song Edit,
        List<UserTag> Tags);
}




