using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Rest.Azure;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using FacetResults = System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<Microsoft.Azure.Search.Models.FacetResult>>;

namespace m4dModels
{
    public class DanceMusicService : IDisposable
    {
        #region Lifetime Management

        public static IDanceMusicFactory Factory { get; set; }

        public static DanceMusicService GetService()
        {
            return Factory.CreateDisconnectedService();
        }

        private IDanceMusicContext _context;

        public DanceMusicService(IDanceMusicContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            UserManager = userManager;
        }
        public void Dispose()
        {
            var temp = _context;
            _context = null;
            temp?.Dispose();
        }

        public bool IsDisposed => _context == null;

        #endregion

        #region Properties

        public IDanceMusicContext Context => _context;
        public DbSet<Dance> Dances => _context.Dances;
        public DbSet<TagGroup> TagGroups => _context.TagGroups;
        public DbSet<Search> Searches => _context.Searches;
        public DbSet<PlayList> PlayLists => _context.PlayLists;
        public UserManager<ApplicationUser> UserManager { get; }

        public int SaveChanges()
        {
            return _context.SaveChanges();
        }

        public void SaveSong(Song song, string id = "default")
        {
            SaveSongs(new [] {song}, id);
        }

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public void SaveSongs(IEnumerable<Song> songs, string id = "default")
        {
            if (songs == null || !songs.Any()) return;

            // DBKill: we probably need to keep a DSI for each index (built on demand of course) so that we can do live swaps
            var stats = DanceStats;
            foreach (var song in songs)
            {
                stats.UpdateSong(song, this);
            }

            UpdateAzureIndex(id);
        }

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public void SaveSongsImmediate(IEnumerable<Song> songs, string id = "default")
        {
            if (songs == null || !songs.Any()) return;

            var stats = DanceStats;
            foreach (var song in songs)
            {
                stats.UpdateSong(song, this);
            }

            IndexUpdater.Enqueue(id);
        }

        public static readonly string EditRole = "canEdit";
        public static readonly string TagRole = "canTag";
        public static readonly string DiagRole = "showDiagnostics";
        public static readonly string DbaRole = "dbAdmin";
        public static readonly string PseudoRole = "pseudoUser";
        public static readonly string PremiumRole = "premium";
        public static readonly string BetaRole = "beta";
        public static readonly string TrialRole = "trial";

        public static readonly string[] Roles = { DiagRole, EditRole, DbaRole, TagRole, PremiumRole, TrialRole, BetaRole };

        #endregion

        #region Edit
        private static Song CreateSong(Guid? guid = null)
        {
            return new Song {SongId =  (guid == null || guid == Guid.Empty) ? Guid.NewGuid() : guid.Value};
        }

        public Song CreateSong(ApplicationUser user, Song sd=null,  IEnumerable<UserTag> tags = null, string command = Song.CreateCommand, string value = null)
        {
            if (sd != null)
            {
                Trace.WriteLineIf(string.Equals(sd.Title, sd.Artist), $"Title and Artist are the same ({sd.Title})");
            }

            var song = CreateSong(sd?.SongId);
            if (sd == null)
            {
                song.Create(user.UserName, command, value, true);
            }
            else
            {
                song.Create(sd, tags, user.UserName, command, value, DanceStats);
            }

            return new Song(song,DanceStats,user.UserName);
        }

        public Song EditSong(ApplicationUser user, Song edit, IEnumerable<UserTag> tags = null)
        {
            var song = FindSong(edit.SongId);

            // TODO: Figure out if we need to rebuild the song after edit in all cases or if there is a cleaner way to do this
            return !song.Edit(user.UserName, edit, tags, DanceStats) ? null : new Song(song.SongId, song.SongProperties, DanceStats);
        }

        // Returns true if changed
        public bool EditSong(string user, Song song, Song edit, IEnumerable<UserTag> tags = null)
        {
            var changed = song.Edit(user, edit, tags, DanceStats);
            if (!changed) return false;
            song.Load(song.SongId,song.SongProperties,DanceStats);
            return true;
        }

        public bool AdminEditSong(Song edit, string properties)
        {
            return edit.AdminEdit(properties,DanceStats);
        }

        public bool AdminAppendSong(Song edit, string user, string properties)
        {
            return edit.AdminAppend(user, properties, DanceStats);
        }

        public bool AdminEditSong(string properties)
        {
            if (Song.TryParseId(properties, out var id) == 0) return false;

            var song = FindSong(id);
            return song != null && AdminEditSong(song, properties);
        }

        public bool AdminModifySong(Song edit, string songModifier)
        {
            return edit.AdminModify(songModifier, DanceStats);
        }

        public Song UpdateSong(ApplicationUser user, Song song, Song edit)
        {
            if (!song.Update(user.UserName, edit, DanceStats)) return null;
            SaveSong(song);
            return song;
        }

        // This is an additive merge - only add new things if they don't conflict with the old
        //  TODO: I'm pretty sure I can clean up this and all the other editing stuff by pushing
        //  the diffing part down into Song (which will also let me unit test it more easily)
        public bool AdditiveMerge(string user, Song initial, Song edit, List<string> addDances)
        {
            return initial.AdditiveMerge(user, edit, addDances, DanceStats);
        }

        public void UpdateDances(ApplicationUser user, Song song, IEnumerable<DanceRatingDelta> deltas)
        {
            song.CreateEditProperties(user.UserName, Song.EditCommand);
            song.EditDanceRatings(deltas, DanceStats);
        }

        public bool EditTags(ApplicationUser user, Guid songId, IEnumerable<UserTag> tags)
        {
            var song = FindSong(songId);

            if (!song.EditTags(user.UserName, tags, DanceStats)) return false;

            SaveSong(song);
            return true;
        }

        public bool EditLike(ApplicationUser user, Guid songId, bool? like, string danceId=null)
        {
            var song = FindSong(songId);

            if (danceId == null)
            {
                if (!song.EditLike(user.UserName, like)) return false;
            }
            else
            {
                if (!song.EditDanceLike(user.UserName, like, danceId, DanceStats)) return false;
            }

            SaveSong(song);
            return true;
        }

        public bool? GetLike(ApplicationUser user, Guid songId, string danceId = null)
        {
            var song = FindSong(songId);

            return danceId == null ? song.GetLike(user.UserName) : song.GetDanceLike(user.UserName, danceId, DanceStats);
        }

        public int CleanupAlbums(ApplicationUser user, Song song)
        {
            var albums = AlbumDetails.MergeAlbums(song.Albums, song.Artist, true);
            if (albums.Count == song.Albums.Count) return 0;

            var delta = song.Albums.Count - albums.Count;
            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, $"{delta}: {song.Title} {song.Artist}");
            song.Albums = albums.ToList();
            EditSong(user, song);
            return delta;
        }

        private static IList<AlbumDetails> MergeAlbums(IEnumerable<Song> songs, string def, ICollection<string> keys, string artist)
        {
            var details = (songs as IList<Song>)??songs.ToList();
            var albumsIn = new List<AlbumDetails>();
            var albumsOut = new List<AlbumDetails>();

            foreach (var sd in details)
            {
                albumsIn.AddRange(sd.Albums);
            }

            var defIdx = -1;
            if (!string.IsNullOrWhiteSpace(def))
            {
                int.TryParse(def, out defIdx);
            }

            var idx = 0;
            if (defIdx >= 0 && albumsIn.Count > defIdx)
            {
                var t = albumsIn[defIdx];
                t.Index = 0;
                albumsOut.Add(t);
                idx = 1;
            }

            for (var i = 0; i < albumsIn.Count; i++)
            {
                if (i == defIdx) continue;

                var name = Song.AlbumListField + "_" + i;

                if (defIdx != -1 && !keys.Contains(name)) continue;

                var t = albumsIn[i];
                t.Index = idx;
                albumsOut.Add(t);
                idx += 1;
            }

            return AlbumDetails.MergeAlbums(albumsOut,artist,false);
        }

        public Song MergeSongs(ApplicationUser user, List<Song> songs, string title, string artist, decimal? tempo, int? length, IList<AlbumDetails> albums)
        {
            var songIds = songs.Select(s => s.SongId).ToList();
            var stringIds = string.Join(";", songIds.Select(id => id.ToString()));

            if (songs.Any(s => s.SongProperties == null))
            {
                songs = FindSongs(songIds).ToList();
            }

            var song = CreateSong(user, null,null, Song.MergeCommand, stringIds);

            // Add in the properties for all of the songs and then delete them
            foreach (var from in songs)
            {
                song.UpdateProperties(from.SongProperties, DanceStats, new[]
                {
                    Song.FailedLookup, Song.AlbumField, Song.TrackField, Song.PublisherField, Song.PurchaseField, Song.AlbumListField, Song.AlbumOrder, Song.AlbumPromote
                });
                DeleteSong(user, from);
            }

            var sd = new Song(title, artist, tempo, length, new List<AlbumDetails>())
            {
                Danceability = song.Danceability,
                Energy = song.Energy,
                Valence = song.Valence,
                Sample = song.Sample
            };

            song.Edit(user.UserName, sd, null, DanceStats);

            song.CreateAlbums(albums);

            song =  new Song(song.SongId,song.SongProperties,DanceStats);
            song.CleanupProperties();

            SaveSong(song);

            return song;
        }

