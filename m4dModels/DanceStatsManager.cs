using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DanceLibrary;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;

namespace m4dModels
{
    public class SmartLock
    {
        private readonly object _lockObject = new object();
        private string _holdingTrace = "";

        private static readonly int WARN_TIMEOUT_MS = 5000; //5 secs

        public void Lock(Action action)
        {
            try
            {
                Enter();
                action.Invoke();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SmartLock Lock action: {ex.Message}");
            }
            finally
            {
                Exit();
            }
        }

        public TResult Lock<TResult>(Func<TResult> action)
        {
            
            try
            {
                Enter();
                return action.Invoke();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SmartLock Lock action: {ex.Message}");
                return default(TResult);
            }
            finally
            {
                Exit();
            }
        }

        private void Enter()
        {
            try
            {
                var locked = false;
                var timeoutMs = 0;
                while (!locked)
                {
                    //keep trying to get the lock, and warn if not accessible after timeout
                    locked = Monitor.TryEnter(_lockObject, WARN_TIMEOUT_MS);
                    if (!locked)
                    {
                        timeoutMs += WARN_TIMEOUT_MS;
                        Trace.WriteLine("Lock held: " + (timeoutMs / 1000) + " secs by " + _holdingTrace + " requested by " + GetStackTrace());
                    }
                }

                //save a stack trace for the code that is holding the lock
                _holdingTrace = GetStackTrace();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SmartLock Enter: {ex.Message}");
            }
        }

        private string GetStackTrace()
        {
            var trace = new StackTrace();
            var threadId = Thread.CurrentThread.Name ?? "";
            return "[" + threadId + "]" + trace.ToString().Replace('\n', '|').Replace("\r", "");
        }

        private void Exit()
        {
            try
            {
                Monitor.Exit(_lockObject);
                _holdingTrace = "";
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SmartLock Exit: {ex.Message}");
            }
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DanceStatsInstance
    {
        public DanceStatsInstance(IEnumerable<TagType> tagTypes)
        {
            TagTypes = (tagTypes as List<TagType>)?.ToList();
            FixupTags();
        }

        [JsonConstructor]
        public DanceStatsInstance(IEnumerable<DanceStats> tree, IEnumerable<TagType> tagTypes)
        {
            TagTypes = tagTypes.ToList();
            Tree = tree.ToList();

            FixupTags();
            FixupStats();
        }

        public void FixupStats(DanceMusicService dms = null, string source = "default")
        {
            foreach (var d in Tree)
            {
                d.SetParents();
            }
            Map = List.ToDictionary(ds => ds.DanceId);

            foreach (var ds in List)
            {
                if (dms == null)
                {
                    ds.RebuildTopSongs(this);
                }
                else if (ds.SongCount > 0)
                {
                    // TopN and MaxWeight
                    var filter = dms.AzureParmsFromFilter(new SongFilter { Dances = ds.DanceId, SortOrder = "Dances" }, 10);
                    DanceMusicService.AddAzureCategories(filter, "GenreTags,StyleTags,TempoTags,OtherTags", 100);
                    var results = dms.AzureSearch(null, filter, DanceMusicService.CruftFilter.NoCruft, source, this);
                    ds.TopSongs = results.Songs;
                    var song = ds.TopSongs.FirstOrDefault();
                    var dr = song?.DanceRatings.FirstOrDefault(d => d.DanceId == ds.DanceId);

                    if (dr != null) ds.MaxWeight = dr.Weight;

                    // SongTags
                    ds.SongTags = (results.FacetResults == null) ? new TagSummary() : new TagSummary(results.FacetResults, TagMap);
                }
            }
        }

        public void FixupTags() { 
            TagMap = TagTypes.ToDictionary(tt => tt.Key.ToLower());

            foreach (var tt in TagTypes.Where(tt => !string.IsNullOrEmpty(tt.PrimaryId)))
            {
                tt.Primary = TagMap[tt.PrimaryId.ToLower()];
                if (tt.Primary.Ring == null) tt.Primary.Ring = new List<TagType>();
                tt.Primary.Ring.Add(tt);
            }
        }

        [JsonProperty]
        public List<TagType> TagTypes { get; set; }

        public Dictionary<string, TagType> TagMap { get; private set; }

        [JsonProperty]
        public List<DanceStats> Tree { get; set; }

        public List<DanceStats> List => _flat ?? (_flat = Flatten());
        public Dictionary<string, DanceStats> Map { get; private set; }

        public int GetScaledRating(string danceId, int weight, int scale = 5)
        {
            var sc = FromId(danceId);
            if (sc == null) return 0;

            float max = sc.MaxWeight;
            var ret = (int)(Math.Ceiling(weight * scale / max));

            if (TraceLevels.General.TraceInfo && (weight > max || ret < 0))
            {
                Trace.WriteLine($"{danceId}: {weight} ? {max}");
            }

            return Math.Max(0, Math.Min(ret, scale));
        }
        public string GetRatingBadge(string danceId, int weight)
        {
            var scaled = GetScaledRating(danceId, weight);

            //return "/Content/thermometer-" + scaled.ToString() + ".png";
            return "rating-" + scaled;
        }

        public DanceStats FromId(string danceId)
        {
            if (danceId.Length > 3) danceId = danceId.Substring(0, 3);

            DanceStats sc;
            if (Map.TryGetValue(danceId, out sc)) return sc;

            if (Dances.Instance.DanceFromId(danceId) == null) return null;

            Trace.WriteLineIf(TraceLevels.General.TraceError, $"Failed to find danceId {danceId}");
            // Clear out the cache to force a reload: workaround for possible cache corruption.
            // TODO: Put in the infrastructure to send app insights events when this happens
            Trace.WriteLineIf(TraceLevels.General.TraceError, "Attempting to rebuild cache");

            DanceStatsManager.ClearCache(null, true);
            return null;
        }

        public DanceStats FromName(string name, string userName = null)
        {
            if (string.IsNullOrEmpty(userName)) userName = null;
            name = DanceObject.SeoFriendly(name);
            var stats = List.FirstOrDefault(sc => string.Equals(sc.SeoName, name));
            return (userName == null) ? stats : stats?.CloneForUser(userName,this);
        }
        public static DanceStatsInstance LoadFromJson(string json)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            var instance = JsonConvert.DeserializeObject<DanceStatsInstance>(json,settings);

            Dances.Reset(Dances.Load(instance.GetDanceTypes(), instance.GetDanceGroups()));

            return instance;
        }

        public string SaveToJson()
        {
            return JsonConvert.SerializeObject(this,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
                });
        }

