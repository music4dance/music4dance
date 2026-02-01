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

    // Record for capturing EditSong calls
    public record EditSongCall(
        ApplicationUser User,
        Song Original,
        Song Edit,
        List<UserTag> Tags);
}