        public Song MergeSongs(ApplicationUser user, List<Song> songs, string title, string artist, decimal? tempo, int? length, string defAlbums, HashSet<string> keys)
        {
            return MergeSongs(user,songs,title,artist,tempo,length,MergeAlbums(songs,defAlbums,keys,artist));
        }

        public void DeleteSong(ApplicationUser user, Song song)
        {
            song.Delete(user.UserName);
            SaveSong(song);
        }

        public BatchInfo CleanupProperties(int max, DateTime from, SongFilter filter)
        {
            var ret = new BatchInfo();

            if (filter == null) filter = new SongFilter();
            var songlist = TakeTail(filter, max, from, CruftFilter.AllCruft);

            var lastTouched = DateTime.MinValue;
            var succeeded = new List<Song>();
            var failed = new List<Song>();
            var cummulative = 0;

            foreach (var song in songlist)
            {
                lastTouched = song.Modified;

                var init = song.SongProperties.Count;

                var changed = song.CleanupProperties();

                var final = song.SongProperties.Count;

                if (changed)
                {
                    Trace.WriteLine($"Succeeded ({init - final})): {song}");
                    succeeded.Add(song);
                    cummulative += init - final;
                }
                else
                {
                    if (init - final != 0)
                    {
                        Trace.WriteLine($"Failed ({init - final}): {song}");
                    }
                    Trace.WriteLine($"Skipped: {song}");
                    failed.Add(song);
                }

                SaveSongs(succeeded);

                AdminMonitor.UpdateTask("CleanProperty", succeeded.Count + failed.Count);

                if (succeeded.Count + failed.Count >= max)
                {
                    break;
                }
            }

            ret.LastTime = lastTouched;
            ret.Succeeded = succeeded.Count;
            ret.Failed = failed.Count;

            ret.Message = $"Cleaned up {cummulative} properties.";
            if (ret.Succeeded > 0)
            {
                SaveChanges();
            }

            if (ret.Succeeded + ret.Failed >= max) return ret;

            ret.Complete = true;
            ret.Message += "No more songs to clean up";

            return ret;
        }

        public  IList<Song> SongsFromTracks(string user, IEnumerable<ServiceTrack> tracks, string dances, string songTags, string danceTags)
        {
            return tracks.Where(track => !string.IsNullOrEmpty(track.Artist)).Select(track => Song.CreateFromTrack(user, track, dances, songTags, danceTags, DanceStats)).ToList();
        }

        public IList<Song> SongsFromTracks(string user, IEnumerable<ServiceTrack> tracks, string multiDance, string songTags)
        {
            return tracks.Where(track => !string.IsNullOrEmpty(track.Artist)).Select(track => Song.CreateFromTrack(user, track, multiDance, songTags, DanceStats)).ToList();
        }

        public IList<Song> SongsFromFile(string user, IList<string> lines)
        {
            var map = Song.BuildHeaderMap(lines[0]);
            lines.RemoveAt(0);
            return Song.CreateFromRows(user, "\t", map, lines, DanceStats, Song.DanceRatingCreate);
        }


        #endregion

        #region Dance Ratings

        public void UpdateDanceRatingsAndTags(Song sd, ApplicationUser user, IEnumerable<string> dances, string songTags, string danceTags, int weight)
        {
            if (!string.IsNullOrEmpty(songTags))
            {
                sd.AddTags(songTags, user.UserName, DanceStats, sd, false);
            }
            var danceList = dances as IList<string> ?? dances.ToList();
            sd.UpdateDanceRatingsAndTags(user.UserName, danceList, Song.DanceRatingIncrement,DanceStats);
            if (!string.IsNullOrWhiteSpace(danceTags))
            {
                foreach (var id in danceList)
                {
                    sd.ChangeDanceTags(id, danceTags, user.UserName, DanceStats);
                }
            }
            sd.InferDances(user.UserName);
        }

        #endregion

        #region Logging

        public void UndoUserChanges(ApplicationUser user, Guid songId)
        {
            var song = FindSong(songId);

            // Delete the songprops
            SongProperty lastCommand = null;
            var props = new List<SongProperty>();
            var collect = false;
            foreach (var prop in song.SongProperties)
            {
                if (prop.Name == Song.UserField)
                {
                    collect = string.Equals(prop.Value, user.UserName, StringComparison.OrdinalIgnoreCase);
                }
                else if (!collect && prop.IsAction)
                {
                    lastCommand = prop;
                }

                if (!collect) continue;

                if (lastCommand != null)
                {
                    props.Add(lastCommand);
                }
                props.Add(prop);
                lastCommand = null;
            }
            foreach (var prop in props)
            {
                song.SongProperties.Remove(prop);
            }

            AdminEditSong(song, song.Serialize(null));

            SaveSong(song);
        }


        #endregion

        #region Song Lookup

