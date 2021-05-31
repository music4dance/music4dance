using m4dModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading;
using DanceLibrary;
using m4d.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;

namespace m4d.Utilities
{
    public class MusicServiceManager
    {
        // Obviously not the clean abstraction
        public MusicServiceManager(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        #region Search
        public IList<ServiceTrack> FindMusicServiceSong(Song song,
            MusicService service, string title = null, string artist = null,
            string album = null)
        {
            IList<ServiceTrack> list;
            if (title == null)
            {
                artist ??= song.Artist;
            }
            title ??= song.Title;

            if (service != null)
            {
                list = DoFindMusicServiceSong(service, title, artist);
            }
            else
            {
                var acc = new List<ServiceTrack>();
                foreach (var s in MusicService.GetSearchableServices())
                {
                    var tracks = DoFindMusicServiceSong(s,
                        title, artist);

                    if (tracks != null) acc.AddRange(tracks);
                }

                list = acc;
            }

            if (list == null || list.Count == 0)
            {
                var cleanArtist = Song.CleanString(artist);
                var cleanTitle = Song.CleanString(title);
                return cleanArtist == artist && cleanTitle == title
                    ? null
                    : FindMusicServiceSong(song, service, title, artist, album);
            }

            list = FilterKaraoke(list);

            list = song != null ? song.RankTracks(list) : Song.RankTracksByCluster(list, album);

            return list;
        }

        public ServiceTrack GetMusicServiceTrack(string id, MusicService service)
        {
            int extra = id.IndexOf('[');
            if (extra != -1)
            {
                id = id.Substring(0, extra);
            }

            var sid = $"\"{service.CID}:{id}\"";
            if (s_trackCache.TryGetValue(sid, out var ret))
            {
                return ret;
            }

            if (service.Id != ServiceType.Amazon)
            {
                var request = service.BuildTrackRequest(id);
                var results = GetMusicServiceResults(request, service);
                ret = service.ParseTrackResults(
                    results,
                    (Func<string, dynamic>) (req => GetMusicServiceResults(req, service)));
            }

            if (s_trackCache.Count > 10000)
            {
                s_trackCache.Clear();
            }
            s_trackCache[sid] = ret;
            return ret;
        }

        public Song CreateSong(DanceMusicCoreService dms,
            ApplicationUser user, string id, MusicService service)
        {
            var track = GetMusicServiceTrack(id, service);

            if (track == null)
            {
                return null;
            }

            var song = Song.UserCreateFromTrack(dms, user, track);
            var found = false;
            var oldSong = dms.FindMatchingSong(song);

            if (oldSong != null)
            {
                found = true;
                song = oldSong;
            }

            UpdateSongAndServices(dms, song);
            UpdateFromTracks(dms, song, new List<ServiceTrack> { track });

            if (found)
            {
                dms.SaveSong(song);
            }
            return song;
        }

        private static readonly Dictionary<string, ServiceTrack> s_trackCache = new Dictionary<string, ServiceTrack>();

        public GenericPlaylist LookupPlaylist(MusicService service, string url,
            IEnumerable<string> oldTrackList, IPrincipal principal = null)
        {
            var results = GetMusicServiceResults(service.BuildLookupRequest(url), service, principal);
            var name = results.name;
            var description = results.description;

            IList<ServiceTrack> tracks = service.ParseSearchResults(results,
                (Func<string, dynamic>)(req => GetMusicServiceResults(req, service)),
                oldTrackList);
            while ((results = NextMusicServiceResults(results, service, principal)) != null)
            {
                var t = (tracks as List<ServiceTrack>) ?? tracks.ToList();
                t.AddRange(service.ParseSearchResults(results,
                    (Func<string, dynamic>)(req => GetMusicServiceResults(req, service)),
                    oldTrackList));
                tracks = t;
            }

            if (tracks == null || tracks.Count == 0) return null;

            ComputeTrackPurchaseInfo(service, tracks);

            return new GenericPlaylist
            {
                Name = name.ToString(),
                Description = description.ToString(),
                Tracks = tracks
            };
        }

        // TODO: Handle services other than spotify
        public List<PlaylistMetadata> GetPlaylists(MusicService service, IPrincipal principal)
        {
            if (service.Id != ServiceType.Spotify) throw new ArgumentOutOfRangeException(nameof(service), "GetPlaylists currently only supports Spotify");

            var results = GetMusicServiceResults("https://api.spotify.com/v1/me/playlists", service, principal);

            var playlists = ParsePlaylistResults(results);
            while ((results = NextMusicServiceResults(results, service, principal)) != null)
            {
                playlists.AddRange(ParsePlaylistResults(results));
            }

            return playlists;
        }

        private List<PlaylistMetadata> ParsePlaylistResults(dynamic results)
        {
            if (results == null) return null;

            var ret = new List<PlaylistMetadata>();

            foreach (var playlist in results.items)
            {
                ret.Add(new PlaylistMetadata
                {
                    Id = playlist.id,
                    Name = playlist.name
                });
            }

            return ret;
        }

        public virtual EchoTrack LookupEchoTrack(string id, MusicService service)
        {
            string request = $"https://api.spotify.com/v1/audio-features/{id}"; //$"http://developer.echonest.com/api/v4/track/profile?api_key=B0SEV0FNKNEOHGFB0&format=json&id=spotify:track:{id}&bucket=audio_summary";
            try
            {
                dynamic results = GetMusicServiceResults(request, service);
                return EchoTrack.BuildEchoTrack(results);
            }
            catch (WebException e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError,$"Error looking up echo track {id}: {e.Message}");
                return null;
            }
        }
        #endregion

