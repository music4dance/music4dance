using System.Net;
using System.Text;

namespace m4d.Tests.Utilities;

/// <summary>
/// A stand-in for the Spotify Web API and the iTunes API for MusicServiceManager tests, with no
/// network. Install it on a manager through its Http property:
/// <c>new MusicServiceManager(config) { Http = new HttpClient(new SpotifyFixtureHandler()) }</c>.
/// <list type="bullet">
/// <item>The track ("Cut To The Feeling", Carly Rae Jepsen) is a Spotify track response as the API
/// console returned it. The search response wraps that same track. The album and artist
/// responses are the album and artist as the track names them, with empty genres: stand-ins in
/// the shape the add path reads.</item>
/// <item>Audio features are the console's sample payload (tempo 118.211). The add path asks for
/// the features of the track's own id; this serves the sample for any id.</item>
/// <item>Anything else is 404, and every iTunes request is 404 (iTunes is out of scope until the
/// Apple Music migration).</item>
/// </list>
/// </summary>
internal sealed class SpotifyFixtureHandler : HttpMessageHandler
{
    public const string TrackId = "11dFghVXANMlKmJXsNCbNl";
    public const string AlbumId = "0tGPJ0bkWOUmH7MEOR77qc";
    public const string ArtistId = "6sFIWsNpZYqfjUpaCgueju";

    /// <summary>The track's length in seconds, as ParseTrackResults rounds duration_ms.</summary>
    public const int TrackSeconds = 208;

    /// <summary>The sample's tempo, which the add path reads from the audio features.</summary>
    public const decimal SampleTempo = 118.211m;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("No request URI");
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var resource = segments.ElementAtOrDefault(1);
        var id = segments.ElementAtOrDefault(2);

        var body = (uri.Host, resource, id) switch
        {
            ("api.spotify.com", "search", null) => SearchJson,
            ("api.spotify.com", "tracks", TrackId) => TrackJson,
            ("api.spotify.com", "audio-features", not null) => AudioFeaturesJson,
            ("api.spotify.com", "albums", AlbumId) => AlbumJson,
            ("api.spotify.com", "artists", ArtistId) => ArtistJson,
            _ => null,
        };

        var response = body == null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        return Task.FromResult(response);
    }

    // A simplified artist reference, as the album and track name their artists
    private static readonly string ArtistRef = """
        {
          "external_urls": { "spotify": "https://open.spotify.com/artist/6sFIWsNpZYqfjUpaCgueju" },
          "href": "https://api.spotify.com/v1/artists/6sFIWsNpZYqfjUpaCgueju",
          "id": "6sFIWsNpZYqfjUpaCgueju",
          "name": "Carly Rae Jepsen",
          "type": "artist",
          "uri": "spotify:artist:6sFIWsNpZYqfjUpaCgueju"
        }
        """;

    private static readonly string AlbumRef = $$"""
        {
          "album_type": "single",
          "total_tracks": 1,
          "external_urls": { "spotify": "https://open.spotify.com/album/0tGPJ0bkWOUmH7MEOR77qc" },
          "href": "https://api.spotify.com/v1/albums/0tGPJ0bkWOUmH7MEOR77qc",
          "id": "0tGPJ0bkWOUmH7MEOR77qc",
          "images": [
            { "url": "https://i.scdn.co/image/ab67616d0000b2737359994525d219f64872d3b1", "height": 640, "width": 640 },
            { "url": "https://i.scdn.co/image/ab67616d00001e027359994525d219f64872d3b1", "height": 300, "width": 300 },
            { "url": "https://i.scdn.co/image/ab67616d000048517359994525d219f64872d3b1", "height": 64, "width": 64 }
          ],
          "name": "Cut To The Feeling",
          "release_date": "2017-05-26",
          "release_date_precision": "day",
          "type": "album",
          "uri": "spotify:album:0tGPJ0bkWOUmH7MEOR77qc",
          "artists": [ {{ArtistRef}} ],
          "is_playable": true
        }
        """;

    private static readonly string ArtistJson = $$"""
        {
          "external_urls": { "spotify": "https://open.spotify.com/artist/6sFIWsNpZYqfjUpaCgueju" },
          "href": "https://api.spotify.com/v1/artists/6sFIWsNpZYqfjUpaCgueju",
          "id": "6sFIWsNpZYqfjUpaCgueju",
          "name": "Carly Rae Jepsen",
          "type": "artist",
          "uri": "spotify:artist:6sFIWsNpZYqfjUpaCgueju",
          "genres": []
        }
        """;

    // The album as the album endpoint returns it: the album plus its genres (none listed)
    private static readonly string AlbumJson = $$"""
        {
          "album_type": "single",
          "total_tracks": 1,
          "external_urls": { "spotify": "https://open.spotify.com/album/0tGPJ0bkWOUmH7MEOR77qc" },
          "href": "https://api.spotify.com/v1/albums/0tGPJ0bkWOUmH7MEOR77qc",
          "id": "0tGPJ0bkWOUmH7MEOR77qc",
          "name": "Cut To The Feeling",
          "release_date": "2017-05-26",
          "release_date_precision": "day",
          "type": "album",
          "uri": "spotify:album:0tGPJ0bkWOUmH7MEOR77qc",
          "artists": [ {{ArtistRef}} ],
          "genres": []
        }
        """;

    private static readonly string TrackJson = $$"""
        {
          "album": {{AlbumRef}},
          "artists": [ {{ArtistRef}} ],
          "disc_number": 1,
          "duration_ms": 207959,
          "explicit": false,
          "external_ids": { "isrc": "USUM71703861" },
          "external_urls": { "spotify": "https://open.spotify.com/track/11dFghVXANMlKmJXsNCbNl" },
          "href": "https://api.spotify.com/v1/tracks/11dFghVXANMlKmJXsNCbNl",
          "id": "11dFghVXANMlKmJXsNCbNl",
          "is_playable": true,
          "name": "Cut To The Feeling",
          "track_number": 1,
          "type": "track",
          "uri": "spotify:track:11dFghVXANMlKmJXsNCbNl"
        }
        """;

    private static readonly string SearchJson = $$"""
        {
          "tracks": {
            "href": "https://api.spotify.com/v1/search",
            "items": [ {{TrackJson}} ],
            "limit": 20,
            "next": null,
            "offset": 0,
            "previous": null,
            "total": 1
          }
        }
        """;

    private const string AudioFeaturesJson = """
        {
          "acousticness": 0.00242,
          "analysis_url": "https://api.spotify.com/v1/audio-analysis/2takcwOaAZWiXQijPHIx7B",
          "danceability": 0.585,
          "duration_ms": 237040,
          "energy": 0.842,
          "id": "2takcwOaAZWiXQijPHIx7B",
          "instrumentalness": 0.00686,
          "key": 9,
          "liveness": 0.0866,
          "loudness": -5.883,
          "mode": 0,
          "speechiness": 0.0556,
          "tempo": 118.211,
          "time_signature": 4,
          "track_href": "https://api.spotify.com/v1/tracks/2takcwOaAZWiXQijPHIx7B",
          "type": "audio_features",
          "uri": "spotify:track:2takcwOaAZWiXQijPHIx7B",
          "valence": 0.428
        }
        """;
}