        public void AddTagType(TagType tt)
        {
            TagMap[tt.Key.ToLower()] = tt;
            var index = TagTypes.BinarySearch(tt,Comparer<TagType>.Create((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase)));
            if (index < 0) index = ~index;
            TagTypes.Insert(index,tt);
        }


        public void UpdateSong(Song song, DanceMusicService dms)
        {
            lock (_queuedSongs)
            {
                _queuedSongs[song.SongId] = song;

                if (!TopSongs.ContainsKey(song.SongId))
                {
                    if (song.IsNull) _otherSongs.Remove(song.SongId);
                    _otherSongs[song.SongId] = song;
                    return;
                }

                if (song.IsNull) TopSongs.Remove(song.SongId);
                TopSongs[song.SongId] = song;

                foreach (var d in List)
                {
                    var songs = d.TopSongs as List<Song>;
                    if (songs == null) continue;
                    var idx = songs.FindIndex(s => s.SongId == song.SongId);
                    if (idx == -1) continue;
                    if (song.IsNull)
                        songs.RemoveAt(idx);
                    else
                        songs[idx] = song;
                }
            }
        }

        public IEnumerable<Song> DequeuSongs()
        {
            lock (_queuedSongs)
            {
                var ret =  _queuedSongs.Values.ToList();
                _queuedSongs.Clear();
                return ret;
            }
        }
        public Song FindSongDetails(Guid songId, string userName)
        {
            var sd = TopSongs.GetValueOrDefault(songId) ?? _otherSongs.GetValueOrDefault(songId);
            if (sd == null) return null;
            return (userName == null) ? sd : new Song(sd, this, userName);
        }

        internal List<DanceType> GetDanceTypes()
        {
            return List.Where(s => s.DanceType != null).Select(s => s.DanceType).ToList();
        }

        internal List<DanceGroup> GetDanceGroups()
        {
            return List.Where(s => s.DanceGroup != null).Select(s => s.DanceGroup).ToList();
        }