        #region Update
        public bool GetEchoData(DanceMusicCoreService dms, Song song)
        {
            var service = MusicService.GetService(ServiceType.Spotify);
            var ids = song.GetPurchaseIds(service);
            var user = service.ApplicationUser;
            var edit = new Song(song, dms);

            EchoTrack track = null;
            foreach (var id in ids)
            {
                var idt = PurchaseRegion.ParseId(id);
                track = LookupEchoTrack(idt, service);
                if (track != null)
                    break;
            }

            if (track == null)
            {
                edit.Danceability = float.NaN;
                return dms.EditSong(user, song, edit);
            }

            if (track.BeatsPerMinute != null)
            {
                edit.Tempo = track.BeatsPerMinute;
            }
            if (track.Danceability != null)
            {
                edit.Danceability = track.Danceability;
            }
            if (track.Energy != null)
            {
                edit.Energy = track.Energy;
            }
            if (track.Valence != null)
            {
                edit.Valence = track.Valence;
            }
            var tags = edit.GetUserTags(user.UserName);
            var meter = track.Meter;
            if (meter != null)
            {
                tags = tags.Add($"{meter}:Tempo");
            }

            if (!dms.EditSong(user, song, edit, new[]
            {
                new UserTag { Id = string.Empty, Tags = tags }
            }))
                return false;

            if (track.BeatsPerMeasure != null)
            {
                edit.InferFromGroups();
            }
            return true;
        }

        public bool GetSampleData(DanceMusicCoreService dms, Song song)
        {
            var spotify = MusicService.GetService(ServiceType.Spotify);
            var edit = new Song(song, dms);

            ServiceTrack track = null;
            // First try Spotify
            var ids = edit.GetPurchaseIds(spotify);
            var user = dms.FindUser("batch-s");
            foreach (var id in ids)
            {
                var idt = PurchaseRegion.ParseId(id);
                track = GetMusicServiceTrack(idt, spotify);
                if (track?.SampleUrl != null)
                {
                    break;
                }
            }

            if (track == null)
            {
                var itunes = MusicService.GetService(ServiceType.ITunes);
                // If spotify failed, try itunes
                ids = edit.GetPurchaseIds(itunes);
                foreach (var id in ids)
                {
                    track = GetMusicServiceTrack(id, itunes);
                    if (track?.SampleUrl != null)
                    {
                        user = dms.FindUser("batch-i");
                        break;
                    }
                }
            }

            edit.Sample = track?.SampleUrl ?? @".";
            return dms.EditSong(user, song, edit);
        }
        #endregion

