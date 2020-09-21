using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.CSharp.RuntimeBinder;
using Exception = System.Exception;

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


        public override IList<ServiceTrack> ParseSearchResults(dynamic results, Func<string, dynamic> getResult)
        {
            if (results == null) return null;

            var ret = new List<ServiceTrack>();

            var items = FoldTracks(results).items;

            foreach (var track in items)
            {
                var trackT = track;
                try
                {
                    if (track.track != null)
                        trackT = track.track;
                }
                catch (Exception)
                {
                    
                }
                ret.Add(ParseTrackResults(trackT, getResult));
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

        public override ServiceTrack ParseTrackResults(dynamic track, Func<string, dynamic> getResult)
        {
            string imageUrl = null;
            //if (track.images != null)
            //{
            //    var width = int.MaxValue;
            //    foreach (var image in track.images)
            //    {
            //        if (image.width >= width) continue;

            //        imageUrl = image.url;
            //        width = image.width;
            //    }
            //}


            var artist = (track.artists.Count > 0) ? track.artists[0] : null;
            var album = track.album;

            //if (TraceLevels.General.TraceVerbose)
            //{
            //    Trace.WriteLine($"TrackId={(string) track.id}");
            //    Trace.WriteLine($"Name={(string) track.name}");
            //    Trace.WriteLine($"Artist={(string) (artist == null ? null : artist.name)}");
            //    Trace.WriteLine($"Album={(string) (album == null ? null : album.name)}");
            //    Trace.WriteLine($"CollectionId={(string) (album == null ? null : album.id)}");
            //    Trace.WriteLine($"ImageUrl={imageUrl}");
            //    Trace.WriteLine($"ImageUrl={imageUrl}");
            //    Trace.WriteLine($"Duration={(string) ((track.duration_ms + 500)/1000).ToString()}");
            //    Trace.WriteLine($"DiscNumber={(string) track.disc_number.ToString()}");
            //    Trace.WriteLine($"TrackNumber={(string) track.track_number.ToString()}");
            //}

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
                    foreach (dynamic o in track.available_markets)
                    {
                        marketList.Add(o.ToString());
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
                Genres = BuildGenres(track, getResult),
                Duration = (track.duration_ms + 500) / 1000,
                TrackNumber = trackNum,
                IsPlayable = isPlayable,
                AvailableMarkets = availableMarkets,
                SampleUrl = sample
            };

            return st;
        }

        public override string BuildPlayListLink(PlayList playList, string user, string email)
        {
            var alias = email.Split('@')[0];
            return $"https://open.spotify.com/user/{alias}/playlist/{playList.Id}";
        }

        private string[] BuildGenres(dynamic track, Func<string, dynamic> getResult)
        {
            var genres = new HashSet<string>();

            genres.UnionWith(GenresFromReference(track.album, getResult));

            if (track?.artists.Count > 0)
            {
                foreach (var a in track.artists)
                {
                    genres.UnionWith(GenresFromReference(a, getResult));
                }
            }

            return genres.Count > 0 ? genres.ToArray() : null;
        }

        private List<string> GenresFromReference(dynamic field, Func<string, dynamic> getResult)
        {
            if (field?.href == null)
            {
                return new List<string>();
            }

            var t = GetResults(field.href.ToString(), getResult);
            return t != null ? (List<string>) GenresFromObject(t) : new List<string>();
        }

        private List<string> GenresFromObject(dynamic obj)
        {
            var list = new List<string>();
            try
            {
                if (obj?.genres == null || obj.genres.Count == 0)
                {
                    return list;
                }

                foreach (var genre in obj.genres)
                {
                    list.Add(CleanupGenre(genre.ToString()));
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            return list;
        }

        private dynamic GetResults(string url, Func<string, dynamic> getResult)
        {
            if (s_results.TryGetValue(url, out var result))
            {
                return result;
            }

            if (getResult == null)
            {
                return null;
            }

            try
            {
                return getResult(url);
            }
            catch (Exception e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, $"Error attempting to call Spotify: {e.Message}");
                return null;
            }
        }

        private static string CleanupGenre(string genre)
        {
            return s_textInfo.ToTitleCase(genre.Replace('-', ' '))
                .Replace(" And ", " and ")
                .Replace(" Or ", " or ");
        }

        private static readonly Dictionary<string, dynamic> s_results = new Dictionary<string, dynamic>();
        private static readonly TextInfo s_textInfo = CultureInfo.CurrentCulture.TextInfo;
    }
}