        private List<DanceStats> Flatten()
        {
            var flat = new List<DanceStats>();

            flat.AddRange(Tree);

            foreach (var children in Tree.Select(ds => ds.Children))
            {
                flat.AddRange(children);
            }

            var all = new DanceStats
            {
                SongCount = Tree.Sum(s => s.SongCount),
                Children = null
            };

            flat.Insert(0, all);

            return flat;
        }

        private Dictionary<Guid,Song> TopSongs => _topSongs ?? (_topSongs = new Dictionary<Guid,Song>(List.SelectMany(d => d.TopSongs ?? new List<Song>()).DistinctBy(s => s.SongId).ToDictionary(s => s.SongId)));
        private readonly Dictionary<Guid, Song> _otherSongs = new Dictionary<Guid, Song>();
        private readonly Dictionary<Guid, Song> _queuedSongs = new Dictionary<Guid, Song>();

        private List<DanceStats> _flat;
        private Dictionary<Guid,Song> _topSongs;
    }

    public class DanceStatsManager
    {
        public static string AppData;
        public static DateTime LastUpdate { get; private set; }
        public static string Source { get; private set; }

        public static Dance DanceFromId(string id)
        {
            return s_instance != null ? s_instance.Map[id].Dance : new Dance {Id = id};
        }

        #region Access
        public static DanceStatsInstance GetInstance(DanceMusicService dms)
        {
            return s_lock.Lock(() => s_instance ?? (s_instance = LoadFromAppData()) ?? (s_instance = LoadFromStore(dms, true)));
        }

        public static void SetInstance(DanceStatsInstance instance)
        {
            s_lock.Lock(() =>
            {
                s_instance = instance;
            });
        }

        public static IList<DanceStats> GetFlatDanceStats(DanceMusicService dms)
        {
            return GetInstance(dms).List;
        }

        public static IList<DanceStats> GetDanceStats(DanceMusicService dms)
        {
            return GetInstance(dms).Tree;
        }
        #endregion

        #region Building

        private static readonly SmartLock s_lock = new SmartLock();
        private static DanceStatsInstance s_instance;

        public static void ClearCache(DanceMusicService dms = null, bool reload = false)
        {
            DanceStatsInstance instance = null;

            if (dms != null) instance = LoadFromStore(dms,true);
            else if (reload) instance = LoadFromAppData();

            s_lock.Lock(() =>
            {
                if (reload)
                {
                    Reloads += 1;
                }

                if (instance != null)
                {
                    s_instance = instance;
                    ClearAssociates();
                    return;
                }

                Task.Run(() => RebuildDanceStats(DanceMusicService.GetService()));
            });
        }

        private static void RebuildDanceStats(DanceMusicService dms)
        {
            var instance = LoadFromStore(dms,true);
            s_lock.Lock(() =>
            {
                s_instance = instance;
                ClearAssociates();
            });
        }

        private static void ClearAssociates()
        {
            DanceMusicService.BlowTagCache();
            Song.ResetIndex();
        }

        public static void ReloadDances(DanceMusicService dms)
        {
            s_lock.Lock(() =>
            {
                dms.Context.LoadDances();
                foreach (var dance in dms.Dances)
                {
                    DanceStats danceStats;
                    if (s_instance.Map.TryGetValue(dance.Id, out danceStats))
                    {
                        danceStats.CopyDanceInfo(dance, false, dms);
                    }
                }

                LastUpdate = DateTime.Now;
                Source = Source + " + reload";
            });
        }

        private static DanceStatsInstance LoadFromAppData()
        {
            return s_lock.Lock(() =>
            {
                if (AppData == null) return null;

                var path = System.IO.Path.Combine(AppData, "dance-tag-stats.json");
                if (!System.IO.File.Exists(path)) return null;

                LastUpdate = DateTime.Now;
                Source = "AppData";
                return DanceStatsInstance.LoadFromJson(System.IO.File.ReadAllText(path));
            });
        }

        public static DanceStatsInstance LoadFromStore(DanceMusicService dms, bool save)
        {
            return LoadFromAzure(dms,"default",save);
        }


        public static DanceStatsInstance LoadFromAzure(DanceMusicService dms, string source = "default", bool save = false)
        {
            var instance = new DanceStatsInstance(AzureTagTypes(dms, source));
            instance.Tree = AzureDanceStats(dms, source).ToList();
            instance.FixupStats(dms,source);
            if (!save) return instance;

            LastUpdate = DateTime.Now;
            Source = "Azure";
            SaveToAppData(instance);
            return instance;
        }