        public Song FindSong(Guid id, string userName = null)
        {
            if (string.IsNullOrEmpty(userName)) userName = null;

            var info = SearchServiceInfo.GetInfo();

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                return InternalFindSong(id, userName, indexClient);
            }
        }

        public IEnumerable<Song> FindSongs(IEnumerable<Guid> ids, string userName = null)
        {
            // TODO: Can we do a more efficient Azure Seach (look at comment SongsFromIds)
            var info = SearchServiceInfo.GetInfo();

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                return ids.Select(id => InternalFindSong(id, userName, indexClient)).ToList();
            }
        }

        private Song InternalFindSong(Guid id, string userName, ISearchIndexClient client)
        {
            var sd = DanceStats.FindSongDetails(id, userName);
            if (sd != null) return sd;

            try
            {
                var doc = client.Documents.Get(id.ToString(), new[] { Song.PropertiesField });
                if (doc == null) return null;

                var details = new Song(id, doc[Song.PropertiesField] as string, DanceStats, userName);
                return details;
            }
            catch (CloudException e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose, e.Message);
                return null;
            }
        }

        private IEnumerable<Song> SongsFromAzureResult(DocumentSearchResult<Document> result, string user = null)
        {
            return result.Results.Select(d => new Song(d.Document, DanceStats, user));
        }

        private IEnumerable<Song> FindUserSongs(string user, bool includeHate=false, string id = "default")
        {
            const int max = 10000;

            var filter = SongFilter.AzureSimple;
            filter.User = user;
            if (includeHate) filter.User += "|A";

            var afilter = AzureParmsFromFilter(filter);
            afilter.Top = max;
            afilter.IncludeTotalResultCount = false;

            var results = new List<Song>();

            var stats = DanceStats;

            var info = SearchServiceInfo.GetInfo(id);

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                var response = DoAzureSearch(null, afilter, CruftFilter.AllCruft, indexClient);

                results.AddRange(SongsFromAzureResult(response,user));

                if (response.ContinuationToken == null) return results;

                try
                {
                    while (response.ContinuationToken != null && results.Count < max)
                    {
                        response = indexClient.Documents.ContinueSearch(response.ContinuationToken);
                        results.AddRange(response.Results.Select(d => new Song(d.Document, stats, user)));
                    }
                }
                catch (CloudException e)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceVerbose, e.Message);
                }
                return results;
            }
        }

        public Song FindMergedSong(Guid id, string userName, ISearchIndexClient client = null)
        {
            return DoAzureSearch(null, 
                new SearchParameters {Filter = $"(AlternateIds/any(t: t eq '{id}'))"}, CruftFilter.AllCruft, client)
                .Results.Select(
                    r => new Song(r.Document, DanceStats, userName)).FirstOrDefault(s => !s.IsNull);
        }

        public DateTimeOffset GetLastModified(ISearchIndexClient client = null)
        {
            var ret = DoAzureSearch(null, new SearchParameters {OrderBy=new [] { "Modified desc" }, Top=1,Select=new[] {Song.ModifiedField} }, CruftFilter.AllCruft, client);
            if (ret.Results.Count == 0) return DateTime.MinValue;

            var d = ret.Results[0].Document[Song.ModifiedField];
            return (DateTimeOffset) d;
        }

        private IEnumerable<Song> SongsFromList(string list)
        {
            var dels = list.Split(';');
            var songs = new List<Song>(list.Length);

            foreach (var t in dels)
            {
                Guid idx;
                if (!Guid.TryParse(t, out idx)) continue;

                var s = FindSong(idx);
                if (s != null)
                {
                    songs.Add(s);
                }
            }

            return songs;
        }
        #endregion

        #region Searching

        [Flags]
        public enum CruftFilter
        {
            NoCruft = 0x00,
            NoPublishers = 0x01,
            NoDances =0x02,
            AllCruft = 0x03
        };

        public LikeDictionary UserLikes(IEnumerable<Song> songs, string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) return null;

            var likes = new LikeDictionary();
            foreach (var s in songs)
            {
                var mod = s.ModifiedBy.FirstOrDefault(m => m.UserName == userName);
                if (mod != null)
                {
                    likes.Add(s.SongId, mod.Like);
                }
            }
            return likes;
        }

        public LikeDictionary UserDanceLikes(IEnumerable<Song> songs, string danceId, string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) return null;

            var likes = new LikeDictionary();
            foreach (var s in songs)
            {
                var level = s.UserDanceRating(userName, danceId);

                if (level > 0) likes.Add(s.SongId, true);
                else if (level < 0) likes.Add(s.SongId, false);
            }
            return likes;
        }

        // TODO: Think about aggregating annonymous & users to show most searched, most recent, etc.
        public void UpdateSearches(ApplicationUser user, SongFilter filter)
        {
            try
            {
                var f = new SongFilter(filter.ToString()) {Page = null};
                if (user != null)
                    f.Anonymize(user.UserName);
                if (!f.IsAzure)
                    f.Action = null;
                var userId = user?.Id;
                var q = f.ToString();
                var search = Searches.FirstOrDefault(s => s.ApplicationUserId == userId && s.Query == q);
                var now = DateTime.Now;
                if (search == null)
                {
                    search = Searches.Create();
                    search.ApplicationUserId = userId;
                    search.Created = now;
                    search.Query = q;
                    search.Count = 0;
                    search.Name = null;
                    Searches.Add(search);
                }

                search.Modified = now;
                search.Count += 1;

                SaveChanges();
            }
            catch (Exception e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceWarning, $"UpdateSearches: Failed with {e.Message}");
            }
        }

        public enum MatchMethod { None, Tempo, Merge };

        private IEnumerable<Song> SongsFromHash(int hash, ISearchIndexClient client = null)
        {
            return DoAzureSearch(null, new SearchParameters { Filter = $"(TitleHash eq {hash})" }, CruftFilter.AllCruft, client)
                    .Results.Select(
                        r => new Song(r.Document, DanceStats));
        }
        private LocalMerger MergeFromTitle(Song song, ISearchIndexClient client)
        {
            var songs = SongsFromHash(song.TitleHash, client);

            var candidates = (from s in songs where Song.SoftArtistMatch(s.Artist, song.Artist) select s).ToList();

            if (candidates.Count <= 0)
                return new LocalMerger {Left = song, Right = null, MatchType = MatchType.None, Conflict = false};

            Song match = null;
            var type = MatchType.None;

            // Now we have a list of existing songs that are a title-artist match to our new song - so see
            //  if we have a title-artist-album match
            if (song.HasAlbums)
            {
                var songD = song;
                foreach (var s in candidates.Where(s => s.FindAlbum(songD.Albums[0].Name) != null))
                {
                    match = s;
                    type = MatchType.Exact;
                    break;
                }
            }

            // If not, try for a length match
            if (match == null && song.Length.HasValue)
            {
                var songD = song;
                foreach (var s in candidates.Where(s => songD.Length != null && (s.Length.HasValue && Math.Abs(s.Length.Value - songD.Length.Value) < 5)))
                {
                    match = s;
                    type = MatchType.Length;
                    break;
                }
            }

            // TODO: We may want to make this even weaker (especially for merge): If merge doesn't have album remove candidate.HasRealAlbums?

            // Otherwise, if there is only one candidate we will choose it
            // TODO: and it doesn't have any 'real' albums [&& (!song.HasAlbums || !candidates[0].HasRealAblums)] (I obviously wanted this extra filter at some point...)
            if (match == null && candidates.Count == 1)
            {
                type = MatchType.Weak;
                match = candidates[0];
            }

            return new LocalMerger { Left = song, Right = match, MatchType = type, Conflict = false };
        }

        private LocalMerger MergeFromPurchaseInfo(Song song, ISearchIndexClient client)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var service in MusicService.GetSearchableServices())
            {
                var id = song.GetPurchaseId(service.Id);

                if (id == null) continue;

                var match = GetSongFromService(service, id, null, client);
                if (match != null)
                {
                    return new LocalMerger { Left = song, Right = match, MatchType = MatchType.Exact, Conflict = false };
                }
            }
            return null;
        }

        public IList<LocalMerger> MatchSongs(IList<Song> newSongs, MatchMethod method, string id = "default")
        {
            newSongs = RemoveDuplicateSongs(newSongs);
            var merge = new List<LocalMerger>();

            var info = SearchServiceInfo.GetInfo(id);
            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                foreach (var song in newSongs)
                {
                    var m = MergeFromPurchaseInfo(song, indexClient) ?? MergeFromTitle(song, indexClient);

                    switch (method)
                    {
                        case MatchMethod.Tempo:
                            if (m.Right != null)
                                m.Conflict = song.TempoConflict(m.Right, 3);
                            break;
                        case MatchMethod.Merge:
                            // Do we need to do anything special here???
                            break;
                    }

                    merge.Add(m);
                    Trace.WriteLineIf(merge.Count % 10 == 0, $"{merge.Count} songs merged");
                    AdminMonitor.UpdateTask("Merge", merge.Count);
                }

                return merge;
            }
        }

        public IList<Song> RemoveDuplicateSongs(IList<Song> songs)
        {
            var hash = new HashSet<string>();
            var ret = new List<Song>();
            foreach (var song in songs)
            {
                var key = song.TitleArtistString;
                if (hash.Contains(key))
                    continue;
                hash.Add(key);
                ret.Add(song);
            }

            return ret;
        }

        public IEnumerable<Song> MergeCatalog(string user, IList<LocalMerger> merges, IEnumerable<string> dances = null)
        {
            var songs = new List<Song>();

            var dancesL = dances?.ToList() ?? new List<string>();

            foreach (var m in merges)
            {

                // Matchtype of none indicates a new (to us) song, so just add it
                if (m.MatchType == MatchType.None)
                {
                    if (dancesL.Any())
                    {
                        m.Left.UpdateDanceRatingsAndTags(user, dancesL, Song.DanceRatingInitial,DanceStats);
                        m.Left.InferDances(user);
                    }
                    songs.Add(m.Left);
                }
                // Any other matchtype should result in a merge, which for now is just adding the dance(s) from
                //  the new list to the existing song (or adding weight).
                // Now we're going to potentially add tempo - need a more general solution for this going forward
                else
                {
                    if (AdditiveMerge(user, m.Right, m.Left, dancesL))
                        songs.Add(m.Right);
                }
            }

            return songs;
        }
        public static ICollection<ICollection<PurchaseLink>> GetPurchaseLinks(ServiceType serviceType, IEnumerable<Song> songs, string region = null)
        {
            if (songs == null) return null;

            var links = new List<ICollection<PurchaseLink>>();
            var cid = MusicService.GetService(serviceType).CID;
            var sid = cid.ToString();

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var song in songs)
            {
                if (song.Purchase == null || !song.Purchase.Contains(cid)) continue;

                var l = song.GetPurchaseLinks(sid,region);
                if (l != null)
                    links.Add(l);
            }

            return links;
        }

        public ICollection<ICollection<PurchaseLink>> GetPurchaseLinks(ServiceType serviceType, IEnumerable<Guid> songIds, string region = null)
        {
            return GetPurchaseLinks(serviceType, FindSongs(songIds), region);
        }

        public string GetPurchaseInfo(ServiceType serviceType, IEnumerable<Song> songs, string region)
        {
            var songLinks = GetPurchaseLinks(serviceType, songs, region);
            return PurchaseLinksToInfo(songLinks, region);
        }

        public string GetPurchaseInfo(ServiceType serviceType, IEnumerable<Guid> songIds, string region)
        {
            var songLinks = GetPurchaseLinks(serviceType, songIds, region);
            return PurchaseLinksToInfo(songLinks, region);
        }

        public string GetPurchaseInfo(ServiceType serviceType, string ids, string region)
        {
            return GetPurchaseInfo(serviceType, Array.ConvertAll(ids.Split(','), s => new Guid(s)), region);
        }

        public static string PurchaseLinksToInfo(ICollection<ICollection<PurchaseLink>> songLinks, string region)
        {
            var results = (from links in songLinks select links.SingleOrDefault(l => l.AvailableMarkets == null || l.AvailableMarkets.Contains(region)) into link where link != null select link.SongId).ToList();
            return string.Join(",",results);
        }

        public static ICollection<PurchaseLink> ReducePurchaseLinks(ICollection<ICollection<PurchaseLink>> songLinks, string region)
        {
            return (from links in songLinks select links.SingleOrDefault(l => l.AvailableMarkets == null || l.AvailableMarkets.Contains(region)) into link where link != null select link).ToList();
        }
        public Song GetSongFromService(MusicService service,string id,string userName=null, ISearchIndexClient client = null)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var sid = $"\"{service.CID}:{id}\"";
            var parameters = new SearchParameters {SearchFields = new [] {Song.ServiceIds} };
            var results = DoAzureSearch(sid,parameters,CruftFilter.AllCruft, client);
            return (results.Results.Count > 0) ? new Song(results.Results[0].Document,DanceStats,userName) : null;
        }

        #endregion

        #region Tags

        public IReadOnlyDictionary<string, TagGroup> TagMap => DanceStatsManager.GetInstance(this).TagManager.TagMap;

        public DanceStatsInstance DanceStats => DanceStatsManager.GetInstance(this);

        public TagGroup FindOrCreateTagGroup(string value, string category, string primary = null)
        {
            return TagGroups.Find(TagGroup.BuildKey(value, category)) ?? CreateTagGroup(value, category);
        }

        // Add in category for tags that don't already have one + create
        //  tagtype if necessary
        public string NormalizeTags(string tags, string category, bool useGroup=false)
        {
            var old = new TagList(tags);
            var result = new List<string>();
            foreach (var tag in old.Tags)
            {
                var fullTag = tag;
                var tempTag = tag;
                var tempCat = category;
                var rg = tag.Split(':');
                if (rg.Length == 1)
                {
                    fullTag = TagGroup.BuildKey(tag, category);
                }
                else if (rg.Length == 2)
                {
                    tempTag = rg[0];
                    tempCat = rg[1];
                }

                var group = FindOrCreateTagGroup(tempTag, tempCat);

                result.Add(useGroup? group.Key : fullTag);
            }

            return new TagList(result).ToString();
        }

        public IEnumerable<TagGroup> GetTagTypes(string value)
        {
            return TagGroups.Where(t => t.Key.StartsWith(value + ":"));
        }

        public IReadOnlyList<TagGroup> CachedTagGroups()
        {
            return DanceStatsManager.GetInstance(this).TagGroups;
        }

        public IEnumerable<TagCount> GetTagSuggestions(Guid? user = null, char? targetType = null, string tagType = null, int count = int.MaxValue, bool normalized=false)
        {
            // from m in Modified where m.ApplicationUserId == user.Id && m.Song.TitleHash != 0 select m.Song;

            var userString = user?.ToString();

            IOrderedEnumerable<TagCount> ret;

            if (userString == null)
            {
                lock (s_sugMap)
                {
                    if (tagType == null) tagType = string.Empty;
                    if (s_sugMap.ContainsKey(tagType))
                    {
                        ret = s_sugMap[tagType];
                    }
                    else
                    {
                        var tts = (tagType == string.Empty) ? CachedTagGroups() : CachedTagGroups().Where(tt => tt.Category == tagType);
                        ret = TagGroup.ToTagCounts(tts).OrderByDescending(tc => tc.Count);
                        s_sugMap[tagType] = ret;
                    }
                }
            }
            else
            {
                var au = UserManager.FindById(user.ToString());

                if (!s_usMap.TryGetValue(au.UserName, out ret))
                {
                    var songs = FindUserSongs(au.UserName);
                    ret = BuildUserSuggestsions(songs);
                    s_usMap[au.UserName] = ret;
                }

                if (tagType != null) ret = ret.Where(t => t.TagClass == tagType).OrderByDescending(tc => tc.Count);
            }

            if (count < int.MaxValue)
            {
                ret = ret.Take(count).OrderByDescending(tc => tc.Count);
            }

            return ret;
        }

        // This assumes that the songdetails have been loaded with a specific current user info
        private static IOrderedEnumerable<TagCount> BuildUserSuggestsions(IEnumerable<Song> songs)
        {
            var dictionary = new Dictionary<string, int>();
            foreach (var song in songs)
            {
                int c;
                foreach (var dt in from dr in song.DanceRatings select dr into dri where dri?.CurrentUserTags != null from dt in dri.CurrentUserTags.Tags select dt)
                {
                    dictionary[dt] = dictionary.TryGetValue(dt, out c) ? c + 1 : 1;
                }

                if (song.CurrentUserTags == null) continue;

                foreach (var tag in song.CurrentUserTags.Tags)
                {
                    dictionary[tag] = dictionary.TryGetValue(tag, out c) ? c + 1 : 1;
                }
            }

            return dictionary.Select(pair => new TagCount(pair.Key, pair.Value)).OrderByDescending(tc => tc.Count);
        }

        public IEnumerable<TagGroup> OrderedTagGroups => DanceStatsManager.GetInstance(this).TagGroups;

        public ICollection<TagGroup> GetTagRings(TagList tags)
        {
            var tagCache = TagMap;
            var map = new Dictionary<string, TagGroup>();
            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var tag in tags.Tags)
            {
                TagGroup tt;
                if (!tagCache.TryGetValue(tag.ToLower(), out tt))
                    continue;

                tt = tt.GetPrimary();
                if (!map.ContainsKey(tt.Key))
                {
                    map.Add(tt.Key, tt);
                }
            }

            return map.Values;
        }

        private void AddTagType(TagGroup tagGroup)
        {
            if (TagGroups.Find(tagGroup.Key) != null) return;

            var newType = TagGroups.Create();
            newType.Key = tagGroup.Key;
            newType.PrimaryId = tagGroup.PrimaryId;
            newType.Count = tagGroup.Count;
            newType.Modified = DateTime.Now;

            TagGroups.Add(newType);
        }

        private TagGroup CreateTagGroup(string value, string category, bool updateTagManager = true) 
        {
            var type = TagGroups.Create();
            type.Key = TagGroup.BuildKey(value, category);

            var other = TagGroups.Find(type.Key);
            if (other != null)
            {
                // This will update case
                type = other;
            }
            else
            {
                type.Modified = DateTime.Now;
                type = TagGroups.Add(type);
                if (updateTagManager)
                    DanceStats.TagManager.AddTagGroup(type);
            }
            return type;
        }

        public static void BlowTagCache()
        {
            lock (s_sugMap)
            {
                s_sugMap.Clear();
                s_usMap.Clear();
            }
        }

        private static readonly Dictionary<string, IOrderedEnumerable<TagCount>> s_sugMap = new Dictionary<string, IOrderedEnumerable<TagCount>>();
        private static readonly Dictionary<string, IOrderedEnumerable<TagCount>> s_usMap = new Dictionary<string, IOrderedEnumerable<TagCount>>();

        #endregion

        #region Load

        private const string SongBreak = "+++++SONGS+++++";
        private const string TagBreak = "+++++TAGSS+++++";
        private const string SearchBreak = "+++++SEARCHES+++++";
        private const string DanceBreak = "+++++DANCES+++++";
        private const string PlaylistBreak = "+++++PLAYLISTS+++++";
        private const string UserHeader = "UserId\tUserName\tRoles\tPWHash\tSecStamp\tLockout\tProviders\tEmail\tEmailConfirmed\tStartDate\tRegion\tPrivacy\tCanContact\tServicePreference\tLastActive\tRowCount\tColumns\tSubscriptionLevel\tSubscriptionStart\tSubscriptionEnd";

        static public bool IsSongBreak(string line) {
            return IsBreak(line, SongBreak);
        }
        static public bool IsTagBreak(string line)
        {
            return IsBreak(line, TagBreak);
        }
        static public bool IsPlaylistBreak(string line)
        {
            return IsBreak(line, PlaylistBreak);
        }
        static public bool IsUserBreak(string line)
        {
            return UserHeader.StartsWith(line.Trim());
        }
        static public bool IsDanceBreak(string line)
        {
            return IsBreak(line, DanceBreak);
        }
        static public bool IsSearchBreak(string line)
        {
            return IsBreak(line, SearchBreak);
        }

        static private bool IsBreak(string line, string brk)
        {
            return string.Equals(line.Trim(), brk, StringComparison.InvariantCultureIgnoreCase);
        }

        public void LoadUsers(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Entering LoadUsers");

            if (lines == null || lines.Count < 1 || !IsUserBreak(lines[0]))
            {
                throw new ArgumentOutOfRangeException();
            }

            var fieldCount = lines[0].Split('\t').Length;
            var i = 1;
            while (i < lines.Count)
            {
                AdminMonitor.UpdateTask("LoadUsers",i-1);
                var s = lines[i];
                i += 1;

                if (string.Equals(s, TagBreak, StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }

                var cells = s.Split('\t');
                if (cells.Length != fieldCount) continue;

                var userId = cells[0];
                var userName = cells[1];
                var roles = cells[2];
                var hash = string.IsNullOrWhiteSpace(cells[3]) ? null : cells[3];
                var stamp = cells[4];
                var lockout = cells[5];
                var providers = cells[6];
                string email = null;
                var emailConfirmed = false;
                var date = new DateTime();
                string region = null;
                byte privacy = 0;
                var canContact = ContactStatus.None;
                string servicePreference = null;
                var active = new DateTime();
                int? rc = null;
                string col = null;
                SubscriptionLevel subscriptionLevel = SubscriptionLevel.None;
                DateTime? subscriptionStart = null;
                DateTime? subscriptionEnd = null;

                var extended = cells.Length >= 17;
                if (extended)
                {
                    email = cells[7];
                    bool.TryParse(cells[8], out emailConfirmed);
                    DateTime.TryParse(cells[9], out date);
                    region = cells[10];
                    byte.TryParse(cells[11], out privacy);
                    byte.TryParse(cells[12], out var canContactT);
                    canContact = (ContactStatus)canContactT;
                    servicePreference = cells[13];
                    DateTime.TryParse(cells[14], out active);
                    if (!string.IsNullOrWhiteSpace(cells[15]) && int.TryParse(cells[15], out var rcT))
                    {
                        rc = rcT;
                    }
                    if (!string.IsNullOrWhiteSpace(cells[16]))
                    {
                        col = cells[16];
                    }
                }

                if (cells.Length >= 20 && Enum.TryParse(cells[17], out subscriptionLevel) && subscriptionLevel != SubscriptionLevel.None)
                {
                    if (DateTime.TryParse(cells[18], out var start)) subscriptionStart = start;
                    if (DateTime.TryParse(cells[18], out var end)) subscriptionEnd = end;
                }

                var user = UserManager.FindById(userId);
                if (user == null)
                {
                    user = UserManager.FindByName(userName);
                }

                var create = user == null;

                if (create)
                {
                    user = _context.Users.Create();
                    user.Id = userId;
                    user.UserName = userName;
                    user.PasswordHash = hash;
                    user.SecurityStamp = stamp;
                    user.LockoutEnabled = string.Equals(lockout, "TRUE", StringComparison.InvariantCultureIgnoreCase);

                    if (extended)
                    {
                        user.StartDate = date;
                        user.Email = email;
                        user.EmailConfirmed = emailConfirmed;
                        user.Region = region;
                        user.Privacy = privacy;
                        user.CanContact = canContact;
                        user.ServicePreference = servicePreference;
                    }

                    Context.Users.Add(user);
                }
                else if (extended)
                {
                    if (string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(email))
                    {
                        user.Email = email;
                        user.EmailConfirmed = emailConfirmed;
                    }
                    if (string.IsNullOrWhiteSpace(user.Region) && !string.IsNullOrWhiteSpace(region))
                    {
                        user.Region = region;
                        user.Privacy = privacy;
                        user.CanContact = canContact;
                        user.ServicePreference = servicePreference;
                    }

                    user.Logins.Clear();
                    user.Roles.Clear();
                }

                user.LastActive = active;
                user.RowCountDefault = rc;
                user.ColumnDefaults = col;
                user.SubscriptionLevel = subscriptionLevel;
                user.SubscriptionStart = subscriptionStart;
                user.SubscriptionEnd = subscriptionEnd;

                if (!string.IsNullOrWhiteSpace(providers))
                {
                    var entries = providers.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    for (var j = 0; j < entries.Length; j += 2)
                    {
                        var login = new IdentityUserLogin { LoginProvider = entries[j], ProviderKey = entries[j + 1], UserId = userId };
                        user.Logins.Add(login);
                    }
                }

                if (!string.IsNullOrWhiteSpace(roles))
                {
                    var roleNames = roles.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var roleName in roleNames)
                    {
                        var role = Context.Roles.FirstOrDefault(r => r.Name == roleName.Trim());
                        if (role == null) continue;

                        var iur = new IdentityUserRole { UserId = user.Id, RoleId = role.Id };
                        user.Roles.Add(iur);
                    }
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Saving Changes");
            SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadUsers");
        }

        public void LoadSearches(IList<string> lines, bool reload=false)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadSearches");

            if (lines == null || lines.Count < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (lines.Count > 0 && IsSearchBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            if (lines.Count > 0)
            {
                if (reload)
                {
                    LoadSearchesBulk(lines);
                }
                else
                {
                    LoadSearchesIncremental(lines);
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
            SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadSearches");
        }

        private void LoadSearchesIncremental(IList<string> lines)
        {
            var fieldCount = lines[0].Split('\t').Length;
            for (var i = 0; i < lines.Count; i++)
            {
                AdminMonitor.UpdateTask("LoadSearches", i);
                var s = lines[i];

                if (string.Equals(s, TagBreak, StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }

                if (!ParseSearchEntry(s, fieldCount, out var user, out string name, out var query, 
                    out var favorite, out var count, out var created, out var modified))
                {
                    continue;
                }

                var search = user == null
                        ? Searches.FirstOrDefault(x => x.ApplicationUser == null && x.Query == query)
                        : Searches.FirstOrDefault(x =>
                            x.ApplicationUser != null && x.ApplicationUser.Id == user.Id && x.Query == query);

                if (search == null)
                {
                    search = Searches.Create();
                    Searches.Add(search);
                }

                search.Update(user, name, query, favorite, count, created, modified);
            }
        }

        private void LoadSearchesBulk(IList<string> lines)
        {
            try
            {
                Context.AutoDetectChangesEnabled = false;

                var fieldCount = lines[0].Split('\t').Length;
                for (var i = 0; i < lines.Count; i++)
                {
                    AdminMonitor.UpdateTask("LoadSearches", i);
                    var s = lines[i];

                    if (string.Equals(s, TagBreak, StringComparison.InvariantCultureIgnoreCase))
                    {
                        break;
                    }

                    if (!ParseSearchEntry(s, fieldCount, out var user, out string name, out var query,
                        out var favorite, out var count, out var created, out var modified))
                    {
                        continue;
                    }

                    var search = Searches.Create();
                    Searches.Add(search);

                    search.Update(user, name, query, favorite, count, created, modified);
                }
            }
            finally
            {
                Context.AutoDetectChangesEnabled = true;
            }
        }


        private bool ParseSearchEntry(
            string line, int fieldCount, out ApplicationUser user, out string name, out string query, 
            out bool favorite, out int count, out DateTime created, out DateTime modified)
        {
            user = null;
            name = null;
            query = null;
            favorite = false;
            count = 0;
            created = DateTime.MinValue;
            modified = DateTime.MaxValue;

            var cells = line.Split('\t');
            if (cells.Length != fieldCount) return false;

            var userName = cells[0];
            name = cells[1];
            query = cells[2];
            favorite = string.Equals(cells[3], "true", StringComparison.OrdinalIgnoreCase);
            int.TryParse(cells[4], out count);
            DateTime.TryParse(cells[5], out created);
            DateTime.TryParse(cells[6], out modified);

            user = string.IsNullOrWhiteSpace(userName) ? null : FindUser(userName);

            return true;
        }

        public void LoadDances(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering Dances");

            if (lines.Count > 0 && IsDanceBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            AdminMonitor.UpdateTask("LoadDances");
            LoadDances();
            var modified = false;
            for (var index = 0; index < lines.Count; index++)
            {
                var s = lines[index];
                AdminMonitor.UpdateTask("LoadDances", index+1);
                if (string.IsNullOrWhiteSpace(s))
                    continue;
                var cells = s.Split('\t').ToList();
                var d = Dances.Find(cells[0]);
                if (d == null)
                {
                    d = Dances.Create();
                    d.Id = cells[0];
                    Dances.Add(d);
                    modified = true;
                }

                cells.RemoveAt(0);
                modified |= d.Update(cells);
            }

            if (modified)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
                SaveChanges();
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting Dances");
        }

        public void LoadTags(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadTags");

            for (var index = 0; index < lines.Count; index++)
            {
                var s = lines[index];
                AdminMonitor.UpdateTask("LoadTags", index + 1);

                var cells = s.Split('\t');
                TagGroup tt = null;
                if (cells.Length >= 2)
                {
                    var category = cells[0];
                    var value = cells[1];
                    var key = TagGroup.BuildKey(value, category);

                    var ttOld = TagGroups.Find(key) ?? TagMap.GetValueOrDefault(key);

                    if (ttOld != null)
                    {
                        if (ttOld.Key != key)
                        {
                            TagGroups.Remove(ttOld);
                            ttOld = null;
                        }
                    }

                    if (ttOld == null)
                    {
                        tt = CreateTagGroup(value, category, false);
                    }
                }

                if (tt != null && cells.Length >= 3 && !string.IsNullOrWhiteSpace(cells[2]))
                {
                    tt.PrimaryId = cells[2];
                }

                if (tt != null)
                {
                    DateTime modified;
                    if (cells.Length >= 4 &&
                        !string.IsNullOrWhiteSpace(cells[3]) &&
                        DateTime.TryParse(cells[3], out modified))
                    {
                        tt.Modified = modified;
                    }
                    else
                    {
                        tt.Modified = DateTime.MinValue;
                    }
                }
            }

            foreach (var tt in TagGroups)
            {
                tt.Children = null;
            }

            foreach (var tt in TagGroups)
            {
                if (string.IsNullOrEmpty(tt.PrimaryId))
                {
                    tt.Primary = null;
                }
                else
                {
                    tt.Primary = TagGroups.Find(tt.PrimaryId);
                    if (tt.Primary.Children == null)
                        tt.Primary.AddChild(tt);
                }
            }
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
            SaveChanges();

            BlowTagCache();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadTags");
        }

        public void LoadPlaylists(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadPlaylists");
            var now = DateTime.Now;

            for (var index = 0; index < lines.Count; index++)
            {
                var s = lines[index].Trim();
                AdminMonitor.UpdateTask("LoadPlaylists", index + 1);

                if (string.Equals(PlaylistBreak, s)) continue;

                var cells = s.Split('\t');

                var created = now;
                DateTime? modified = null;
                var deleted = false;
                string data2 = null;
                string data1;
                string id;
                string name = null;
                string description = null;

                var type = PlayListType.SongsFromSpotify;

                if (cells.Length < 3) continue;

                // m4dId
                var userId = cells[0];


                // This is a special case for SongFromSpotify [m4did,DanceTags,url]
                if (cells.Length == 3 && type == PlayListType.SongsFromSpotify)
                {
                    var r = new Regex(@"https://open.spotify.com/user/(?<user>[a-z0-9]*)/playlist/(?<id>[a-z0-9]*)",
                        RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
                    var m = r.Match(cells[2]);
                    if (!m.Success) continue;

                    id = m.Groups["id"].Value;
                    var email = m.Groups["user"].Value;

                    FindOrAddUser(userId, PseudoRole, email + "@spotify.com");
                    data1 = cells[1];
                }
                else
                {
                    if (cells.Length < 4) continue;

                    // Type
                    // TODO: Once this is published to the server, we can get rid of this check for legacy type "Spotify"
                    if (!string.Equals(cells[1], "Spotify"))
                    {
                        Enum.TryParse(cells[1], out type);
                    }

                    // Dance/tags
                    data1 = cells[2];

                    // Spotify Playlist Id
                    id = cells[3];

                    if (cells.Length > 4) DateTime.TryParse(cells[4], out created);
                    if (cells.Length > 5 && DateTime.TryParse(cells[5], out var mod)) modified = mod;
                    if (cells.Length > 6) bool.TryParse(cells[6], out deleted);
                    if (cells.Length > 7) data2 = cells[7];
                    if (cells.Length > 8) name = cells[8];
                    if (cells.Length > 9) description = cells[8];
                }

                var playlist = PlayLists.Find(id);
                var isNew = playlist == null;
                if (isNew) playlist = new PlayList();

                playlist.Id = id;
                playlist.Type = type;
                playlist.Data1 = data1;
                playlist.User = userId;
                playlist.Created = created;
                playlist.Updated = modified;
                playlist.Deleted = deleted;
                playlist.Data2 = data2;
                playlist.Name = name;
                playlist.Description = description;

                if (isNew) PlayLists.Add(playlist);
            }
            SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadPlaylists");
        }

        public void LoadSongs(IList<string> lines)
        {
            // Load the dance List
            LoadDances();

            var c = 0;
            foreach (var line in lines)
            {
                AdminMonitor.UpdateTask("LoadSongs",c);
                var time = DateTime.Now;
                var song = new Song {Created = time, Modified = time};

                song.Load(line, DanceStats);
                
                c += 1;

                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving next 100 songs");
                }
            }
        }

        public void UpdateSongs(IList<string> lines, bool clearCache=true)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering UpdateSongs");

            // Load the dance List
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Dances");
            LoadDances();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Songs");

            if (lines.Count > 0 && IsSongBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            var danceStats = DanceStats;
            var c = 0;
            foreach (var line in lines.Where(line => !line.StartsWith("//")))
            {
                AdminMonitor.UpdateTask("UpdateSongs", c);

                var sd = new Song(line,danceStats);
                var song = FindSong(sd.SongId);

                if (song == null)
                {
                    var up = sd.FirstProperty(Song.UserField);
                    var user = FindOrAddUser(up != null ? up.Value : "batch", EditRole);

                    song = CreateSong(sd.SongId);
                    UpdateSong(user, song, sd);

                    // This was a merge so delete the input songs
                    if (sd.SongProperties.Count > 0 && sd.SongProperties[0].Name == Song.MergeCommand)
                    {
                        var list = SongsFromList(sd.SongProperties[0].Value);
                        foreach (var s in list)
                        {
                            DeleteSong(user, s);
                        }
                    }
                }
                else
                {
                    var up = sd.LastProperty(Song.UserField);
                    var user = FindOrAddUser(up != null ? up.Value : "batch", EditRole);
                    if (sd.IsNull)
                    {
                        DeleteSong(user, song);
                    }
                    else
                    {
                        UpdateSong(user, song, sd);
                    }
                }

                c += 1;
                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{c} songs updated");
                }
            }

            if (clearCache)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Clearing Song Cache");
                DanceStatsManager.ClearCache();
            }
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting UpdateSongs");
        }

        public void AdminUpdate(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering AdminUpdate");

            // Load the dance List
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Dances");
            LoadDances();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Loading Songs");

            if (lines.Count > 0 && IsSongBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            var c = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("//"))
                    continue;

                AdminMonitor.UpdateTask("UpdateSongs", c);

                AdminEditSong(line);

                c += 1;
                if (c % 100 == 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"{c} songs updated");
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Clearing Song Cache");
            DanceStatsManager.ClearCache();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting AdminUpdate");
        }

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private static Guid s_guidError = new Guid("25053e8c-5f1e-441e-bd54-afdab5b1b638");



        public void SeedDances()
        {
            var dances = DanceLibrary.Dances.Instance;
            foreach (var dance in from d in dances.AllDanceGroups let dance = _context.Dances.Find(d.Id) where dance == null select new Dance { Id = d.Id })
            {
                _context.Dances.Add(dance);
            }
            foreach (var dance in from d in dances.AllDanceTypes let dance = _context.Dances.Find(d.Id) where dance == null select new Dance { Id = d.Id })
            {
                _context.Dances.Add(dance);
            }
        }

        private void LoadDances()
        {
            Context.LoadDances();
        }

        #endregion

        #region Save
        public IList<string> SerializeUsers(bool withHeader=true, DateTime? from = null)
        {
            var users = new List<string>();

            if (!from.HasValue) from = new DateTime(1,1,1);
            
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var user in UserManager.Users.Where(u => u.LastActive >= from.Value).OrderByDescending(u => u.LastActive > u.StartDate ? u.LastActive : u.StartDate).ToList())
            {
                var userId = user.Id;
                var username = user.UserName;
                var roles = string.Join("|", UserManager.GetRoles(user.Id));
                var hash = user.PasswordHash;
                var stamp = user.SecurityStamp;
                var lockout = user.LockoutEnabled.ToString();
                var providers = string.Join("|", UserManager.GetLogins(user.Id).Select(l => l.LoginProvider + "|" + l.ProviderKey));
                var email = user.Email;
                var emailConfirmed = user.EmailConfirmed;
                var time = user.StartDate.ToString("g");
                var region = user.Region;
                var privacy = user.Privacy.ToString();
                var canContact = ((byte) user.CanContact).ToString();
                var servicePreference = user.ServicePreference;
                var lastActive = user.LastActive.ToString("g");
                var rc = user.RowCountDefault;
                var col = user.ColumnDefaults;
                var sl = user.SubscriptionLevel;
                var ss = user.SubscriptionStart;
                var se = user.SubscriptionEnd;

                users.Add(
                    $"{userId}\t{username}\t{roles}\t{hash}\t{stamp}\t{lockout}\t{providers}\t{email}\t{emailConfirmed}\t{time}\t{region}\t{privacy}\t{canContact}\t{servicePreference}\t{lastActive}\t{rc}\t{col}\t{sl}\t{ss}\t{se}");
            }

            if (withHeader && users.Count > 0)
            {
                users.Insert(0,UserHeader);
            }

            return users;
        }

        public IList<string> SerializeTags(bool withHeader = true, DateTime? from = null)
        {
            var tags = new List<string>();

            if (!from.HasValue) from = new DateTime(1, 1, 1);

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var tt in TagGroups.Where(t => t.Modified >= from.Value).OrderBy(t => t.Modified))
            {
                tags.Add($"{tt.Category}\t{tt.Value}\t{tt.PrimaryId}\t{tt.Modified.ToString("g")}");
            }

            if (withHeader && tags.Count > 0)
            {
                tags.Insert(0,TagBreak);
            }

            return tags;
        }

        public IList<string> SerializeSearches(bool withHeader = true, DateTime? from = null)
        {
            var searches = new List<string>();

            if (!from.HasValue) from = new DateTime(1, 1, 1);

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var search in Searches.Include(s => s.ApplicationUser).Where(s => s.Modified >= from.Value).OrderBy(s => s.Modified))
            {
                var userName = (search.ApplicationUser != null) ? search.ApplicationUser.UserName : string.Empty;
                searches.Add($"{userName}\t{search.Name}\t{search.Query}\t{search.Favorite}\t{search.Count}\t{search.Created.ToString("g")}\t{search.Modified.ToString("g")}");
            }

            if (withHeader && searches.Count > 0)
            {
                searches.Insert(0,SearchBreak);
            }

            return searches;
        }

        public IList<string> SerializeSongs(bool withHeader = true, bool withHistory = true, int max = -1, DateTime? from = null, SongFilter filter = null, HashSet<Guid> exclusions = null)
        {
            var songs = new List<string>();

            if (withHeader)
            {
                songs.Add(SongBreak);
            }

            songs.AddRange(BackupIndex("default", max, from, filter));

            return songs;
        }

        [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
        public IList<string> SerializePlaylists(bool withHeader = true, DateTime? from = null)
        {
            if (!from.HasValue) from = new DateTime(1, 1, 1);

            var playlists = PlayLists.Where(d => (d.Updated.HasValue && d.Updated >= from.Value) || d.Created >= from.Value).OrderBy(d => d.Updated).ThenBy(d => d.Created);

            var lines = new List<string>();
            foreach (var p in playlists)
            {
                lines.Add($"{p.User}\t{p.Type}\t{p.Data1}\t{p.Id}\t{p.Created}\t{p.Updated}\t{p.Deleted}\t{p.Data2}\t{p.Name}\t{p.Description}");
            }

            if (withHeader && lines.Count > 0)
            {
                lines.Insert(0, PlaylistBreak);
            }
            return lines;
        }

        public IList<string> SerializeDances(bool withHeader = true, DateTime? from = null)
        {
            var dances = new List<string>();

            if (!from.HasValue) from = new DateTime(1, 1, 1);

            var dancelist = Dances.Where(d => d.Modified >= from.Value).OrderBy(d => d.Modified).ToList();
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var dance in dancelist)
            {
                var line = dance.Serialize();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    dances.Add(line);
                }
            }

            if (withHeader && dances.Count > 0)
            {
                dances.Insert(0,DanceBreak);
            }

            return dances;
        }

        #endregion

        #region Search

        public bool ResetIndex(string id = "default")
        {
            var info = SearchServiceInfo.GetInfo(id);
            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            {
                if (serviceClient.Indexes.Exists(info.Index))
                {
                    serviceClient.Indexes.Delete(info.Index);
                }

                var index = Song.GetIndex(this);
                index.Name = info.Index;

                serviceClient.Indexes.Create(index);
            }

            return true;
        }

        public bool UpdateIndex(IEnumerable<string> dances, string id = "default")
        {
            var info = SearchServiceInfo.GetInfo(id);
            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            {
                var index = serviceClient.Indexes.Get(info.Index);
                foreach (var dance in dances)
                {
                    var field = Song.IndexFieldFromDanceId(dance);
                    if (index.Fields.All(f => f.Name != field.Name))
                    {
                        index.Fields.Add(field);
                    }
                }
                serviceClient.Indexes.CreateOrUpdate(index);
            }
            return true;
        }

            public void CloneIndex(string to, string from = "default")
        {
            AdminMonitor.UpdateTask("StartBackup");
            var lines = BackupIndex(from) as IList<string>;
            AdminMonitor.UpdateTask("StartReset");
            ResetIndex(to);
            AdminMonitor.UpdateTask("StartUpload");
            UploadIndex(lines, false, to);
        }

        public int UploadIndex(IList<string> lines, bool trackDeleted, string id = "default")
        {
            const int chunkSize = 500;
            var info = SearchServiceInfo.GetInfo(id);
            var page = 0;
            var stats = DanceStats;
            var added = 0;

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                var delete = new List<string>();

                for (var i = 0; i < lines.Count; page += 1)
                {
                    AdminMonitor.UpdateTask("AddSongs", added);
                    var chunk = new List<Song>();
                    for (; i < lines.Count && i < (page+1)*chunkSize; i++)
                    {
                        var song = new Song(lines[i], stats);
                        chunk.Add(song);
                        if (trackDeleted)
                        {
                            delete.AddRange(song.GetAltids());
                        }
                    }

                    var songs = (from song in chunk where !song.IsNull select song.GetIndexDocument()).ToList();

                    if (songs.Count <= 0) continue;

                    try
                    {
                        var batch = IndexBatch.MergeOrUpload(songs);
                        var results = indexClient.Documents.Index(batch);
                        added += results.Results.Count;
                    }
                    catch (IndexBatchException ex)
                    {
                        Trace.WriteLine($"IndexBatchException: ex.Message");
                        added += ex.IndexingResults.Count;
                    }
                    Trace.WriteLine($"Upload Index: {added} songs added.");
                }

                if (delete.Count <= 0) return added;

                var docs = IndexBatch.Delete(delete.Select(d => new Document {[Song.SongIdField] = d}));
                indexClient.Documents.Index(docs);
                return added;
            }
        }

        public int UpdateAzureIndex(string id = "default")
        {
            return UpdateAzureIndex(DanceStats.DequeueSongs(), id);
        }

        public int UpdateAzureIndex(IEnumerable<Song> songs, string id = "default")
        {
            var info = SearchServiceInfo.GetInfo(id);

            var changed = false;
            var tags = DanceStats.TagManager.DequeueTagGroups();
            foreach (var tag in tags)
            {
                AddTagType(tag);
                changed = true;
            }
            if (changed)
            {
                SaveChanges();
                // TODO: Consider doing a lighter version of this
                BlowTagCache();
            }
            if (songs == null) return 0;

            try
            {
                using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
                using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
                {
                    var processed = 0;
                    var list = (songs as List<Song>) ?? songs.ToList();

                    while (list.Count > 0)
                    {
                        var deleted = new List<Document>();
                        var added = new List<Document>();

                        // ReSharper disable once LoopCanBeConvertedToQuery
                        foreach (var song in list)
                        {
                            if (!song.IsNull)
                            {
                                added.Add(song.GetIndexDocument());
                            }
                            else
                            {
                                deleted.Add(song.GetIndexDocument());
                            }
                            if (added.Count > 990 || deleted.Count > 990)
                            {
                                break;
                            }
                        }

                        if (added.Count > 0)
                        {
                            var batch = IndexBatch.Upload(added);
                            indexClient.Documents.Index(batch);
                        }
                        if (deleted.Count > 0)
                        {
                            var delete = IndexBatch.Delete(deleted);
                            indexClient.Documents.Index(delete);
                        }

                        list.RemoveRange(0, added.Count + deleted.Count);
                        processed += added.Count + deleted.Count;
                    }

                    return processed;
                }

            }
            catch (Exception e)
            {
                Trace.WriteLine($"UpdateAzureIndex Failed: {e.Message}");
                return 0;
            }
        }

        public SearchResults AzureSearch(SongFilter filter, int? pageSize = null, CruftFilter cruft = CruftFilter.NoCruft, string id = "default")
        {
            if (filter.CruftFilter != CruftFilter.NoCruft)
            {
                cruft = filter.CruftFilter;
            }
            return AzureSearch(filter.SearchString, AzureParmsFromFilter(filter, pageSize), cruft, id, DanceStats);
        }

        public SearchResults AzureSearch(string search, SearchParameters parameters, CruftFilter cruft = CruftFilter.NoCruft, string id = "default", DanceStatsInstance stats = null)
        {
            var response = DoAzureSearch(search,parameters,cruft,id);
            var songs = response.Results.Select(d => new Song(d.Document,stats??DanceStats)).ToList();
            var pageSize = parameters.Top ?? 25;
            var page = ((parameters.Skip ?? 0)/pageSize) + 1;
            var facets = response.Facets;
            return new SearchResults(search, songs.Count,response.Count ?? -1,page, pageSize, songs, facets);
        }

        public FacetResults GetTagFacets(string categories, int count, string id = "default")
        {
            var parameters = AzureParmsFromFilter(new SongFilter(), 1);
            AddAzureCategories(parameters,categories,count);

            return DoAzureSearch(null, parameters, CruftFilter.NoCruft, id).Facets;
        }

        public static void AddAzureCategories(SearchParameters parameters, string categories, int count)
        {
            parameters.Facets = categories.Split(',').Select(c => $"{c},count:{count}").ToList();
        }

        public IEnumerable<Song> FindAlbum(string name, CruftFilter cruft = CruftFilter.NoCruft, string id = "default")
        {
            return SongsFromAzureResult(DoAzureSearch($"\"{name}\"",new SearchParameters {SearchFields=new [] {Song.AlbumsField}},cruft, id));
        }

        public IEnumerable<Song> FindArtist(string name, CruftFilter cruft = CruftFilter.NoCruft, string id = "default")
        {
            return SongsFromAzureResult(DoAzureSearch($"\"{name}\"", new SearchParameters { SearchFields = new[] { Song.ArtistField } }, cruft, id));
        }

        public IEnumerable<Song> TakeTail(SongFilter filter, int max, DateTime? from = null, CruftFilter cruft = CruftFilter.NoCruft, string id = "default")
        {
            var parameters = AddCruftInfo(AzureParmsFromFilter(filter), cruft);

            return TakeTail(parameters,max,from,id);
        }

        public IEnumerable<Song> TakePage(SearchParameters parameters, int pageSize, ref SearchContinuationToken token, string id = "default")
        {
            parameters.IncludeTotalResultCount = false;
            parameters.Top = null;

            var info = SearchServiceInfo.GetInfo(id);
            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                var results = new List<Song>();
                do
                {
                    var response = (token == null)
                        ? indexClient.Documents.Search(null, parameters)
                        : indexClient.Documents.ContinueSearch(token);

                    foreach (var doc in response.Results)
                    {
                        results.Add(new Song(doc.Document, DanceStats));

                        if (results.Count >= pageSize) break;
                    }
                    token = response.ContinuationToken;
                } while (token != null && results.Count < pageSize);

                return results;
            }
        }

        public IEnumerable<Song> TakeTail(SearchParameters parameters, int max, DateTime? from = null, string id = "default")
        {
            return TakeTail(null, parameters, max, from, id);
        }

        public IEnumerable<Song> TakeTail(string search, SearchParameters parameters, int max, DateTime? from = null, string id = "default")
        {
            parameters.OrderBy = new[] { "Modified desc" };
            parameters.IncludeTotalResultCount = false;
            parameters.Top = null;

            var info = SearchServiceInfo.GetInfo(id);
            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                SearchContinuationToken token = null;
                var results = new List<Song>();
                do
                {
                    var response = (token == null)
                        ? indexClient.Documents.Search(search, parameters)
                        : indexClient.Documents.ContinueSearch(token);

                    token = response.ContinuationToken;
                    foreach (var doc in response.Results)
                    {
                        var m = doc.Document["Modified"];
                        var modified = (DateTimeOffset)m;
                        if (from != null && modified < from)
                        {
                            token = null;
                            break;
                        }

                        results.Add(new Song(doc.Document,DanceStats));

                        if (results.Count >= max) break;
                    }
                } while (token != null && results.Count < max);

                return results;
            }
        }

        //public IEnumerable<Song> SongsFromIds(IEnumerable<Guid> ids, string user = null, string id = "default")
        //{
        //    var parameters = new SearchParameters
        //    {
        //        QueryType = QueryType.Simple,
        //        Top = 1000,
        //        Select = new[] { Song.SongIdField,  Song.PropertiesField }
        //    };

        //    var info = SearchServiceInfo.GetInfo(id);
        //    using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
        //    using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
        //    {
        //        var response = indexClient.Documents.Search(null, parameters);

        //        return response.Results.Select(doc => new Song(doc.Document, DanceStats));
        //    }
        //}

        public IEnumerable<Song> LoadLightSongs(string id = "default")
        {
            var parameters = new SearchParameters
            {
                QueryType = QueryType.Simple,
                Top = int.MaxValue,
                Select = new[] { Song.SongIdField, Song.TitleField, Song.ArtistField, Song.LengthField, Song.TempoField }
            };

            var info = SearchServiceInfo.GetInfo(id);
            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                SearchContinuationToken token = null;

                var results = new List<Song>();
                do
                {
                    var response = (token == null)
                        ? indexClient.Documents.Search(null, parameters)
                        : indexClient.Documents.ContinueSearch(token);

                    // ReSharper disable once LoopCanBeConvertedToQuery
                    foreach (var res in response.Results)
                    {
                        var doc = res.Document;
                        var title = doc[Song.TitleField] as string;
                        if (string.IsNullOrEmpty(title)) continue;

                        var sid = doc[Song.SongIdField] as string;
                        var lobj = doc[Song.LengthField];
                        var length = (long?) lobj;
                        var tobj = doc[Song.TempoField];
                        var tempo = (double?) tobj;

                        results.Add(new Song {
                            SongId = sid == null ? new Guid() : new Guid(sid),
                            Title = title,
                            Artist = doc[Song.ArtistField] as string,
                            Length = (int?)length,
                            Tempo = (decimal?) tempo
                        });
                    }
                    token = response.ContinuationToken;
                } while (token != null);

                return results;
            }
        }

        private static DocumentSearchResult<Document> DoAzureSearch(string search, SearchParameters parameters, CruftFilter cruft = CruftFilter.NoCruft, ISearchIndexClient client = null)
        {
            if (client == null)
                return DoAzureSearch(search, parameters, cruft,"default");

            parameters = AddCruftInfo(parameters, cruft);
            if (string.IsNullOrWhiteSpace(search))
            {
                search = "*";
            }

            return client.Documents.Search(search, parameters);
        }

        private static DocumentSearchResult<Document> DoAzureSearch(string search, SearchParameters parameters, CruftFilter cruft = CruftFilter.NoCruft, string id = "default")
        {
            var info = SearchServiceInfo.GetInfo(id);

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                return DoAzureSearch(search, parameters, cruft, indexClient);
            }
        }

        public SearchParameters AzureParmsFromFilter(SongFilter filter, int? pageSize = null)
        {
            if (!pageSize.HasValue) pageSize = 25;

            if (filter.IsRaw)
            {
                return new RawSearch(filter).GetAzureSearchParams(pageSize);
            }
            var order = filter.ODataSort;
            var odataFilter = filter.GetOdataFilter(this);

            return new SearchParameters
            {
                QueryType = QueryType.Simple, // filter.IsSimple ? QueryType.Simple : QueryType.Full,
                Filter = odataFilter,
                IncludeTotalResultCount = true,
                Top = pageSize,
                Skip = ((filter.Page??1) - 1) * pageSize,
                OrderBy = order
            };
        }

        public static SearchParameters AddCruftInfo(SearchParameters parameters, CruftFilter cruft)
        {
            if (cruft == CruftFilter.AllCruft) return parameters;

            var extra = new StringBuilder();
            if ((cruft & CruftFilter.NoPublishers) != CruftFilter.NoPublishers)
            {
                extra.Append("Purchase/any()");
            }

            if ((cruft & CruftFilter.NoDances) != CruftFilter.NoDances)
            {
                if (extra.Length > 0) extra.Append(" and ");
                extra.Append("DanceTags/any()");
            }

            if (parameters.Filter == null)
            {
                parameters.Filter = extra.ToString();
            }
            else
            {
                extra.AppendFormat(" and {0}", parameters.Filter);
                parameters.Filter = extra.ToString();
            }
            return parameters;
        }

        public SuggestionList AzureSuggestions(string query, string id = "default")
        {
            var info = SearchServiceInfo.GetInfo(id);

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                var sp = new SuggestParameters {Top=50};
          
                var response = indexClient.Documents.Suggest(query, "songs", sp);

                var comp = new SuggestionComparer();
                //var ret = new SuggestionList {Query = "query", Suggestions = new List<Suggestion>()};
                var ret = response.Results.Select(result => new Suggestion
                {
                    Value = result.Text,
                    Data = result.Document["SongId"] as string
                }).Distinct(comp).Take(10).ToList();

               return new SuggestionList
                {
                    Query = query,
                    Suggestions = ret
                };
            }
        }

        public IEnumerable<string> BackupIndex(string name = "default", int count = -1, DateTime? from = null, SongFilter filter = null)
        {
            var info = SearchServiceInfo.GetInfo(name);
            if (filter == null) filter = SongFilter.AzureSimple;

            var parameters = AzureParmsFromFilter(filter);
            parameters.IncludeTotalResultCount = false;
            parameters.Skip = null;
            parameters.Top = (count == -1) ? (int?) null : count;
            parameters.OrderBy = new [] {"Modified desc"};
            parameters.Select = new[] {Song.SongIdField, Song.ModifiedField, Song.PropertiesField};

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                SearchContinuationToken token = null;
                var searchString = string.IsNullOrWhiteSpace(filter.SearchString) ? null : filter.SearchString;
                var results = new List<string>();
                do
                {
                    var response = (token == null)
                        ? indexClient.Documents.Search(searchString, parameters)
                        : indexClient.Documents.ContinueSearch(token);

                    token = response.ContinuationToken;
                    foreach (var doc in response.Results)
                    {
                        var m = doc.Document["Modified"];
                        var modified = (DateTimeOffset)m;
                        if (from != null && modified < from)
                        {
                            token = null;
                            break;
                        }

                        results.Add(Song.Serialize(doc.Document[Song.SongIdField] as string, doc.Document[Song.PropertiesField] as string));
                        AdminMonitor.UpdateTask("readSongs", results.Count);
                    }
                } while (token != null);

                return results;
            }
        }

        #endregion

        #region User
        public ApplicationUser FindUser(string name)
        {
            if (_userCache.TryGetValue(name,out var user))
                return user;

            user = UserManager.FindByName(name);
            if (user != null)
                _userCache[name] = user;

            return user;
        }
        public ApplicationUser FindOrAddUser(string name, string role, string email = null)
        {
            var user = FindUser(name);

            if (user == null)
            {
                if (email == null)
                {
                    email = name + "@music4dance.net";
                }
                user = new ApplicationUser { UserName = name, Email = email, EmailConfirmed = true, StartDate = DateTime.Now };
                var res = UserManager.Create(user, "_This_Is_@_placeh0lder_");
                if (res.Succeeded)
                {
                    var user2 = FindUser(name);
                    Trace.WriteLine($"{user2.UserName}:{user2.Id}");
                }

            }

            if (string.Equals(role, PseudoRole))
            {
                user.LockoutEnabled = true;
            }
            else
            {
                AddRole(user.Id, role);
            }
            return user;
        }

        public void ChangeUserName(string oldUserName, string userName)
        {
            var songs = FindUserSongs(oldUserName, includeHate:true).ToList();

            foreach (var song in songs)
            {
                var props = new List<SongProperty>(song.SongProperties);
                foreach (var prop in props.Where(p =>
                    (p.Name == Song.UserField || p.Name == Song.UserProxy) && p.Value == oldUserName))
                {
                    prop.Value = userName;
                }

                song.AdminEdit(props,DanceStats);
            }

            SaveSongs(songs);
        }

        private void AddRole(string id, string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return;

            var key = id + ":" + role;
            if (_roleCache.Contains(key))
                return;

            if (!UserManager.IsInRole(id, role))
            {
                UserManager.AddToRole(id, role);
            }

            _roleCache.Add(key);
        }

        private readonly Dictionary<string, ApplicationUser> _userCache = new Dictionary<string, ApplicationUser>();
        private readonly HashSet<string> _roleCache = new HashSet<string>();

        #endregion

        #region Merging
        public IList<Song> FindMergeCandidates(int n, int level)
        {
            return MergeCluster.GetMergeCandidates(this, n, level);
        }

        public void RemoveMergeCandidates(IEnumerable<Song> songs)
        {
            foreach (var song in songs)
            {
                MergeCluster.RemoveMergeCandidate(song);
            }
        }

        public void ClearMergeCandidates()
        {
            MergeCluster.ClearMergeCandidateCache();
        }

        public void UpdatePlayList(string id, IEnumerable<Song> songs)
        {
            var playlist = PlayLists.Find(id);
            if (playlist == null || playlist.Type != PlayListType.SongsFromSpotify) throw new ArgumentOutOfRangeException(nameof(id));


            var service = MusicService.GetService(ServiceType.Spotify);
            if (service == null) throw new ArgumentOutOfRangeException(nameof(id));

            playlist.AddSongs(songs.Select(s => s.GetPurchaseId(service.Id)));
            playlist.Updated = DateTime.Now;

            SaveChanges();
        }
        #endregion

    }
}
