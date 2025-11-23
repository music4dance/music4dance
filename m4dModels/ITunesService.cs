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

    protected override string BuildPurchaseLink(PurchaseType pt, string album, string song)
    {
        // TODO: itunes would need a different kind of link for album only lookup...
        return pt == PurchaseType.Song && album != null && song != null ? string.Format(AssociateLink, song, album) : null;
    }

    public override async Task<IList<ServiceTrack>> ParseSearchResults(
        dynamic results, Func<string, Task<dynamic>> getResult,
        IEnumerable<string> excludeTracks)
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
        Func<string, Task<dynamic>> getResult)
    {
        if (results == null)
        {
            return null;
        }
        var tracks = results.results;
        return tracks.Count > 0 ? InternalParseTrackResults(tracks[0]) : null;
    }

    private Task<ServiceTrack> InternalParseTrackResults(dynamic track)
    {
        if (!string.Equals("song", (string)track.kind))
        {
            return null;
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
            SampleUrl = track.PreviewUrl
        });
    }
}
