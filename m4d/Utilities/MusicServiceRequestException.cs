using System.Net;

namespace m4d.Utilities;

/// <summary>
/// A music service answered a request with an error status. It derives from WebException so the
/// existing catch blocks still handle it; callers that need to tell a missing resource (404) from
/// a transient failure (5xx, 429) read <see cref="StatusCode"/>.
/// </summary>
public class MusicServiceRequestException : WebException
{
    public MusicServiceRequestException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }

    public static bool IsNotFound(Exception e) =>
        e is MusicServiceRequestException { StatusCode: HttpStatusCode.NotFound };
}