        #region Edit
        // TODO: Handle services other than spotify
        public PlaylistMetadata CreatePlaylist(MusicService service, IPrincipal principal, string name, string description, IFileProvider fileProvider)
        {

            dynamic obj = new {name = name, description = description};
            var inputs = JsonConvert.SerializeObject(obj);
            var response = MusicServiceAction("https://api.spotify.com/v1/me/playlists", inputs, WebRequestMethods.Http.Post, service, principal );

            if (response == null) return null;

            MusicServiceAction($"https://api.spotify.com/v1/playlists/{response.id}/images", GetEncodedImage(fileProvider, "/wwwroot/images/icons/color-logo.jpg"), WebRequestMethods.Http.Put, service, principal, "image/jpeg");

            return new PlaylistMetadata
            {
                Id = response.id,
                Name = response.name
            };
        }

        private string GetEncodedImage(IFileProvider fileProvider, string path)
        {
            var fullPath =  fileProvider.GetFileInfo(path).PhysicalPath;

            using Image image = Image.FromFile(fullPath);
            using var m = new MemoryStream();
            image.Save(m, image.RawFormat);
            var imageBytes = m.ToArray();
            var base64String = Convert.ToBase64String(imageBytes);
            return base64String;

            throw new NotImplementedException();
        }

        public bool SetPlaylistTracks(MusicService service, IPrincipal principal, string id,
            IEnumerable<string> tracks)
        {
            var tracklist = string.Join(",", tracks.Where(t => t != null).Select(t => $"\"spotify:track:{t}\""));
            var response = MusicServiceAction($"https://api.spotify.com/v1/playlists/{id}/tracks",
                $"{{\"uris\":[{tracklist}]}}", WebRequestMethods.Http.Put, service, principal);

            return response != null && response.snapshot_id != null;
        }


        #endregion

        #region Utilities
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

        private IList<ServiceTrack> DoFindMusicServiceSong(MusicService service,
            string title = null, string artist = null)
        {
            var tracks = FindMSSongGeneral(service,title, artist);

            if (tracks == null) return null;

            ComputeTrackPurchaseInfo(service, tracks);

            return tracks;
        }

        private void ComputeTrackPurchaseInfo(MusicService service, IEnumerable<ServiceTrack> tracks)
        {
            foreach (var track in tracks)
            {
                track.AlbumLink = service.GetPurchaseLink(PurchaseType.Album, track.CollectionId, track.TrackId);
                track.SongLink = service.GetPurchaseLink(PurchaseType.Song, track.CollectionId, track.TrackId);
                track.PurchaseInfo = AlbumDetails.BuildPurchaseInfo(service.Id, track.CollectionId, track.TrackId);
            }
        }

        // ReSharper disable once InconsistentNaming
        private IList<ServiceTrack> FindMSSongGeneral(MusicService service, string title = null, string artist = null)
        {
            dynamic results = GetMusicServiceResults(service.BuildSearchRequest(artist, title), service);
            return service.ParseSearchResults(results,
                (Func<string, dynamic>)(req => GetMusicServiceResults(req, service, null)), null);
        }

        private static int GetRateInfo(WebHeaderCollection headers, string type)
        {
            var s = headers.Get(type);
            if (s == null)
                return -1;

            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, $"{type}: {s}");
            return int.TryParse(s, out var info) ? info : -1;
        }

