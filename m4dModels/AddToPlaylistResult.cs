namespace m4dModels;

/// <summary>
/// Result model for adding a song to a Spotify playlist.
/// </summary>
public class AddToPlaylistResult
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Message describing the result (error message on failure, success message on success)
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// The Spotify playlist snapshot ID (if successful)
    /// </summary>
    public string SnapshotId { get; set; }

    public static AddToPlaylistResult CreateSuccess(string snapshotId, string message = "Song added to playlist successfully")
    {
        return new AddToPlaylistResult
        {
            Success = true,
            Message = message,
            SnapshotId = snapshotId
        };
    }

    public static AddToPlaylistResult CreateFailure(string message)
    {
        return new AddToPlaylistResult
        {
            Success = false,
            Message = message,
            SnapshotId = null
        };
    }
}
