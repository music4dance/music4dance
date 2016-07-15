using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

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
            temp.Dispose();
        }

        public bool IsDisposed => _context == null;

        #endregion

        #region Properties

        public IDanceMusicContext Context => _context;
        public DbSet<Dance> Dances => _context.Dances;
        public DbSet<TagType> TagTypes => _context.TagTypes;
        public DbSet<Search> Searches => _context.Searches;

        public UserManager<ApplicationUser> UserManager { get; }

        public void UpdateAndEnqueue(IEnumerable<Song> songs = null)
        {
            SaveChanges(songs);
            IndexUpdater.Enqueue();
        }

        public int SaveChanges(IEnumerable<Song> songs = null)
        {
            var ret =  _context.SaveChanges();
            if (ret == 0 || songs == null) return ret;

            UpdateTopSongs(songs);
            return ret;
        }

        public void UpdateTopSongs(IEnumerable<Song> songs = null)
        {
            if (songs == null) return;

            foreach (var song in songs)
            {
                DanceStats.UpdateSong(song, this);
            }
        }

        public static readonly string EditRole = "canEdit";
        public static readonly string TagRole = "canTag";
        public static readonly string DiagRole = "showDiagnostics";
        public static readonly string DbaRole = "dbAdmin";
        public static readonly string PseudoRole = "pseudoUser";

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

            return song;
        }

        public Song EditSong(ApplicationUser user, Song edit, IEnumerable<UserTag> tags = null)
        {
            var song = FindSong(edit.SongId);

            return !song.Edit(user.UserName, edit, tags, DanceStats) ? null : edit;
        }

        public bool AdminEditSong(Song edit, string properties)
        {
            return edit.AdminEdit(properties,DanceStats);
        }

        public bool AdminEditSong(string properties)
        {
            Guid id;
            if (Song.TryParseId(properties, out id) == 0) return false;

            var song = FindSong(id);
            return song != null && AdminEditSong(song, properties);
        }

        public Song UpdateSong(ApplicationUser user, Song song, Song edit)
        {
            return !song.Update(user.UserName, edit, DanceStats) ? null : song;
        }

        // This is an additive merge - only add new things if they don't conflict with the old
        //  TODO: I'm pretty sure I can clean up this and all the other editing stuff by pushing
        //  the diffing part down into Song (which will also let me unit test it more easily)
        public bool AdditiveMerge(ApplicationUser user, Guid songId, Song edit, List<string> addDances)
        {
            return FindSong(songId).AdditiveMerge(user.UserName, edit, addDances, DanceStats);
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

            UpdateAndEnqueue(new[] { song });
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

            UpdateAndEnqueue(new [] {song});
            return true;
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
            var songIds = string.Join(";", songs.Select(s => s.SongId.ToString()));

            var song = CreateSong(user, null,null, Song.MergeCommand, songIds);

            // Add in the properties for all of the songs and then delete them
            foreach (var from in songs)
            {
                DeleteSong(user, from);
            }

            var sd = new Song(title, artist, tempo, length, albums)
            {
                Danceability = song.Danceability,
                Energy = song.Energy,
                Valence = song.Valence,
                Sample = song.Sample
            };

            song.Edit(user.UserName, sd, null, DanceStats);

            return song;
        }

        public Song MergeSongs(ApplicationUser user, List<Song> songs, string title, string artist, decimal? tempo, int? length, string defAlbums, HashSet<string> keys)
        {
            return MergeSongs(user,songs,title,artist,tempo,length,MergeAlbums(songs,defAlbums,keys,artist));
        }

        public void DeleteSong(ApplicationUser user, Song song)
        {
            song.Delete(user.UserName);
            UpdateAndEnqueue(new [] {song});
        }

        private void RemoveSong(Song song, ApplicationUser user)
        {
            song.Delete(user.UserName);
            UpdateAndEnqueue(new [] {song});
        }

        public BatchInfo CleanupProperties(int max, DateTime from, SongFilter filter)
        {
            return null;

            // DBKILL - Need to get batch/tail of azure songs implemented
            //var ret = new BatchInfo();

            //if (filter == null) filter = new SongFilter();
            //var songlist = TakeTail(BuildSongList(filter,CruftFilter.AllCruft), from, max);

            //var lastTouched = DateTime.MinValue;
            //var succeeded = new List<Song>();
            //var failed = new List<Song>();
            //var cummulative = 0;

            //foreach (var song in songlist)
            //{
            //    lastTouched = song.Modified;

            //    var init = song.SongProperties.Count;

            //    var changed = song.CleanupProperties();

            //    var final = song.SongProperties.Count;

            //    if (changed)
            //    {
            //        Trace.WriteLine($"Succeeded ({init-final})): {song}");
            //        succeeded.Add(song);
            //        cummulative += init - final;
            //    }
            //    else
            //    {
            //        if (init - final != 0)
            //        {
            //            Trace.WriteLine($"Failed ({init - final}): {song}");
            //        }
            //        Trace.WriteLine($"Skipped: {song}");
            //        failed.Add(song);
            //    }

            //    AdminMonitor.UpdateTask("CleanProperty", succeeded.Count + failed.Count);

            //    if (succeeded.Count + failed.Count >= max)
            //    {
            //        break;
            //    }
            //}

            //ret.LastTime = lastTouched;
            //ret.Succeeded = succeeded.Count;
            //ret.Failed = failed.Count;

            //ret.Message = $"Cleaned up {cummulative} properties.";
            //if (ret.Succeeded > 0)
            //{
            //    SaveChanges();
            //}

            //if (ret.Succeeded + ret.Failed >= max) return ret;

            //ret.Complete = true;
            //ret.Message += "No more songs to clean up";

            //return ret;
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

            UpdateAndEnqueue(new [] { song } );
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
            var info = SearchServiceInfo.GetInfo();

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                return ids.Select(id => InternalFindSong(id, userName, indexClient)).ToList();
            }
        }

        private Song InternalFindSong(Guid id, string userName, SearchIndexClient client)
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
            catch (Microsoft.Rest.Azure.CloudException e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose, e.Message);
                return null;
            }
        }

        IEnumerable<Song> FindUserSongs(string user, string id = "default")
        {
            const int max = 250;

            var filter = SongFilter.AzureSimple;
            filter.User = user;

            var afilter = AzureParmsFromFilter(filter);
            afilter.Top = max;
            afilter.IncludeTotalResultCount = false;

            var results = new List<Song>();

            var stats = DanceStats;

            var info = SearchServiceInfo.GetInfo(id);

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                var response = DoAzureSearch(indexClient, null, afilter);

                results.AddRange(response.Results.Select(d => new Song(d.Document, stats, user)));

                if (response.ContinuationToken == null) return results;

                try
                {
                    while (response.ContinuationToken != null && results.Count < max)
                    {
                        response = indexClient.Documents.ContinueSearch(response.ContinuationToken);
                        results.AddRange(response.Results.Select(d => new Song(d.Document, stats, user)));
                    }
                }
                catch (Microsoft.Rest.Azure.CloudException e)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceVerbose, e.Message);
                }
                return results;
            }
        }

        public Song FindMergedSong(Guid id, string userName = null)
        {
            return DoAzureSearch(null, new SearchParameters {Filter = $"(AlternateIds/any(t: t eq '{id}'))"})
                .Results.Select(
                    r => new Song(r.Document, DanceStats, userName)).FirstOrDefault(s => !s.IsNull);
        }

        public DateTime GetLastModified()
        {
            // TODONEXT: Implement this with AZS to find most recently modified...
            var ret = DoAzureSearch(null, new SearchParameters {OrderBy=new [] { "Modified desc" }, Top=1,Select=new[] {Song.ModifiedField} }, CruftFilter.AllCruft);
            if (ret.Results.Count == 0) return DateTime.MinValue;

            var d = ret.Results[0].Document[Song.ModifiedField];
            return (DateTime) d;
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

        public enum MatchMethod { None, Tempo, Merge };

        private IEnumerable<Song> SongsFromHash(int hash)
        {
            return DoAzureSearch(null, new SearchParameters { Filter = $"(TitleHash eq {hash})" })
                    .Results.Select(
                        r => new Song(r.Document, DanceStats));
        }
        private LocalMerger MergeFromTitle(Song song)
        {
            var songs = SongsFromHash(song.TitleHash);

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

        private LocalMerger MergeFromPurchaseInfo(Song song)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var service in MusicService.GetSearchableServices())
            {
                var id = song.GetPurchaseId(service.Id);

                if (id == null) continue;

                var match = GetSongFromService(service, id);
                if (match != null)
                {
                    return new LocalMerger { Left = song, Right = match, MatchType = MatchType.Exact, Conflict = false };
                }
            }
            return null;
        }

        public IList<LocalMerger> MatchSongs(IList<Song> newSongs, MatchMethod method)
        {
            newSongs = RemoveDuplicateSongs(newSongs);
            var merge = new List<LocalMerger>();
            foreach (var song in newSongs)
            {
                var m = MergeFromPurchaseInfo(song) ?? MergeFromTitle(song);

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
            }

            return merge;
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

        public bool MergeCatalog(ApplicationUser user, IList<LocalMerger> merges, IEnumerable<string> dances = null)
        {
            var modified = false;

            var dancesL = dances?.ToList() ?? new List<string>();

            foreach (var m in merges)
            {

                // Matchtype of none indicates a new (to us) song, so just add it
                if (m.MatchType == MatchType.None)
                {
                    if (dancesL.Any())
                    {
                        m.Left.UpdateDanceRatingsAndTags(user.UserName, dancesL, Song.DanceRatingInitial,DanceStats);
                        m.Left.InferDances(user.UserName);
                    }
                    var temp = CreateSong(user, m.Left);
                    if (temp != null)
                    {
                        modified = true;
                        m.Left.SongId = temp.SongId;
                    }
                }
                // Any other matchtype should result in a merge, which for now is just adding the dance(s) from
                //  the new list to the existing song (or adding weight).
                // Now we're going to potentially add tempo - need a more general solution for this going forward
                else
                {
                    modified = AdditiveMerge(user, m.Right.SongId, m.Left, dancesL);
                }
            }

            return modified;
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
        public Song GetSongFromService(MusicService service,string id,string userName=null)
        {
            return null;

            //DBKILL: Need to index purchase links if we want to re-enable this
            //if (string.IsNullOrWhiteSpace(id)) return null;

            //var end = $":{service.CID}S";
            //var purchase = SongProperties.FirstOrDefault(sp => sp.Value.StartsWith(id) && sp.Name.StartsWith("Purchase:") && sp.Name.EndsWith(end));

            //return purchase == null ? null : FindSong(purchase.SongId, userName);
        }

        // TODO: This is extremely dependent on the form of the danceIds, just
        //  a temporary kludge until we get multi-select working
        private static string TrySingleId(List<string> danceList)
        {
            string ret = null;
            if (danceList != null && danceList.Count > 0 && !string.IsNullOrWhiteSpace(danceList[0]))
            {
                ret = danceList[0].Substring(0, 3).ToUpper();
                for (var i = 1; i < danceList.Count; i++)
                {
                    if (!string.Equals(ret, danceList[i].Substring(0, 3),StringComparison.OrdinalIgnoreCase))
                    {
                        ret = null;
                        break;
                    }
                }
            }

            return ret;
        }

        #endregion

        #region Tags

        public IReadOnlyDictionary<string, TagType> TagMap => DanceStatsManager.GetInstance(this).TagMap;

        public DanceStatsInstance DanceStats => DanceStatsManager.GetInstance(this);

        public TagType FindOrCreateTagType(string tag)
        {
            // Create a transitory TagType just for the parsing
            var temp = new TagType(tag);

            return FindOrCreateTagType(temp.Value, temp.Category);
        }

        public TagType FindOrCreateTagType(string value, string category, string primary = null)
        {
            return _context.TagTypes.Find(TagType.BuildKey(value, category)) ?? CreateTagType(value, category);
        }

        // TAGDELETE: I think we can get rid of this because is seems easier just to keep
        //  verbose normalized tags around (looks like a case of over eager optimization)
        public string CompressTags(string tags, string category)
        {
            var old = new TagList(tags);
            var result = new List<string>();
            foreach (var tag in old.Tags)
            {
                var types = TagTypes.Where(tt => tt.Value == tag).ToList();
                var type = types.FirstOrDefault(tt => tt.Category == category);
                var count = types.Count;

                if (type == null)
                {
                    type = CreateTagType(tag, category);
                    count += 1;
                }

                result.Add(count > 1 ? type.ToString() : tag);
            }

            return new TagList(result).ToString();
        }

        // Add in category for tags that don't already have one + create
        //  tagtype if necessary
        public string NormalizeTags(string tags, string category)
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
                    fullTag = TagType.BuildKey(tag, category);
                }
                else if (rg.Length == 2)
                {
                    tempTag = rg[0];
                    tempCat = rg[1];
                }

                FindOrCreateTagType(tempTag, tempCat);

                result.Add(fullTag);
            }

            return new TagList(result).ToString();
        }

        public IEnumerable<TagType> GetTagTypes(string value)
        {
            return _context.TagTypes.Where(t => t.Key.StartsWith(value + ":"));
        }

        public IReadOnlyList<TagType> CachedTagTypes()
        {
            return DanceStatsManager.GetInstance(this).TagTypes;
        }

        public IEnumerable<TagCount> GetTagSuggestions(Guid? user = null, char? targetType = null, string tagType = null, int count = int.MaxValue, bool normalized=false)
        {
            // from m in Modified where m.ApplicationUserId == user.Id && m.Song.TitleHash != 0 select m.Song;

            var userString = user?.ToString();
            var trg = targetType.HasValue ? new string(targetType.Value, 1) : null;
            var tagLabel = tagType == null ? null : ":" + tagType;

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
                        var tts = (tagType == string.Empty) ? CachedTagTypes() : CachedTagTypes().Where(tt => tt.Category == tagType);
                        ret = TagType.ToTagCounts(tts).OrderByDescending(tc => tc.Count);
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

        public IEnumerable<TagType> OrderedTagTypes => DanceStatsManager.GetInstance(this).TagTypes;

        public ICollection<TagType> GetTagRings(TagList tags)
        {
            var tagCache = TagMap;
            var map = new Dictionary<string, TagType>();
            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var tag in tags.Tags)
            {
                TagType tt;
                if (!tagCache.TryGetValue(tag.ToLower(), out tt))
                    continue;

                while (tt.Primary != null)
                {
                    tt = tt.Primary;
                }
                if (!map.ContainsKey(tt.Key))
                {
                    map.Add(tt.Key, tt);
                }
            }

            return map.Values;
        }

        private TagType CreateTagType(string value, string category, string primary = null) 
        {
            var type = _context.TagTypes.Create();
            type.Key = TagType.BuildKey(value, category);
            type.PrimaryId = primary;

            var other = TagTypes.Find(type.Key);
            if (other != null)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Attempt to add duplicate tag: {other} {type}");
                type = other;
            }
            else
            {
                type = _context.TagTypes.Add(type);
                DanceStatsManager.GetInstance(this).AddTagType(type);
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

        private static readonly Dictionary<string,IOrderedEnumerable<TagCount>> s_sugMap = new Dictionary<string, IOrderedEnumerable<TagCount>>();
        private static readonly Dictionary<string, IOrderedEnumerable<TagCount>> s_usMap = new Dictionary<string, IOrderedEnumerable<TagCount>>();

        #endregion

        #region Load

        private const string SongBreak = "+++++SONGS+++++";
        private const string TagBreak = "+++++TAGSS+++++";
        private const string SearchBreak = "+++++SEARCHES+++++";
        private const string DanceBreak = "+++++DANCES+++++";
        private const string UserHeader = "UserId\tUserName\tRoles\tPWHash\tSecStamp\tLockout\tProviders\tEmail\tEmailConfirmed\tStartDate\tRegion\tPrivacy\tCanContact\tServicePreference\tLastActive\tRowCount\tColumns";

        static public bool IsSongBreak(string line) {
            return IsBreak(line, SongBreak);
        }
        static public bool IsTagBreak(string line)
        {
            return IsBreak(line, TagBreak);
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

                var extended = cells.Length >= 13;
                if (extended)
                {
                    email = cells[7];
                    bool.TryParse(cells[8], out emailConfirmed);
                    DateTime.TryParse(cells[9], out date);
                    region = cells[10];
                    byte.TryParse(cells[11], out privacy);
                    byte canContactT;
                    byte.TryParse(cells[12], out canContactT);
                    canContact = (ContactStatus)canContactT;
                    servicePreference = cells[13];
                    DateTime.TryParse(cells[14], out active);
                    int rcT;
                    if (!string.IsNullOrWhiteSpace(cells[15]) && int.TryParse(cells[15], out rcT))
                    {
                        rc = rcT;
                    }
                    if (!string.IsNullOrWhiteSpace(cells[16]))
                    {
                        col = cells[16];
                    }
                }

                var user = FindUser(userName);
                var create = user == null;

                if (create)
                {
                    user = _context.Users.Create();
                    user.Id = userId;
                    user.UserName = userName;
                    user.PasswordHash = hash;
                    user.SecurityStamp = stamp;
                    user.LockoutEnabled = string.Equals(lockout, "TRUE", StringComparison.InvariantCultureIgnoreCase);

                    if (!string.IsNullOrWhiteSpace(providers))
                    {
                        var entries = providers.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        for (var j = 0; j < entries.Length; j += 2)
                        {
                            var login = new IdentityUserLogin() { LoginProvider = entries[j], ProviderKey = entries[j + 1], UserId = userId };
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

                            var iur = new IdentityUserRole() { UserId = user.Id, RoleId = role.Id };
                            user.Roles.Add(iur);
                        }
                    }

                    if (extended)
                    {
                        user.StartDate = date;
                        user.Email = email;
                        user.EmailConfirmed = emailConfirmed;
                        user.Region = region;
                        user.Privacy = privacy;
                        user.CanContact = canContact;
                        user.ServicePreference = servicePreference;
                        user.LastActive = active;
                        user.RowCountDefault = rc;
                        user.ColumnDefaults = col;
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

                    user.LastActive = active;
                    user.RowCountDefault = rc;
                    user.ColumnDefaults = col;
                }
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Saving Changes");
            SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadUsers");
        }

        public void LoadSearches(IList<string> lines)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering LoadSearches");

            if (lines == null || lines.Count < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (lines.Count > 1 && IsSearchBreak(lines[0]))
            {
                lines.RemoveAt(0);
            }

            var fieldCount = lines[0].Split('\t').Length;
            for (var i = 0; i < lines.Count; i++)
            {
                AdminMonitor.UpdateTask("LoadSearches", i);
                var s = lines[i];

                if (string.Equals(s, TagBreak, StringComparison.InvariantCultureIgnoreCase))
                {
                    break;
                }

                var cells = s.Split('\t');
                if (cells.Length != fieldCount) continue;

                var userName = cells[0];
                var name = cells[1];
                var query = cells[2];
                var favorite = string.Equals(cells[3], "true", StringComparison.OrdinalIgnoreCase);
                int count;
                int.TryParse(cells[4], out count);
                DateTime created;
                DateTime.TryParse(cells[5], out created);
                DateTime modified;
                DateTime.TryParse(cells[6], out modified);

                var user = string.IsNullOrWhiteSpace(userName) ? null : FindUser(userName);

                var search = user == null ? Searches.FirstOrDefault(x => x.ApplicationUser == null && x.Query == query) : 
                    Searches.FirstOrDefault(x => x.ApplicationUser != null && x.ApplicationUser.Id == user.Id && x.Query == query);

                if (search == null)
                {
                    search = Searches.Create();
                    search.ApplicationUser = user;
                    search.Query = query;
                    Searches.Add(search);
                }

                search.Name = name;
                search.Favorite = favorite;
                search.Count = count;
                search.Created = created;
                search.Modified = modified;
            }

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
            SaveChanges();
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadSearches");
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
                TagType tt = null;
                if (cells.Length >= 2)
                {
                    var category = cells[0];
                    var value = cells[1];

                    tt = FindOrCreateTagType(value, category);
                }

                if (tt != null && cells.Length >= 3 && !string.IsNullOrWhiteSpace(cells[2]))
                {
                    tt.PrimaryId = cells[2];
                }

                DateTime modified;
                if (tt != null && cells.Length >= 4 && 
                    !string.IsNullOrWhiteSpace(cells[3]) && 
                    DateTime.TryParse(cells[3], out modified))
                {
                    tt.Modified = modified;
                }
            }

            foreach (var tt in TagTypes)
            {
                if (tt.PrimaryId != null && tt.Primary == null)
                {
                    tt.Primary = TagTypes.Find(tt.PrimaryId);
                }
            }
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Saving Changes");
            SaveChanges();

            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting LoadTags");
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

            _context.TrackChanges(true);

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

            _context.TrackChanges(false);

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

            _context.TrackChanges(true);
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

        public void UpdatePurchaseInfo(string songIds)
        {
            // DBKILL: Need to have a mechanism to do song updates in chunks
            //var songs = (songIds == null) ? Songs.Where(s => s.TitleHash != 0) : SongsFromList(songIds);

            //foreach (var song in songs)
            //{
            //    var sd = new Song(song);
            //    song.Purchase = sd.GetPurchaseTags();
            //}
        }
        #endregion

        #region Save
        public IList<string> SerializeUsers(bool withHeader=true, DateTime? from = null)
        {
            var users = new List<string>();

            if (!from.HasValue) from = new DateTime(1,1,1);

            foreach (var user in UserManager.Users.Where(u => u.StartDate >= from.Value))
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

                users.Add(
                    $"{userId}\t{username}\t{roles}\t{hash}\t{stamp}\t{lockout}\t{providers}\t{email}\t{emailConfirmed}\t{time}\t{region}\t{privacy}\t{canContact}\t{servicePreference}\t{lastActive}\t{rc}\t{col}");
            }

            if (withHeader && users.Count > 0)
            {
                users.Insert(0,UserHeader);
            }

            return users;
        }

        public IList<string> SerializeTags(bool withHeader = true)
        {
            var tags = new List<string>();

            if (withHeader)
            {
                tags.Add(TagBreak);
            }

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var tt in TagTypes)
            {
                tags.Add($"{tt.Category}\t{tt.Value}\t{tt.PrimaryId}\t{tt.Modified.ToString("g")}");
            }

            return tags;
        }

        public IList<string> SerializeSearches(bool withHeader = true)
        {
            var searches = new List<string>();

            if (withHeader)
            {
                searches.Add(SearchBreak);
            }

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var search in Searches.Include(s => s.ApplicationUser))
            {
                var userName = (search.ApplicationUser != null) ? search.ApplicationUser.UserName : string.Empty;
                searches.Add($"{userName}\t{search.Name}\t{search.Query}\t{search.Favorite}\t{search.Count}\t{search.Created.ToString("g")}\t{search.Modified.ToString("g")}");
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

        public IList<string> SerializeDances(bool withHeader = true, DateTime? from = null)
        {
            var dances = new List<string>();

            if (!from.HasValue) from = new DateTime(1, 1, 1);

            var dancelist = Dances.Where(d => d.Modified >= from.Value).OrderBy(d => d.Id);
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


        static private readonly string[] s_updateFields =  { Song.ModifiedField, Song.PropertiesField };
        static private readonly string[] s_updateEntities = { "Song", "SongProperties", "ModifiedBy", "DanceRatings" };
        static private readonly string[] s_updateNoId = { Song.NoSongId };
        private static readonly TimeSpan s_updateDelta = TimeSpan.FromMilliseconds(10);


        public int UploadIndex(IList<string> lines, string id = "default")
        {
            const int chunkSize = 500;
            var info = SearchServiceInfo.GetInfo(id);
            var page = 0;
            var stats = DanceStats;
            var added = 0;

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                for (var i = 0; i < lines.Count; page += 1)
                {
                    AdminMonitor.UpdateTask("AddSongs", added);
                    var chunk = new List<Song>();
                    for (; i < lines.Count && i < (page+1)*chunkSize; i++)
                    {
                        chunk.Add(new Song(lines[i],stats));
                    }

                    var songs = (from song in chunk where !song.IsNull select song.GetIndexDocument()).ToList();

                    if (songs.Count <= 0) continue;

                    var batch = IndexBatch.MergeOrUpload(songs);
                    var results = indexClient.Documents.Index(batch);
                    added += results.Results.Count;
                }

                return added;
            }
        }

        public int UpdateAzureIndex(string id = "default")
        {
            return 0;

            // DBKILL: Need to make sure that we're queueing updates to the Azure Index where we used to save to the DB
            //var skip = 0;
            //var done = false;

            //var info = SearchServiceInfo.GetInfo(id);

            //using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.AdminKey)))
            //using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            //{
            //    do
            //    {
            //        var songs = new List<Document>();
            //        var deleted = new List<Document>();

            //        var chunk = 5;
            //        switch (skip)
            //        {
            //            case 0:
            //                break;
            //            case 5:
            //                chunk = 50;
            //                break;
            //            default:
            //                chunk = 500;
            //                Context.ClearEntities(s_updateEntities);
            //                break;
            //        }

            //        var sqlRecent = TakeRecent(chunk, skip);
            //        // ReSharper disable once LoopCanBeConvertedToQuery
            //        foreach (var song in sqlRecent)
            //        {
            //            var exists = true;
            //            try
            //            {
            //                var doc = indexClient.Documents.Get(song.SongId.ToString(), s_updateFields);
            //                var diff = song.Modified - ((DateTimeOffset) doc["Modified"]).UtcDateTime;
            //                var teq = diff < s_updateDelta;
            //                if (teq && doc["Properties"] as string == song.Serialize(s_updateNoId))
            //                {
            //                    done = true;
            //                    break;
            //                }
            //            }
            //            catch (Microsoft.Rest.Azure.CloudException)
            //            {
            //                exists = false;
            //                // Document isn't in the index at all, so add it
            //            }

            //            if (!song.IsNull)
            //            {
            //                songs.Add(new Song(song).GetIndexDocument());
            //                DanceStats.UpdateSong(song,this);
            //            }
            //            else if (exists)
            //            {
            //                deleted.Add(new Song(song).GetIndexDocument());
            //                DanceStats.UpdateSong(song, this);
            //            }
            //            skip += 1;
            //        }

            //        if (songs.Count > 0)
            //        {
            //            var batch = IndexBatch.MergeOrUpload(songs);
            //            indexClient.Documents.Index(batch);
            //        }
            //        if (deleted.Count > 0)
            //        {
            //            var delete = IndexBatch.Delete(deleted);
            //            indexClient.Documents.Index(delete);
            //        }

            //    } while (!done);
            //}

            //return skip;
        }

        public SearchResults AzureSearch(SongFilter filter, int? pageSize = null, CruftFilter cruft = CruftFilter.NoCruft, string id = "default")
        {
            return AzureSearch(filter.SearchString, AzureParmsFromFilter(filter, pageSize), cruft, id);
        }

        public SearchResults AzureSearch(string search, SearchParameters parameters, CruftFilter cruft = CruftFilter.NoCruft, string id = "default", DanceStatsInstance stats = null)
        {
            parameters.IncludeTotalResultCount = true;
            var response = DoAzureSearch(search,parameters,cruft,id);
            var songs = response.Results.Select(d => new Song(d.Document,stats)).ToList();
            var pageSize = parameters.Top ?? 25;
            var page = ((parameters.Skip ?? 0)/pageSize) + 1;
            var facets = response.Facets;
            return new SearchResults(search, songs.Count,response.Count ?? -1,page,pageSize,songs,facets);
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

        private static DocumentSearchResult DoAzureSearch(ISearchIndexClient client, string search, SearchParameters parameters, CruftFilter cruft = CruftFilter.NoCruft)
        {
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

            if (extra.Length <= 0)
                return client.Documents.Search(search, parameters);

            if (parameters.Filter == null)
            {
                parameters.Filter = extra.ToString();
            }
            else
            {
                extra.AppendFormat(" and {0}", parameters.Filter);
                parameters.Filter = extra.ToString();
            }

            return client.Documents.Search(search, parameters);
        }

        private static DocumentSearchResult DoAzureSearch(string search, SearchParameters parameters, CruftFilter cruft = CruftFilter.NoCruft, string id = "default")
        {
            var info = SearchServiceInfo.GetInfo(id);

            using (var serviceClient = new SearchServiceClient(info.Name, new SearchCredentials(info.QueryKey)))
            using (var indexClient = serviceClient.Indexes.GetClient(info.Index))
            {
                return DoAzureSearch(indexClient, search, parameters, cruft);
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

        public IEnumerable<string> BackupIndex(string name = "default", int count = -1, DateTime? from = null, string filter = null)
        {
            return BackupIndex(name, count, from, (filter == null) ? null : new SongFilter(filter));
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

                    foreach (var doc in response.Results)
                    {
                        var m = doc.Document["Modified"];
                        var modified = (DateTimeOffset)m;
                        if (from != null && modified < from)
                        {
                            response.ContinuationToken = null;
                            break;
                        }

                        results.Add(Song.Serialize(doc.Document[Song.SongIdField] as string, doc.Document[Song.PropertiesField] as string));
                        AdminMonitor.UpdateTask("readSongs", results.Count);
                    }
                    token = response.ContinuationToken;
                } while (token != null);

                return results;
            }
        }

        #endregion

        #region User
        public ApplicationUser FindUser(string name)
        {
            ApplicationUser user;
            if (_userCache.TryGetValue(name,out user))
                return user;

            user = UserManager.FindByName(name);
            if (user != null)
                _userCache[name] = user;

            return user;
        }
        public ApplicationUser FindOrAddUser(string name, string role)
        {
            var user = FindUser(name);

            if (user == null)
            {
                user = new ApplicationUser { UserName = name, Email = name + "@music4dance.net", EmailConfirmed = true, StartDate = DateTime.Now };
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

        //public void ChangeUserName(string oldUserName, string userName)
        //{
            // DBKILL: Do we want to enable this in the new universe?
            //var user = UserManager.FindByName(oldUserName);
            //if (user == null)
            //{
            //    throw new ArgumentOutOfRangeException($"User {0} doesn't exist",oldUserName);
            //}

            //Context.TrackChanges(false);
            //Context.LazyLoadingEnabled = false;

            //foreach (var prop in SongProperties.Where(p => (p.Name == Song.UserField || p.Name == Song.UserProxy) && p.Value == oldUserName))
            //{
            //    prop.Value = userName;
            //}

            //Context.TrackChanges(true);
        //}

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
        public IList<Song> FindMergeCandidates(int n, int level)
        {
            return MergeCluster.GetMergeCandidates(_context, n, level);
        }
    }
}
