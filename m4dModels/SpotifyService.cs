using System;
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

        public override bool HasRegions => true;

        public override string BuildLookupRequest(string url)
        {
            if (url.Contains("/album/"))
            {
                var id = url.Substring(url.LastIndexOf('/')+1);

                return $"https://api.spotify.com/v1/albums/{id}";
            }

            if (url.Contains("/playlist/"))
            {
                var rg = url.Split(new[] {'/'},StringSplitOptions.RemoveEmptyEntries);

                if (rg.Length != 6)
                    return null;

                var user = rg[3];
                var id = rg[5];

                return $"https://api.spotify.com/v1/users/{user}/playlists/{id}";
            }

            return null;
        }

        public override string GetNextRequest(dynamic last)
        {
            return FoldTracks(last)?.next;
        }

        private static dynamic FoldTracks(dynamic results)
        {
            try
            {
                return results.tracks??results;
            }
            catch (RuntimeBinderException)
            {
                return results;
            }
        }


        public override IList<ServiceTrack> ParseSearchResults(dynamic results)
        {
            if (results == null) return null;

            var ret = new List<ServiceTrack>();

            var items = FoldTracks(results).items;

            foreach (var track in items)
            {
                dynamic trackT = track;
                try
                {
                    if (track.track != null)
                        trackT = track.track;
                }
                catch (Exception)
                {
                    
                }
                ret.Add(ParseTrackResults(trackT));
            }

            try
            {
                // Only albums have an album_type field...
                var a = results.album_type;

                if (a != null)
                {
                    var name = results.name;
                    foreach (var t in ret)
                    {
                        t.Album = name;
                    }
                }
            }
            catch (Exception)
            {
                // Figure out the appropriate binding exception to catch.
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
                Trace.WriteLine($"TrackId={(string) track.id}");
                Trace.WriteLine($"Name={(string) track.name}");
                Trace.WriteLine($"Artist={(string) (artist == null ? null : artist.name)}");
                Trace.WriteLine($"Album={(string) (album == null ? null : album.name)}");
                Trace.WriteLine($"CollectionId={(string) (album == null ? null : album.id)}");
                Trace.WriteLine($"ImageUrl={imageUrl}");
                //                Trace.WriteLine(string.Format("Genre={0}", (string)((artist == null || artist.genres.Length == 0) ? null : artist.genres[0])));
                Trace.WriteLine($"Duration={(string) ((track.duration_ms + 500)/1000).ToString()}");
                Trace.WriteLine($"DiscNumber={(string) track.disc_number.ToString()}");
                Trace.WriteLine($"TrackNumber={(string) track.track_number.ToString()}");
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
                        if (o is string s) marketList.Add(s);
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
            string sample = null;
            try
            {
                sample = track.preview_url;
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
                AvailableMarkets = availableMarkets,
                SampleUrl = sample
            };

            return st;
        }
        public override string BuildPlayListLink(PlayList playList, ApplicationUser user)
        {
            var alias = user.Email.Split('@')[0];
            return $"https://open.spotify.com/user/{alias}/playlist/{playList.Id}";
        }

    }
}