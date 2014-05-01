using DanceLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class ITunesService : MusicService
    {
        public ITunesService(ServiceType id, char cid, string name, string target, string description, string link, string request) :
            base(id, cid, name, target, description, link, request)
        {
        }
        protected override string BuildPurchaseLink(PurchaseType pt, string album, string song)
        {
            // TODO: itunes would need a different kind of link for album only lookup...
            if (pt == PurchaseType.Song && album != null && song != null)
            {
                return string.Format(_associateLink, song, album);
            }
            else
            {
                return null;
            }
        }

        public override IList<ServiceTrack> ParseSearchResults(dynamic results)
        {
            List<ServiceTrack> ret = new List<ServiceTrack>();

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

                    ServiceTrack st = new ServiceTrack
                    {
                        TrackId = track.trackId.ToString(),
                        CollectionId = track.collectionId.ToString(),
                        Name = track.trackName,
                        Artist = track.artistName,
                        Album = track.collectionName,
                        ImageUrl = track.artworkUrl30,
                        Link = track.trackViewUrl,
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
