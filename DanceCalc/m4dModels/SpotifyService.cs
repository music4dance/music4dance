using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CSharp.RuntimeBinder;

namespace m4dModels
{
    class SpotifyService : MusicService
    {
        public SpotifyService() :
            base(ServiceType.Spotify,
            'S', 
            "Spotify", 
            "spotify_client", 
            "listen on spotify",
            "http://open.spotify.com/track/{0}", 
            "https://api.spotify.com/v1/search?q={0}&type=track",
            "https://api.spotify.com/v1/tracks/{0}")
        {
        }

        public override bool HasRegions
        {
            get { return true; }
        }

        public override IList<ServiceTrack> ParseSearchResults(dynamic results)
        {
            var ret = new List<ServiceTrack>();

            var tracks = results.tracks;
            var items = tracks.items;

            foreach (var track in items)
            {
                ret.Add(ParseTrackResults(track));
            }

            return ret;
        }

        public override ServiceTrack ParseTrackResults(dynamic track)
        {
            string imageUrl = null;
            if (track.images != null)
            {
                var width = int.MaxValue;
                foreach (var image in track.images)
                {
                    if (image.width >= width) continue;

                    imageUrl = image.url;
                    width = image.width;
                }
            }


            var artist = (track.artists.Length > 0) ? track.artists[0] : null;
            var album = track.album;

            // TODO: Genre appears to be broken????
            if (TraceLevels.General.TraceVerbose)
            {
                Trace.WriteLine(string.Format("TrackId={0}", (string)track.id));
                Trace.WriteLine(string.Format("Name={0}", (string)track.name));
                Trace.WriteLine(string.Format("Artist={0}", (string)(artist == null ? null : artist.name)));
                Trace.WriteLine(string.Format("Album={0}", (string)(album == null ? null : album.name)));
                Trace.WriteLine(string.Format("CollectionId={0}", (string)(album == null ? null : album.id)));
                Trace.WriteLine(string.Format("ImageUrl={0}", imageUrl));
                //                Trace.WriteLine(string.Format("Genre={0}", (string)((artist == null || artist.genres.Length == 0) ? null : artist.genres[0])));
                Trace.WriteLine(string.Format("Duration={0}", (string)((track.duration_ms + 500) / 1000).ToString()));
                Trace.WriteLine(string.Format("DiscNumber={0}", (string)track.disc_number.ToString()));
                Trace.WriteLine(string.Format("TrackNumber={0}", (string)track.track_number.ToString()));
            }

            int trackNum = track.track_number;
            if (track.disc_number > 1)
            {
                trackNum = new TrackNumber(trackNum, (int)track.disc_number, null);
            }

            string[] availableMarkets = null;
            try
            {
                if (track.available_markets != null)
                {
                    var marketList = new List<string>();
                    foreach (object o in track.available_markets)
                    {
                        var s = o as string;
                        if (s != null) marketList.Add(s);
                    }
                    availableMarkets = marketList.ToArray();
                }
            }
            catch (RuntimeBinderException)
            {
            }
            bool? isPlayable = null;
            try
            {
                isPlayable = track.is_playable;
            }
            catch (RuntimeBinderException)
            {
            }

            var st = new ServiceTrack
            {
                Service = ServiceType.Spotify,
                TrackId = track.id,
                Name = track.name,
                //AltId = altId,
                Artist = artist == null ? null : artist.name,
                Album = album == null ? null : album.name,
                CollectionId = album == null ? null : album.id,
                ImageUrl = imageUrl,
                //ReleaseDate = track.ReleaseDate,
                //Genre = (artist == null || artist.genres.Length == 0) ? null : artist.genres[0],
                Duration = (track.duration_ms + 500) / 1000,
                TrackNumber = trackNum,
                IsPlayable = isPlayable,
                AvailableMarkets = availableMarkets
            };

            return st;
        }
    }
}