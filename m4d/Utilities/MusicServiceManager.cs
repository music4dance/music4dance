using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DanceLibrary;
using m4dModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using TagList = m4dModels.TagList;

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

        public async Task<bool> UpdateSongAndServices(DanceMusicCoreService dms, Song sd,
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

                if (await UpdateSongAndService(dms, sd, service))
                {
                    if (service.Id == ServiceType.Spotify)
                    {
                        await GetEchoData(dms, sd);
                        await GetSampleData(dms, sd);
                    }

                    changed = true;
                }
            }

            return changed;
        }

        public async Task<bool> UpdateSongAndService(
            DanceMusicCoreService dms, Song sd, MusicService service)
        {
            var tracks = await MatchSongAndService(sd, service);
            return await UpdateFromTracks(dms, sd, tracks);
        }

        public async Task<bool> ConditionalUpdateSongAndService(
            DanceMusicCoreService dms, Song sd, MusicService service)
        {
            if (sd.ServiceTried(service))
            {
                return false;
            }

            var found = await UpdateSongAndService(dms, sd, service);
            return found || sd.RecordFail(service);
        }

        private async Task<bool> UpdateFromTracks(
            DanceMusicCoreService dms, Song sd, IList<ServiceTrack> tracks)
        {
            if (tracks.Count <= 0)
            {
                return false;
            }

            var edit = await Song.Create(sd, dms);

            var tags = new TagList();
            foreach (var foundTrack in tracks)
            {
                UpdateMusicServiceFromTrack(dms, edit, foundTrack, ref tags);
            }

            var user = MusicService.GetService(tracks[0].Service).ApplicationUser;
            if (user != null)
            {
                tags = tags.Add(sd.GetUserTags(user.UserName));
            }

            return await dms.SongIndex.EditSong(
                user, sd, edit,
                new[] { new UserTag { Id = string.Empty, Tags = tags } });
        }

        public async Task<IList<ServiceTrack>> MatchSongAndService(Song sd, MusicService service)
        {
            IList<ServiceTrack> found = new List<ServiceTrack>();
            var tracks = await FindMusicServiceSong(service, sd);

            // First try the full title/artist
            if ((tracks == null || tracks.Count == 0) &&
                    !string.Equals(DefaultServiceSearch(sd, true), DefaultServiceSearch(sd, false)))
                // Now try cleaned up title/artist (remove punctuation and stuff in parens/brackets)
            {
                tracks = await FindMusicServiceSong(service, sd);
            }

            if (tracks == null || tracks.Count <= 0)
            {
                return found;
            }

            // First filter out anything that's not a title-artist match (weak)
            tracks = sd.TitleArtistFilter(tracks);
            if (tracks.Count <= 0)
            {
                return found;
            }

            // Then check for exact album match if we don't have a tempo
            if (!sd.Length.HasValue)
            {
                foreach (var track in tracks.Where(
                    track =>
                        sd.FindAlbum(track.Album, track.TrackNumber) != null))
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
                if (track.Duration != null)
                {
                    found = Song.DurationFilter(tracks, track.Duration.Value, 6);
                }
            }

            // Add back in any existing tracks for this service
            var existingIds = sd.GetPurchaseIds(service);
            foreach (var track in existingIds.Where(id => found.All(f => f.TrackId != id)))
            {
                var t = await GetMusicServiceTrack(track, service);
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
            var ret = UpdateMusicService(
                song, MusicService.GetService(track.Service),
                track.Name, track.Album, track.Artist, trackId, track.CollectionId,
                track.AltId, track.Duration.ToString(), track.TrackNumber);
            if (track.Genres != null)
            {
                tags = tags.Add(
                    new TagList(
                        dms.NormalizeTags(
                            string.Join("|", track.Genres.Select(TagList.Clean)),
                            "Music")));
            }

            return ret;
        }

        public static Song UpdateMusicService(Song song, MusicService service, string name,
            string album, string artist, string trackId, string collectionId, string alternateId,
            string duration, int? trackNum)
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
                ad = new AlbumDetails
                    { Name = album, Track = trackNum, Index = song.GetNextAlbumIndex() };
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

        private static void UpdateMusicServicePurchase(AlbumDetails ad, MusicService service,
            PurchaseType pt, string trackId, string alternateId = null)
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

        public static string DefaultServiceSearch(Song song, bool clean)
        {
            if (clean)
            {
                return song.CleanTitle + " " + song.CleanArtist;
            }

            return song.Title + " " + song.Artist;
        }

        #region Search

        public async Task<IList<ServiceTrack>> FindMusicServiceSong(MusicService service = null,
            Song song = null, string title = null, string artist = null,
            string album = null)
        {
            IList<ServiceTrack> list;
            if (song != null)
            {
                if (title == null)
                {
                    artist ??= song.Artist;
                }

                title ??= song.Title;
            }

            if (title == null)
            {
                throw new ArgumentNullException(nameof(title));
            }

            if (artist == null)
            {
                throw new ArgumentNullException(nameof(artist));
            }

            if (service != null)
            {
                list = await DoFindMusicServiceSong(service, title, artist);
            }
            else
            {
                var acc = new List<ServiceTrack>();
                foreach (var s in MusicService.GetSearchableServices())
                {
                    var tracks = await DoFindMusicServiceSong(
                        s,
                        title, artist);

                    if (tracks != null)
                    {
                        acc.AddRange(tracks);
                    }
                }

                list = acc;
            }

            if (list == null || list.Count == 0)
            {
                var cleanArtist = Song.CleanString(artist);
                var cleanTitle = Song.CleanString(title);
                return cleanArtist == artist && cleanTitle == title
                    ? null
                    : await FindMusicServiceSong(service, song, cleanTitle, cleanArtist, album);
            }

            list = FilterKaraoke(list);

            list = song != null ? song.RankTracks(list) : Song.RankTracksByCluster(list, album);

            return list;
        }

        public async Task<ServiceTrack> GetMusicServiceTrack(string id, MusicService service)
        {
            var extra = id.IndexOf('[');
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
                var results = await GetMusicServiceResults(request, service);
                ret = await service.ParseTrackResults(
                    results,
                    (Func<string, Task<dynamic>>)(req => GetMusicServiceResults(req, service)));
            }

            if (s_trackCache.Count > 10000)
            {
                s_trackCache.Clear();
            }

            s_trackCache[sid] = ret;
            return ret;
        }

        public async Task<Song> CreateSong(DanceMusicCoreService dms,
            ApplicationUser user, string id, MusicService service)
        {
            var track = await GetMusicServiceTrack(id, service);

            if (track == null)
            {
                return null;
            }

            var song = await Song.UserCreateFromTrack(dms, user, track);
            var found = false;
            var oldSong = await dms.SongIndex.FindMatchingSong(song);

            if (oldSong != null)
            {
                found = true;
                song = oldSong;
            }

            await UpdateSongAndServices(dms, song);
            await UpdateFromTracks(dms, song, new List<ServiceTrack> { track });

            if (found)
            {
                await dms.SongIndex.SaveSong(song);
            }

            return song;
        }

        private static readonly Dictionary<string, ServiceTrack> s_trackCache = new();

        public async Task<GenericPlaylist> LookupPlaylist(MusicService service, string url,
            IEnumerable<string> oldTrackList, IPrincipal principal = null)
        {
            var results =
                await GetMusicServiceResults(service.BuildLookupRequest(url), service, principal);
            if (results == null)
            {
                return null;
            }

            var name = results.name;
            var description = results.description;

            IList<ServiceTrack> tracks = await service.ParseSearchResults(
                results,
                (Func<string, Task<dynamic>>)(req => GetMusicServiceResults(req, service)),
                // ReSharper disable once PossibleMultipleEnumeration
                oldTrackList);
            while ((results = await NextMusicServiceResults(results, service, principal)) != null)
            {
                var t = tracks as List<ServiceTrack> ?? tracks.ToList();
                t.AddRange(
                    await service.ParseSearchResults(
                        results,
                        (Func<string, Task<dynamic>>)(req => GetMusicServiceResults(req, service)),
                        // ReSharper disable once PossibleMultipleEnumeration
                        oldTrackList));
                tracks = t;
            }

            if (tracks == null || tracks.Count == 0)
            {
                return null;
            }

            ComputeTrackPurchaseInfo(service, tracks);

            return new GenericPlaylist
            {
                Name = name.ToString(),
                Description = description.ToString(),
                Tracks = tracks
            };
        }

        // TODO: Handle services other than spotify
        public async Task<List<PlaylistMetadata>> GetPlaylists(MusicService service, IPrincipal principal)
        {
            if (service.Id != ServiceType.Spotify)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(service),
                    "GetPlaylists currently only supports Spotify");
            }

            var results = await GetMusicServiceResults(
                "https://api.spotify.com/v1/me/playlists", service,
                principal);

            if (results == null)
            {
                return new List<PlaylistMetadata>();
            }

            var playlists = ParsePlaylistResults(results);
            while ((results = await NextMusicServiceResults(results, service, principal)) != null)
            {
                playlists.AddRange(ParsePlaylistResults(results));
            }

            return playlists;
        }

        private List<PlaylistMetadata> ParsePlaylistResults(dynamic results)
        {
            if (results == null)
            {
                return null;
            }

            var ret = new List<PlaylistMetadata>();

            foreach (var playlist in results.items)
            {
                var external = playlist.external_urls;
                var link = (external != null && external.spotify != null) ? external.spotify : null;
                ret.Add(
                    new PlaylistMetadata
                    {
                        Id = playlist.id,
                        Name = playlist.name,
                        Description = playlist.description,
                        Link = link,
                        Reference = playlist.tracks?.href,
                        Count = playlist.tracks?.total
                    });
            }

            return ret;
        }

        public virtual async Task<EchoTrack> LookupEchoTrack(string id, MusicService service)
        {
            var request =
                $"https://api.spotify.com/v1/audio-features/{id}"; //$"http://developer.echonest.com/api/v4/track/profile?api_key=B0SEV0FNKNEOHGFB0&format=json&id=spotify:track:{id}&bucket=audio_summary";
            try
            {
                var results = await GetMusicServiceResults(request, service);
                return EchoTrack.BuildEchoTrack(results);
            }
            catch (WebException e)
            {
                Trace.WriteLineIf(
                    TraceLevels.General.TraceError,
                    $"Error looking up echo track {id}: {e.Message}");
                return null;
            }
        }

        #endregion

        #region Update

        public async Task<bool> GetEchoData(DanceMusicCoreService dms, Song song)
        {
            var service = MusicService.GetService(ServiceType.Spotify);
            var ids = song.GetPurchaseIds(service);
            var user = service.ApplicationUser;
            var edit = await Song.Create(song, dms);

            EchoTrack track = null;
            foreach (var id in ids)
            {
                track = await LookupEchoTrack(id, service);
                if (track != null)
                {
                    break;
                }
            }

            if (track == null)
            {
                edit.Danceability = float.NaN;
                return await dms.SongIndex.EditSong(user, song, edit);
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

            if (!await dms.SongIndex.EditSong(
                user, song, edit, new[]
                {
                    new UserTag { Id = string.Empty, Tags = tags }
                }))
            {
                return false;
            }

            return true;
        }

        public async Task<bool> GetSampleData(DanceMusicCoreService dms, Song song)
        {
            var spotify = MusicService.GetService(ServiceType.Spotify);
            var edit = await Song.Create(song, dms);

            ServiceTrack track = null;
            // First try Spotify
            var ids = edit.GetPurchaseIds(spotify);
            var user = await dms.FindUser("batch-s");
            foreach (var id in ids)
            {
                track = await GetMusicServiceTrack(id, spotify);
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
                    track = await GetMusicServiceTrack(id, itunes);
                    if (track?.SampleUrl != null)
                    {
                        user = await dms.FindUser("batch-i");
                        break;
                    }
                }
            }

            edit.Sample = track?.SampleUrl ?? @".";
            return await dms.SongIndex.EditSong(user, song, edit);
        }

        #endregion

        #region Edit

        // TODO: Handle services other than spotify
        public async Task<PlaylistMetadata> CreatePlaylist(MusicService service, IPrincipal principal,
            string name, string description, IFileProvider fileProvider)
        {
            dynamic obj = new
            {
                name,
                description
            };
            var inputs = JsonConvert.SerializeObject(obj);
            var response = await MusicServiceAction(
                "https://api.spotify.com/v1/me/playlists", inputs,
                HttpMethod.Post, service, principal);

            if (response == null)
            {
                return null;
            }

            await MusicServiceAction(
                $"https://api.spotify.com/v1/playlists/{response.id}/images",
                GetEncodedImage(fileProvider, "/wwwroot/images/icons/color-logo.jpg"),
                HttpMethod.Put, service, principal, "image/jpeg");

            return new PlaylistMetadata
            {
                Id = response.id,
                Name = response.name
            };
        }

        private string GetEncodedImage(IFileProvider fileProvider, string path)
        {
            var fullPath = fileProvider.GetFileInfo(path).PhysicalPath;

            using var image = Image.Load(fullPath, out var format);
            using var m = new MemoryStream();
            image.Save(m, format);
            var imageBytes = m.ToArray();
            var base64String = Convert.ToBase64String(imageBytes);
            return base64String;
        }

        public async Task<bool> SetPlaylistTracks(MusicService service, IPrincipal principal, string id,
            IEnumerable<string> tracks)
        {
            var tracklist = string.Join(
                ",",
                tracks.Where(t => t != null).Select(t => $"\"spotify:track:{t}\""));
            var response = await MusicServiceAction(
                $"https://api.spotify.com/v1/playlists/{id}/tracks",
                $"{{\"uris\":[{tracklist}]}}", HttpMethod.Put, service, principal);

            return response != null && response.snapshot_id != null;
        }

        #endregion

        #region Utilities

        private static IList<ServiceTrack> FilterKaraoke(IList<ServiceTrack> list)
        {
            return list
                .Where(track => !ContainsKaraoke(track.Name) && !ContainsKaraoke(track.Album))
                .ToList();
        }

        private static bool ContainsKaraoke(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var exclude = new[] { "karaoke", "in the style of", "a tribute to" };
            return exclude.Any(
                s =>
                    name.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) != -1);
        }

        private async Task<IList<ServiceTrack>> DoFindMusicServiceSong(MusicService service,
            string title = null, string artist = null)
        {
            var tracks = await FindMSSongGeneral(service, title, artist);

            if (tracks == null)
            {
                return null;
            }

            ComputeTrackPurchaseInfo(service, tracks);

            return tracks;
        }

        private void ComputeTrackPurchaseInfo(MusicService service,
            IEnumerable<ServiceTrack> tracks)
        {
            foreach (var track in tracks)
            {
                track.AlbumLink = service.GetPurchaseLink(
                    PurchaseType.Album, track.CollectionId,
                    track.TrackId);
                track.SongLink = service.GetPurchaseLink(
                    PurchaseType.Song, track.CollectionId,
                    track.TrackId);
                track.PurchaseInfo =
                    AlbumDetails.BuildPurchaseInfo(service.Id, track.CollectionId, track.TrackId);
            }
        }

        // ReSharper disable once InconsistentNaming
        private async Task<IList<ServiceTrack>> FindMSSongGeneral(MusicService service, string title = null,
            string artist = null)
        {
            try
            {
                var results =
                  await GetMusicServiceResults(service.BuildSearchRequest(artist, title), service);
                return await service.ParseSearchResults(
                    results,
                    (Func<string, Task<dynamic>>)(req => GetMusicServiceResults(req, service)), null);
            }
            catch (AbortBatchException)
            {
                throw;
            }
            catch (Exception e)
            {
                Trace.TraceWarning(
                    $"Hard failure searching for {title} by {artist} on {service.Name}: {e.Message}");
                return new List<ServiceTrack>();
            }
        }

        private static int GetRateInfo(HttpResponseHeaders headers, string type)
        {
            if (!headers.TryGetValues(type, out var values)) {
                return -1;
            }

            return int.TryParse(values.First(), out var info) ? info : -1;
        }

        private async Task<dynamic> GetMusicServiceResults(string request,
            MusicService service,
            IPrincipal principal = null)
        {
            if (CheckPaused(service))
            {
                return null;
            }

            var retries = 5;
            while (true)
            {
                string responseString = null;

                if (request == null)
                {
                    return null;
                }

                using var req = new HttpRequestMessage(HttpMethod.Get, request);
                req.Headers.Add("Accept", "application/json");
                string auth = null;
                if (service != null)
                {
                    auth = await AdmAuthentication.GetServiceAuthorization(
                        Configuration, service.Id,
                        principal);
                }

                if (auth != null)
                {
                    req.Headers.Add("Authorization", auth);
                }

                try
                {
                    using var response = await HttpClientHelper.Client.SendAsync(req);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var remaining = GetRateInfo(response.Headers, "X-RateLimit-Remaining");

                        responseString = await response.Content.ReadAsStringAsync();

                        if (remaining is > 0 and < 20)
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceInfo,
                                $"Excedeed EchoNest Limits: Pre-emptive {remaining} - used = {GetRateInfo(response.Headers, "X-RateLimit-Used")} - limit = {GetRateInfo(response.Headers, "X-RateLimit-Limit")}");
                            Thread.Sleep(3 * 1000);
                        }
                    }
                    else if ((int)response.StatusCode == 429 /*HttpStatusCode.TooManyRequests*/)
                    {
                        // Wait algorithm failed, pause for 15 seconds
                        Trace.WriteLineIf(
                            TraceLevels.General.TraceInfo,
                            "Exceeded EchoNest Limits: Caught");
                        Thread.Sleep(15 * 1000);
                        continue;
                    }

                    if (responseString == null)
                    {
                        throw new WebException(response.ReasonPhrase);
                    }
                }
                catch (WebException we)
                {
                    if (we.Response is HttpWebResponse r)
                    {
                        var statusCode = (int)r.StatusCode;
                        if (statusCode == 429)
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceInfo,
                                "Exceeded EchoNest Limits: Caught");
                            Thread.Sleep(15 * 1000);
                            continue;
                        }

                        if (statusCode == 403 && service?.Id == ServiceType.ITunes)
                        {
                            if (retries-- > 0)
                            {
                                Trace.WriteLineIf(
                                    TraceLevels.General.TraceInfo,
                                    $"Exceeded Itunes Limits: {5 - retries} {req.RequestUri}");
                                Thread.Sleep(60 * 1000);
                                continue;
                            }

                            _pauseITunes = DateTime.Now;

                            var message =
                                $"Exceeded Itunes Limits @{_pauseITunes}: Pausing {req.RequestUri}";
                            Trace.WriteLineIf(TraceLevels.General.TraceInfo, message);

                            throw new AbortBatchException(message, we);
                        }
                    }

                    throw;
                }

                if (service != null)
                {
                    responseString = service.PreprocessResponse(responseString);
                    if (service.Id == ServiceType.ITunes)
                    {
                        iTunesCalls += 1;
                    }
                    else if (service.Id == ServiceType.Spotify)
                    {
                        spotifyCalls += 1;
                    }
                }

                return JsonConvert.DeserializeObject(responseString);
            }
        }

        private static bool CheckPaused(MusicService service)
        {
            if (service.Id != ServiceType.ITunes)
            {
                // Only pausing itunes for now
                return false;
            }

            if (_pauseITunes == DateTime.MinValue)
            {
                // iTunes is paused when this value is set to DateTime.Now
                return false;
            }

            if (PauseExpired)
            {
                // We've been paused for at least 15 minutes, unpause
                _pauseITunes = DateTime.MinValue;
                return false;
            }

            Skipped += 1;
            return true;
        }

        public static bool Paused => _pauseITunes != DateTime.MinValue && !PauseExpired;

        public static int iTunesCalls { get; private set; }

        public static int spotifyCalls { get; private set; }

        private static bool PauseExpired => _pauseITunes.AddMinutes(15) < DateTime.Now;
        private static DateTime _pauseITunes = DateTime.MinValue;

        public static int Skipped { get; private set; }

        // TODO Handle services other than spotify.
        // This method requires a valid principal
        private async Task<dynamic> MusicServiceAction(string request, string input, HttpMethod method,
            MusicService service, IPrincipal principal, string contentType = "application/json")
        {
            string responseString = null;

            if (request == null)
            {
                return null;
            }

            using var req = new HttpRequestMessage(method, request);
            req.Headers.Add("Accept", "application/json");
            if (!string.IsNullOrEmpty(input))
            {
                req.Content = new StringContent(input, Encoding.UTF8, contentType);
            }

            req.Headers.Add(
                "Authorization",
                await AdmAuthentication.GetServiceAuthorization(Configuration, service.Id, principal));

            try
            {
                using var response = await HttpClientHelper.Client.SendAsync(req);

                if (response.StatusCode == HttpStatusCode.Created ||
                    response.StatusCode == HttpStatusCode.Accepted)
                {
                    responseString = await response.Content.ReadAsStringAsync();
                }

                if (responseString == null)
                {
                    throw new WebException(response.ReasonPhrase);
                }
            }
            catch (WebException we)
            {
                Trace.WriteLine(we.Message);
                return null;
            }

            return JsonConvert.DeserializeObject(responseString);
        }


        private async Task<dynamic> NextMusicServiceResults(dynamic last, MusicService service,
            IPrincipal principal = null)
        {
            var request = service.GetNextRequest(last);
            return request == null ? null : await GetMusicServiceResults(request, service, principal);
        }

        #endregion
    }
}
