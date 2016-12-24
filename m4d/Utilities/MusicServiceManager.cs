using m4dModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Web.Helpers;

namespace m4d.Utilities
{
    public class MusicServiceManager
    {
        // Obviously not the clean abstraction, but Amazon is different enough that my abstraction
        //  between itunes and groove doesn't work.   So I'm going to shoe-horn this in to get it working
        //  and refactor later.

        public IList<ServiceTrack> FindMusicServiceSong(Song song, MusicService service, bool clean = false, string title = null, string artist = null, string album = null, string region = null)
        {
            IList<ServiceTrack> list;

            if (service != null)
            {
                list = DoFindMusicServiceSong(song, service, clean, title, artist, region);
            }
            else
            {
                var acc = new List<ServiceTrack>();
                foreach (var s in MusicService.GetSearchableServices())
                {
                    var tracks = DoFindMusicServiceSong(song, s, clean, title, artist, region);

                    if (tracks != null) acc.AddRange(tracks);
                }

                list = acc;
            }

            if (list == null) return null;

            list = FilterKaraoke(list);

            list = song != null ? song.RankTracks(list) : Song.RankTracksByCluster(list, album);

            return list;
        }

        public ServiceTrack GetMusicServiceTrack(string id, MusicService service, string region = null)
        {
            var sid = $"\"{service.CID}:{id}\"";
            ServiceTrack ret;
            if (s_trackCache.TryGetValue(sid, out ret))
            {
                return ret;
            }

            if (service.Id == ServiceType.Amazon)
            {
                ret = AmazonFetcher.LookupTrack(id);
            }
            else
            {
                var request = service.BuildTrackRequest(id, region);
                dynamic results = GetMusicServiceResults(request, service);
                ret = service.ParseTrackResults(results);
            }

            if (s_trackCache.Count > 1000)
            {
                s_trackCache.Clear();
            }
            s_trackCache[sid] = ret;
            return ret;
        }

        private static readonly Dictionary<string, ServiceTrack> s_trackCache = new Dictionary<string, ServiceTrack>();

        public IList<ServiceTrack> LookupServiceTracks(MusicService service, string url, IPrincipal principal)
        {
            dynamic results = GetMusicServiceResults(service.BuildLookupRequest(url), service, principal);
            IList<ServiceTrack> tracks = service.ParseSearchResults(results);
            while ((results = NextMusicServiceResults(results, service, principal)) != null)
            {
                var t = (tracks as List<ServiceTrack>) ?? tracks.ToList();
                t.AddRange(service.ParseSearchResults(results));
                tracks = t;
            }

            if (tracks == null) return null;

            ComputeTrackPurchaseInfo(service, tracks);

            return tracks;
        }

        public ServiceTrack CoerceTrackRegion(string id, MusicService service, string region)
        {
            if (string.IsNullOrWhiteSpace(region)) return null;

            var track = GetMusicServiceTrack(id, service, region);

            if (track == null) return null;

            return track.IsPlayable == false ? null : GetMusicServiceTrack(track.TrackId, service);
        }

        public virtual EchoTrack LookupEchoTrack(string id, MusicService service)
        {
            string request = $"https://api.spotify.com/v1/audio-features/{id}"; //$"http://developer.echonest.com/api/v4/track/profile?api_key=B0SEV0FNKNEOHGFB0&format=json&id=spotify:track:{id}&bucket=audio_summary";
            dynamic results = GetMusicServiceResults(request,service);
            return EchoTrack.BuildEchoTrack(results);
        }

        private static IList<ServiceTrack> FilterKaraoke(IList<ServiceTrack> list)
        {
            return list.Where(track => !ContainsKaraoke(track.Name) && !ContainsKaraoke(track.Album)).ToList();
        }

        private static bool ContainsKaraoke(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var exclude = new[] { "karaoke", "in the style of", "a tribute to" };
            return exclude.Any(s => name.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) != -1);
        }

        private IList<ServiceTrack> DoFindMusicServiceSong(Song song, MusicService service, bool clean = false, string title = null, string artist = null, string region = null)
        {
            IList<ServiceTrack> tracks;
            switch (service.Id)
            {
                case ServiceType.Amazon:
                    tracks = FindMSSongAmazon(song, clean, title, artist);
                    break;
                default:
                    tracks = FindMSSongGeneral(service, title, artist);
                    break;
            }

            if (tracks == null) return null;

            // Convoluted way of saying that we should coerce regions for spotify

            if (service.HasRegions && !string.IsNullOrWhiteSpace(region))
            {
                var dict = new Dictionary<string, ServiceTrack>();
                foreach (var track in tracks)
                {
                    if (dict.ContainsKey(track.TrackId)) continue;

                    ServiceTrack t = null;
                    if (!track.AvailableMarkets.Contains(region))
                    {
                        t = CoerceTrackRegion(track.TrackId, service, region);
                        if (t != null)
                        {
                            t.AvailableMarkets = PurchaseRegion.MergeRegions(t.AvailableMarkets, track.AvailableMarkets);
                        }
                    }

                    if (t == null) t = track;

                    dict[t.TrackId] = t;
                }

                tracks = dict.Values.ToList();
            }

            ComputeTrackPurchaseInfo(service, tracks);

            return tracks;
        }

