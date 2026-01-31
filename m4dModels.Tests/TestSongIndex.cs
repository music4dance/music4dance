namespace m4dModels.Tests;

/// <summary>
/// Test implementation of SongIndex that captures EditSong calls for verification in tests.
/// Overrides the now-virtual EditSong method.
/// </summary>
public class TestSongIndex : SongIndex
{
    public List<EditSongCall> EditCalls { get; } = new();

    public TestSongIndex(DanceMusicCoreService dms, string id = "test") : base(dms, id) { }

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


