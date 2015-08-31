using System;
using System.Collections.Generic;
using DanceLibrary;
using Microsoft.CSharp.RuntimeBinder;

namespace m4dModels
{
    public class XboxService : MusicService
    {
        public XboxService() :
            base(ServiceType.XBox,
                'X',
                "Groove",
                "groove_store",
                "Play it on Groove Music",
                "http://music.xbox.com/Track/{0}?partnerID=Music4Dance&action=play&target=app",
                "https://music.xboxlive.com/1/content/music/search?q={0}&filters=tracks")
        {

        }
        public override bool RequiresKey => true;

        protected override string BuildPurchaseLink(PurchaseType pt, string album, string song)
        {
            album = Strip(album);
            song = Strip(song);

            return base.BuildPurchaseLink(pt, album, song);
        }
        public override string PreprocessResponse(string response)
        {
            return response.Replace(@"""music.amg""", @"""music_amg""");
        }
        public override IList<ServiceTrack> ParseSearchResults(dynamic results)
        {
            var ret = new List<ServiceTrack>();

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
                    catch (RuntimeBinderException)
                    {
                    }
                }

                int? duration = null;
                if (!string.IsNullOrWhiteSpace(track.Duration))
                {
                    try
                    {
                        var dd = new SongDuration(track.Duration).Length;
                        duration = (int)Math.Round(dd);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                    }
                }

                var st = new ServiceTrack
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