        private dynamic GetMusicServiceResults(string request,
            MusicService service,
            IPrincipal principal = null)
        {
            var retries = 2;
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
                    auth = AdmAuthentication.GetServiceAuthorization(Configuration, service.Id, principal);
                }

                if (auth != null)
                {
                    req.Headers.Add("Authorization", auth);
                }

                try
                {
                    using var response = (HttpWebResponse) req.GetResponse();
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var stream = response.GetResponseStream();
                        using (var sr = new StreamReader(stream))
                        {
                            responseString = sr.ReadToEnd();
                        }

                        var remaining = GetRateInfo(response.Headers, "X-RateLimit-Remaining");

                        if (remaining > 0 && remaining < 20)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                                $"Excedeed EchoNest Limits: Pre-emptive {remaining} - used = {GetRateInfo(response.Headers, "X-RateLimit-Used")} - limit = {GetRateInfo(response.Headers, "X-RateLimit-Limit")}");
                            Thread.Sleep(3 * 1000);
                        }
                    }
                    else if ((int) response.StatusCode == 429 /*HttpStatusCode.TooManyRequests*/)
                    {
                        // Wait algorithm failed, pause for 15 seconds
                        Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Excedeed EchoNest Limits: Caught");
                        Thread.Sleep(15 * 1000);
                        continue;
                    }
                    if (responseString == null)
                    {
                        throw new WebException(response.StatusDescription);
                    }
                }
                catch (WebException we)
                {
                    if (we.Response is HttpWebResponse r)
                    {
                        var statusCode = (int) r.StatusCode;
                        if (statusCode == 429)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Excedeed EchoNest Limits: Caught");
                            Thread.Sleep(15 * 1000);
                            continue;
                        }

                        if (statusCode == 403 && service?.Id == ServiceType.ITunes)
                        {
                            if (retries-- > 0)
                            {
                                Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Excedeed Itunes Limits: {2-retries} {req.Address}");
                                Thread.Sleep(15 * 1000);
                                continue;
                            }
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Excedeed Itunes Limits: Giving Up {req.Address}");
                        }
                    }

                    throw;
                }

                if (service != null)
                {
                    responseString = service.PreprocessResponse(responseString);
                }

                return JsonConvert.DeserializeObject(responseString);
            }
        }

        // TODO Handle services other than spotify.
        // This method requires a valid principal
        private dynamic MusicServiceAction(string request, string input, string method,
            MusicService service, IPrincipal principal, string contentType = "application/json")
        {
            string responseString = null;

            if (request == null)
            {
                return null;
            }

            var req = (HttpWebRequest)WebRequest.Create(request);
            req.Method = method;
            req.Accept = "application/json";
            req.ContentType = contentType;

            req.Headers.Add("Authorization", AdmAuthentication.GetServiceAuthorization(Configuration, service.Id, principal));

            try
            {
                using (var body = req.GetRequestStream())
                {
                    var data = Encoding.UTF8.GetBytes(input);
                    body.Write(data, 0, data.Length);
                    body.Close();
                }

                using var response = (HttpWebResponse)req.GetResponse();
                if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.Accepted)
                {
                    var stream = response.GetResponseStream();
                    using var sr = new StreamReader(stream);
                    responseString = sr.ReadToEnd();
                }
                if (responseString == null)
                {
                    throw new WebException(response.StatusDescription);
                }
            }
            catch (WebException we)
            {
                Trace.WriteLine(we.Message);
                return null;
            }

            return JsonConvert.DeserializeObject(responseString);
        }


        private dynamic NextMusicServiceResults(dynamic last, MusicService service, IPrincipal principal = null)
        {
            var request = service.GetNextRequest(last);
            return request == null ? null : GetMusicServiceResults(request, service, principal);
        }
        #endregion

        public bool UpdateSongAndServices(DanceMusicCoreService dms, Song sd,
            bool crossRetry = false)
        {
            var changed = false;
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var service in MusicService.GetSearchableServices())
            {
                if (crossRetry && sd.Purchase != null && sd.Purchase.Contains(service.CID))
                {
                    break;
                }
                if (UpdateSongAndService(dms, sd, service))
                {
                    if (service.Id == ServiceType.Spotify)
                    {
                        GetEchoData(dms, sd);
                        GetSampleData(dms, sd);
                    }
                    changed = true;
                }
            }
            return changed;
        }

        public bool UpdateSongAndService(
            DanceMusicCoreService dms, Song sd, MusicService service,
            ApplicationUser user = null)
        {
            return UpdateFromTracks(dms, sd,
                MatchSongAndService(sd, service), user);
        }

        private bool UpdateFromTracks(
            DanceMusicCoreService dms, Song sd, IList<ServiceTrack> tracks,
            ApplicationUser user = null)
        {
            if (tracks.Count <= 0) return false;

            var edit = new Song(sd, dms);

            var tags = new TagList();
            foreach (var foundTrack in tracks)
            {
                UpdateMusicServiceFromTrack(dms, edit, foundTrack, ref tags);
            }

            user ??= MusicService.GetService(tracks[0].Service).ApplicationUser;
            if (user != null)
            {
                tags = tags.Add(sd.GetUserTags(user.UserName));
            }

            return dms.EditSong(user, sd, edit, new[] { new UserTag { Id = string.Empty, Tags = tags } });
        }

        public IList<ServiceTrack> MatchSongAndService(Song sd, MusicService service)
        {
            IList<ServiceTrack> found = new List<ServiceTrack>();
            var tracks = FindMusicServiceSong(sd, service);

            // First try the full title/artist
            if ((tracks == null || tracks.Count == 0) && !string.Equals(DefaultServiceSearch(sd, true), DefaultServiceSearch(sd, false)))
            {
                // Now try cleaned up title/artist (remove punctuation and stuff in parens/brackets)
                tracks = FindMusicServiceSong(sd, service);
            }

            if (tracks == null || tracks.Count <= 0) return found;

            // First filter out anything that's not a title-artist match (weak)
            tracks = sd.TitleArtistFilter(tracks);
            if (tracks.Count <= 0) return found;

            // Then check for exact album match if we don't have a tempo
            if (!sd.Length.HasValue)
            {
                foreach (var track in tracks.Where(track => sd.FindAlbum(track.Album, track.TrackNumber) != null))
                {
                    found.Add(track);
                    break;
                }
            }
            // If not exact album match and the song has a length, choose all albums with the same tempo (delta a few seconds)
            else
            {
                found = sd.DurationFilter(tracks, 6);
            }

            // If no album name or length match, choose the 'dominant' version of the title/artist match by clustering lengths
            //  Note that this degenerates to choosing a single album if that is what is available
            if (found.Count == 0 && !sd.HasRealAblums)
            {
                var track = Song.FindDominantTrack(tracks);
                if (track.Duration != null) found = Song.DurationFilter(tracks, track.Duration.Value, 6);
            }

            // Add back in any existing tracks for this service
            var existingIds = sd.GetPurchaseIds(service);
            foreach (var track in existingIds.Where(id => found.All(f => f.TrackId != id)))
            {
                var t = this.GetMusicServiceTrack(track, service);
                if (t != null)
                {
                    found.Add(t);
                }
            }

            return found;
        }

        public static Song UpdateMusicServiceFromTrack(DanceMusicCoreService dms,
            Song song, ServiceTrack track, ref TagList tags)
        {
            var trackId = track.TrackId;
            var ret = UpdateMusicService(song, MusicService.GetService(track.Service),
                track.Name, track.Album, track.Artist, trackId, track.CollectionId,
                track.AltId, track.Duration.ToString(), track.TrackNumber);
            if (track.Genres != null)
            {
                tags = tags.Add(new TagList(
                    dms.NormalizeTags(string.Join("|", track.Genres),
                        "Music", true)));
            }

            return ret;
        }

        public static Song UpdateMusicService(Song song, MusicService service, string name, string album, string artist, string trackId, string collectionId, string alternateId, string duration, int? trackNum)
        {
            // This is a very transitory object to hold the old values for a semi-automated edit
            var alt = new Song();

            if (!string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(song.Title))
            {
                alt.Title = song.Title;
                song.Title = name;
            }

            if (!string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(song.Artist))
            {
                alt.Artist = song.Artist;
                song.Artist = artist;
            }

            var ad = song.FindAlbum(album, trackNum);
            if (ad != null)
            {
                // If there is a match set up the new info next to the album
                var aidxM = song.Albums.IndexOf(ad);

                for (var aidx = 0; aidx < song.Albums.Count; aidx++)
                {
                    if (aidx == aidxM)
                    {
                        var adA = new AlbumDetails(ad);
                        if (string.IsNullOrWhiteSpace(ad.Name))
                        {
                            adA.Name = ad.Name;
                            ad.Name = album;
                        }

                        if (!ad.Track.HasValue || ad.Track.Value == 0)
                        {
                            adA.Track = ad.Track;
                            ad.Track = trackNum;
                        }
                        alt.Albums.Add(adA);
                    }
                    else
                    {
                        alt.Albums.Add(new AlbumDetails());
                    }
                }
            }
            else
            {
                // Otherwise just add an album
                ad = new AlbumDetails { Name = album, Track = trackNum, Index = song.GetNextAlbumIndex() };
                //song.Albums.Insert(0, ad);
                song.Albums.Add(ad);
            }

            UpdateMusicServicePurchase(ad, service, PurchaseType.Song, trackId, alternateId);
            if (collectionId != null)
            {
                UpdateMusicServicePurchase(ad, service, PurchaseType.Album, collectionId);
            }

            if ((!song.Length.HasValue || song.Length == 0) && !string.IsNullOrWhiteSpace(duration))
            {
                try
                {
                    var sd = new SongDuration(duration);

                    var length = decimal.ToInt32(sd.Length);
                    if (length > 9999)
                    {
                        length = 9999;
                    }

                    if (length != song.Length)
                    {
                        alt.Length = song.Length;
                        song.Length = length;
                    }
                }
                catch (ArgumentOutOfRangeException)
                {

                }
            }

            return alt;
        }

        private static void UpdateMusicServicePurchase(AlbumDetails ad, MusicService service, PurchaseType pt, string trackId, string alternateId = null)
        {
            // Don't update if there is alread a trackId
            var old = ad.GetPurchaseIdentifier(service.Id, pt);
            if (old != null && old.StartsWith(trackId))
            {
                return;
            }

            ad.SetPurchaseInfo(pt, service.Id, trackId);
            if (!string.IsNullOrWhiteSpace(alternateId))
            {
                ad.SetPurchaseInfo(pt, ServiceType.AMG, alternateId);
            }
        }

        public IList<ServiceTrack> FindMusicServiceSong(Song song, MusicService service, 
            bool clean, string title, string artist)
        {
            IList<ServiceTrack> tracks = null;
            try
            {
                FixupTitleArtist(song, clean, ref title, ref artist);
                tracks = FindMusicServiceSong(song, service, title, artist);
            }
            catch (WebException we)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, $"Failed '{we.Message}' on Song '{song}");
            }

            return tracks;
        }

        public static string DefaultServiceSearch(Song song, bool clean)
        {
            if (clean)
                return song.CleanTitle + " " + song.CleanArtist;

            return song.Title + " " + song.Artist;
        }

        public static void FixupTitleArtist(Song song, bool clean,
            ref string title, ref string artist)
        {
            if (song != null && artist == null && title == null)
            {
                artist = clean ? song.CleanArtist : song.Artist;
                title = clean ? song.CleanTitle : song.Title;
            }
        }
    }
}
