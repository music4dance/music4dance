namespace m4dModels;

// ReSharper disable once InconsistentNaming
public class ITunesService : MusicService
{
    public ITunesService() :
        base(
            ServiceType.ITunes,
            'I',
            "ITunes",
            "itunes_store",
            "Buy it on ITunes",
            "https://itunes.apple.com/album/id{1}?i={0}&uo=4&at=11lwtf",
            "https://itunes.apple.com/search?term={0}&media=music&entity=song&limit=200",
            "https://itunes.apple.com/lookup?id={0}&entity=song")
    {
    }

    // The Apple Music API (MusicKit) catalog lookup. The stored iTunes track ids are Apple Music
    // catalog song ids. songs?ids= returns 200 with the ids it doesn't have left out of data, where
    // songs/{id} would return 404, so the one-id lookup uses it too. The default response already
    // holds the album id (relationships.albums), so include=albums isn't needed.
    private const string AppleMusicSongsRequest = "https://api.music.apple.com/v1/catalog/{0}/songs?ids={1}";

    public static string BuildAppleMusicSongsRequest(string storefront, IEnumerable<string> ids) =>
        string.Format(AppleMusicSongsRequest, Uri.EscapeDataString(storefront),
            string.Join(",", ids.Select(Uri.EscapeDataString)));

    protected override string BuildPurchaseLink(PurchaseType pt, string album, string song)
    {
        // TODO: itunes would need a different kind of link for album only lookup...
        return pt == PurchaseType.Song && album != null && song != null ? string.Format(AssociateLink, song, album) : null;
    }

    public override async Task<IList<ServiceTrack>> ParseSearchResults(
        dynamic results, Func<string, Task<dynamic>> getResult,
        IEnumerable<string> excludeTracks, bool includeGenres = true)
    {
        var ret = new List<ServiceTrack>();

        if (results != null)
        {
            var tracks = results.results;

            foreach (var track in tracks)
            {
                var st = await InternalParseTrackResults(track);
                if (st != null)
                {
                    ret.Add(st);
                }
            }
        }

        return ret;
    }

    public override Task<ServiceTrack> ParseTrackResults(dynamic results,
        Func<string, Task<dynamic>> getResult, bool includeGenres = true)
    {
        if (results == null)
        {
            return Task.FromResult<ServiceTrack>(null);
        }

        // An Apple Music API response (data[]) or a legacy lookup response (results[])
        if (results.data != null)
        {
            var songs = results.data;
            ServiceTrack song = songs.Count > 0 ? ParseAppleMusicSong(songs[0]) : null;
            return Task.FromResult(song);
        }

        var tracks = results.results;
        return tracks == null || tracks.Count == 0
            ? Task.FromResult<ServiceTrack>(null)
            : InternalParseTrackResults(tracks[0]);
    }

    // One song resource from the Apple Music API, mapped to the fields the legacy lookup fills
    public static ServiceTrack ParseAppleMusicSong(dynamic song)
    {
        var attributes = song.attributes;
        int? duration = null;
        if (attributes?.durationInMillis != null)
        {
            duration = ((int)attributes.durationInMillis + 500) / 1000;
        }

        // genreNames ends with the catch-all "Music"; the first is the legacy primaryGenreName
        var genre = ((IEnumerable<dynamic>)attributes?.genreNames ?? [])
            .Select(g => (string)g)
            .FirstOrDefault(g => !string.IsNullOrWhiteSpace(g) && g != "Music");

        // The artwork url is a template ({w}x{h}bb.jpg, sometimes {w}x{h}{c}.{f}); 30x30 is the
        // size the legacy lookup's artworkUrl30 has
        var artwork = (string)attributes?.artwork?.url;

        var albums = song.relationships?.albums?.data;
        var isrc = (string)attributes?.isrc;

        return new ServiceTrack
        {
            Service = ServiceType.ITunes,
            TrackId = (string)song.id,
            CollectionId = albums != null && albums.Count > 0 ? (string)albums[0].id : null,
            Name = (string)attributes?.name,
            Artist = (string)attributes?.artistName,
            Album = (string)attributes?.albumName,
            ImageUrl = artwork?.Replace("{w}", "30").Replace("{h}", "30").Replace("{c}", "bb").Replace("{f}", "jpg"),
            ReleaseDate = (string)attributes?.releaseDate,
            Duration = duration,
            Genres = genre == null ? null : [genre],
            TrackNumber = (int?)attributes?.trackNumber,
            SampleUrl = attributes?.previews != null && attributes.previews.Count > 0
                ? (string)attributes.previews[0].url
                : null,
            ISRC = string.IsNullOrWhiteSpace(isrc) ? null : GetService(ServiceType.ISRC).NormalizeId(isrc),
        };
    }

    private Task<ServiceTrack> InternalParseTrackResults(dynamic track)
    {
        if (!string.Equals("song", (string)track.kind))
        {
            return Task.FromResult<ServiceTrack>(null);
        }

        int? duration = null;
        if (track.trackTimeMillis != null)
        {
            duration = ((int)track.trackTimeMillis + 500) / 1000;
        }

        return Task.FromResult(new ServiceTrack
        {
            Service = ServiceType.ITunes,
            TrackId = track.trackId.ToString(),
            CollectionId = track.collectionId.ToString(),
            Name = track.trackName,
            Artist = track.artistName,
            Album = track.collectionName,
            ImageUrl = track.artworkUrl30,
            //                        Link = track.trackViewUrl,
            ReleaseDate = track.releaseDate,
            Duration = duration,
            Genres = [track.primaryGenreName],
            TrackNumber = track.trackNumber,
            // JObject member names are case-sensitive: PreviewUrl was always null
            SampleUrl = track.previewUrl
        });
    }
}
