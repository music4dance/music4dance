using DanceLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class XboxService : MusicService
    {
        public XboxService(ServiceType id, char cid, string name, string target, string description, string link, string request) :
            base(id, cid, name, target, description, link, request)
        {

        }
        public override bool RequiresKey
        {
            get { return true; }
        }
        protected override string BuildPurchaseLink(PurchaseType pt, string album, string song)
        {
            album = Strip(album);
            song = Strip(song);

            return base.BuildPurchaseLink(pt, album, song);
        }
        public override string PreprocessSearchResponse(string response)
        {
            return response.Replace(@"""music.amg""", @"""music_amg""");
        }
        public override IList<ServiceTrack> ParseSearchResults(dynamic results)
        {
            List<ServiceTrack> ret = new List<ServiceTrack>();

            var tracks = results.Tracks;
            var items = tracks.Items;


            foreach (var track in items)
            {
                string altId = null;
                if (track.OtherIds != null)
                {
                    try
                    {
                        altId = tracks.OtherIds.music_amg;
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                    {
                    }
                }

                int? duration = null;
                if (!string.IsNullOrWhiteSpace(track.Duration))
                {
                    try
                    {
                        decimal dd = new SongDuration(track.Duration).Length;
                        duration = (int)Math.Round(dd);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                    }
                }

                ServiceTrack st = new ServiceTrack
                {
                    Service = ServiceType.XBox,
                    TrackId = track.Id,
                    Name = track.Name,
                    AltId = altId,
                    Artist = track.Artists[0].Artist.Name,
                    Album = track.Album.Name,
                    ImageUrl = track.ImageUrl,
//                    Link = track.Link + "?action=play&target=app",
                    ReleaseDate = track.ReleaseDate,
                    Genre = track.Genres[0],
                    Duration = duration,
                    TrackNumber = track.TrackNumber,
                };

                ret.Add(st);
            }

            return ret;
        }

        static string Strip(string info)
        {
            if (info != null && info.StartsWith("music."))
            {
                info = info.Substring(6);
            }

            return info;
        }
    }
}
