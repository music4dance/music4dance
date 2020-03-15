using System;
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
                "https://itunes.apple.com/search?term={0}&media=music&entity=song&limit=200",
                "http://itunes.apple.com/lookup?id={0}&entity=song")
        {
        }

        protected override string BuildPurchaseLink(PurchaseType pt, string album, string song)
        {
            // TODO: itunes would need a different kind of link for album only lookup...
            if (pt == PurchaseType.Song && album != null && song != null)
            {
                return string.Format(AssociateLink, song, album);
            }
            return null;
        }

        public override IList<ServiceTrack> ParseSearchResults(dynamic results, Func<string, dynamic> getResult)
        {
            var ret = new List<ServiceTrack>();

            var tracks = results.results;

            foreach (var track in tracks)
            {
                var st = InternalParseTrackResults(track);
                if (st != null) ret.Add(st);
            }

            return ret;
        }

        public override ServiceTrack ParseTrackResults(dynamic results, Func<string, dynamic> getResult)
        {
            var tracks = results.results;
            return tracks.Count > 0 ? InternalParseTrackResults(tracks[0]) : null;
        }

        private ServiceTrack InternalParseTrackResults(dynamic track)
        {
            if (!string.Equals("song", (string)track.kind)) return null;

            int? duration = null;
            if (track.trackTimeMillis != null)
            {
                duration = ((int) track.trackTimeMillis + 500) / 1000;
            }

            return new ServiceTrack
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
                Genres = new string[] { track.primaryGenreName },
                TrackNumber = track.trackNumber,
                SampleUrl = track.PreviewUrl,
            };
        }
    }
}