        private void ComputeTrackPurchaseInfo(MusicService service, IEnumerable<ServiceTrack> tracks)
        {
            foreach (var track in tracks)
            {
                track.AlbumLink = service.GetPurchaseLink(PurchaseType.Album, track.CollectionId, track.TrackId);
                track.SongLink = service.GetPurchaseLink(PurchaseType.Song, track.CollectionId, track.TrackId);
                track.PurchaseInfo = AlbumDetails.BuildPurchaseInfo(service.Id, track.CollectionId, track.TrackId, track.AvailableMarkets);
            }
        }
        // ReSharper disable once InconsistentNaming
        private IList<ServiceTrack> FindMSSongAmazon(Song song, bool clean = false, string title = null, string artist = null)
        {
            var custom = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(artist);

            return custom ? AmazonFetcher.FetchTracks(title, artist) : AmazonFetcher.FetchTracks(song, clean);
        }

        // ReSharper disable once InconsistentNaming
        private static IList<ServiceTrack> FindMSSongGeneral(MusicService service, string title = null, string artist = null)
        {
            dynamic results = GetMusicServiceResults(service.BuildSearchRequest(artist, title), service);
            return service.ParseSearchResults(results);
        }

        private static int GetRateInfo(WebHeaderCollection headers, string type)
        {
            var s = headers.Get(type);
            if (s == null)
                return -1;

            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, $"{type}: {s}");
            int info;
            return int.TryParse(s, out info) ? info : -1;
        }

        private static dynamic GetMusicServiceResults(string request, MusicService service = null, IPrincipal principal = null)
        {
            while (true)
            {
                string responseString = null;

                if (request == null)
                {
                    return null;
                }

                var req = (HttpWebRequest)WebRequest.Create(request);
                req.Method = WebRequestMethods.Http.Get;
                req.Accept = "application/json";

                string auth = null;
                if (service != null)
                {
                    auth = AdmAuthentication.GetServiceAuthorization(service.Id, principal);
                }

                if (auth != null)
                {
                    req.Headers.Add("Authorization", auth);
                }

                try
                {
                    using (var response = (HttpWebResponse)req.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            var stream = response.GetResponseStream();
                            if (stream != null)
                            {
                                using (var sr = new StreamReader(stream))
                                {
                                    responseString = sr.ReadToEnd();
                                }

                                var remaining = GetRateInfo(response.Headers, "X-RateLimit-Remaining");

                                if (remaining > 0 && remaining < 20)
                                {
                                    Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                                        $"Excedeed EchoNest Limits: Pre-emptive {remaining} - used = {GetRateInfo(response.Headers, "X-RateLimit-Used")} - limit = {GetRateInfo(response.Headers, "X-RateLimit-Limit")}");
                                    System.Threading.Thread.Sleep(3 * 1000);
                                }
                            }
                        }
                        else if ((int)response.StatusCode == 429 /*HttpStatusCode.TooManyRequests*/)
                        {
                            // Wait algorithm failed, paus for 15 seconds
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Excedeed EchoNest Limits: Caught");
                            System.Threading.Thread.Sleep(15 * 1000);
                            continue;
                        }
                        if (responseString == null)
                        {
                            throw new WebException(response.StatusDescription);
                        }
                    }
                }
                catch (WebException we)
                {
                    var r = we.Response as HttpWebResponse;
                    if (r == null || (int)r.StatusCode != 429) throw;

                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Excedeed EchoNest Limits: Caught");
                    System.Threading.Thread.Sleep(15 * 1000);
                    continue;
                }

                if (service != null)
                {
                    responseString = service.PreprocessResponse(responseString);
                }

                return Json.Decode(responseString);
            }
        }

        private static dynamic NextMusicServiceResults(dynamic last, MusicService service, IPrincipal principal = null)
        {
            var request = service.GetNextRequest(last);
            return request == null ? null : GetMusicServiceResults(request, service, principal);
        }

        private AWSFetcher AmazonFetcher => _awsFetcher ?? (_awsFetcher = new AWSFetcher());
        private AWSFetcher _awsFetcher;

    }
}
