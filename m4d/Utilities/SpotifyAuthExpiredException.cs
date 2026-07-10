namespace m4d.Utilities;

/// <summary>
/// Thrown when a user's Spotify refresh token is rejected by Spotify (revoked or expired).
/// Distinguishes this from generic HTTP failures so callers can prompt the user to
/// reconnect their Spotify account instead of showing a generic error.
/// </summary>
public class SpotifyAuthExpiredException : Exception
{
    public SpotifyAuthExpiredException()
    {
    }

    public SpotifyAuthExpiredException(string message) : base(message)
    {
    }

    public SpotifyAuthExpiredException(string message, Exception inner) : base(message, inner)
    {
    }
}
