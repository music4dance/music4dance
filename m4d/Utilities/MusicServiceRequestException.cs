using System.Net;

namespace m4d.Utilities;

/// <summary>
/// A music service answered a request with an error status. It derives from WebException so the
/// existing catch blocks still handle it; callers that need the status read <see cref="StatusCode"/>.
/// </summary>
public class MusicServiceRequestException : WebException
{
    public MusicServiceRequestException(HttpStatusCode statusCode, string message, Exception inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

/// <summary>
/// The playlist itself is gone: the service answered 404 for the playlist's own lookup, not for a
/// later page of tracks or a catalog lookup made while importing it. Only this one feeds
/// MissingPlaylistPolicy.
/// </summary>
public class PlaylistNotFoundException(string url, MusicServiceRequestException inner)
    : MusicServiceRequestException(HttpStatusCode.NotFound, $"Playlist not found: {url}", inner);
