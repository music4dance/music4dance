using System.Collections.Generic;

namespace m4dModels
{
    // ReSharper disable once InconsistentNaming
    public class ITunesService : MusicService
    {
        public ITunesService() :
            base(ServiceType.ITunes,
                'I',
                "ITunes",
                "itunes_store",
                "Buy it on ITunes",
                "http://itunes.apple.com/album/id{1}?i={0}&uo=4&at=11lwtf",
                "https://itunes.apple.com/search?term={0}&media=music&entity=song&limit=200")
        {
        }
        protected override string BuildPurchaseLink(PurchaseType pt, string album, string song)
        {
            // TODO: itunes would need a different kind of link for album only lookup...
            if (pt == PurchaseType.Song && album != null && song != null)
            {
                return string.Format(AssociateLink, song, album);
            }
            else
            {
                return null;
            }
        }

        public override IList<ServiceTrack> ParseSearchResults(dynamic results)
        {
            var ret = new List<ServiceTrack>();

            var tracks = results.results;

            foreach (var track in tracks)
            {
                if (string.Equals("song", track.kind))
                {
                    int? duration = null;
                    if (track.TrackTimeMillis != null)
                    {
                        duration = (track.trackTimeMillis + 500) / 1000;
                    }

                    var st = new ServiceTrack
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
                        Genre = track.primaryGenreName,
                        TrackNumber = track.trackNumber,
                    };

                    ret.Add(st);
                }
            }

            return ret;
        }
    }
}