        private static void SaveToAppData(DanceStatsInstance instance)
        {
            s_lock.Lock(() =>
            {
                if (AppData == null) return;

                var json = instance.SaveToJson();
                var path = System.IO.Path.Combine(AppData, "dance-tag-stats.json");
                System.IO.File.WriteAllText(path, json, Encoding.UTF8);
            });
        }

        private static IEnumerable<DanceStats> AzureDanceStats(DanceMusicService dms, string source)
        {
            var stats = new List<DanceStats>();
            dms.Context.LoadDances();

            var facets = dms.GetTagFacets("DanceTags,DanceTagsInferred", 100, source);

            var tags =  IndexDanceFacet(facets["DanceTags"]);
            var inferred = IndexDanceFacet(facets["DanceTagsInferred"]);

            var used = new HashSet<string>();

            // First handle dancegroups and types under dancegroups
            foreach (var dg in Dances.Instance.AllDanceGroups)
            {
                // All groups except other have a valid 'root' node...
                var scGroup = InfoFromDance(dms, false, dg);
                scGroup.Children = new List<DanceStats>();
                scGroup.AggregateSongCounts(tags, inferred);

                stats.Add(scGroup);

                foreach (var dtyp in dg.Members.Select(dtypT => dtypT as DanceType))
                {
                    AzureHandleType(dtyp, scGroup, tags, inferred, dms);
                    used.Add(dtyp.Id);
                }
            }

            // Then handle ungrouped types
            foreach (var dt in Dances.Instance.AllDanceTypes.Where(dt => !used.Contains(dt.Id)))
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Ungrouped Dance: {0}", dt.Id);
            }

            return stats.OrderByDescending(x => x.Children.Count).ToList();
        }

        private static IEnumerable<TagType> AzureTagTypes(DanceMusicService dms, string source)
        {
            var tagTypes = dms.TagTypes.OrderBy(t => t.Key).ToList();

            var facets = dms.GetTagFacets("GenreTags,StyleTags,TempoTags,OtherTags", 500, source);

            var map = tagTypes.ToDictionary(tt => tt.Key.ToLower());

            foreach (var facet in facets)
            {
                var id = SongFilter.TagClassFromName(facet.Key.Substring(0,facet.Key.Length-4)).ToLower();
                IndexFacet(facet.Value,id,map);
            }

            return tagTypes;
        }

        private static Dictionary<string, long> IndexDanceFacet(IEnumerable<FacetResult> facets)
        {
            var ret = new Dictionary<string, long>();

            foreach (var facet in facets)
            {
                var d = Dances.Instance.DanceFromName((string)facet.Value);
                if (d == null || !facet.Count.HasValue) continue;

                ret[d.Id] = facet.Count.Value;
            }

            return ret;
        }

        private static void IndexFacet(IEnumerable<FacetResult> facets, string category, IReadOnlyDictionary<string,TagType> map)
        {
            foreach (var facet in facets)
            {
                if (!facet.Count.HasValue) continue;

                var key = $"{facet.Value}:{category}";
                TagType tt;
                if (!map.TryGetValue(key, out tt))
                {
                    Trace.WriteLine($"Couldn't map key: {key}");
                    continue;
                }

                tt.Count = (int)facet.Count.Value;
            }
        }



        private static void AzureHandleType(DanceObject dtyp, DanceStats scGroup, IReadOnlyDictionary<string, long> tags, IReadOnlyDictionary<string, long> inferred, DanceMusicService dms)
        {
            var scType = InfoFromDance(dms, false, dtyp);
            scType.AggregateSongCounts(tags, inferred);

            scGroup.Children.Add(scType);
            scType.Parent = scGroup;

            // Only add children to MSC, for other groups they're already built in

            if (scGroup.DanceId == "MSC" || scGroup.DanceId == "PRF")
            {
                scGroup.SongCount += scType.SongCount;
            }
        }

        private static DanceStats InfoFromDance(DanceMusicService dms, bool includeStats, DanceObject d)
        {
            if (d == null)
            {
                throw new ArgumentNullException(nameof(d));
            }

            var danceStats = new DanceStats
            {
                DanceObject = d,
                Children = null
            };

            danceStats.CopyDanceInfo(dms.Dances.FirstOrDefault(t => t.Id == d.Id), includeStats, dms);
            return danceStats;
        }

        #endregion

        public static int Reloads { get; private set; }
    }
}