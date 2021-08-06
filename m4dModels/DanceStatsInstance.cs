using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DanceLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace m4dModels
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DanceStatsInstance
    {
        private const string descriptionPlaceholder =
            "We're busy doing research and pulling together a general description for this dance style. Please check back later for more info.";

        private readonly Dictionary<Guid, Song> _otherSongs = new Dictionary<Guid, Song>();
        private readonly Dictionary<Guid, Song> _queuedSongs = new Dictionary<Guid, Song>();

        private List<DanceStats> _flat;
        private Dictionary<Guid, Song> _topSongs;

        [JsonConstructor]
        public DanceStatsInstance(IEnumerable<DanceStats> tree, IEnumerable<TagGroup> tagGroups) :
            this(tree, new TagManager(tagGroups))
        {
        }

        public DanceStatsInstance(IEnumerable<DanceStats> tree, TagManager tagManager)
        {
            TagManager = tagManager;
            Tree = tree.ToList();
        }

        [JsonProperty]
        public List<DanceStats> Tree { get; set; }

        [JsonProperty(PropertyName = "tagGroups")]
        public List<TagGroup> TagGroups => TagManager.TagGroups;

        public TagManager TagManager { get; set; }

        public List<DanceStats> List => _flat ??= Flatten();
        public Dictionary<string, DanceStats> Map { get; private set; }

        private Dictionary<Guid, Song> TopSongs => _topSongs ??= new Dictionary<Guid, Song>(
            List.SelectMany(d => d.TopSongs ?? new List<Song>())
                .DistinctBy(s => s.SongId).ToDictionary(s => s.SongId));

        public static async Task<DanceStatsInstance> BuildInstance(TagManager tagManager,
            IEnumerable<DanceStats> tree,
            DanceMusicCoreService dms, string source)
        {
            var instance = new DanceStatsInstance(tree, tagManager);
            await instance.FixupStats(dms, true, source);
            return instance;
        }

        public async Task FixupStats(DanceMusicCoreService dms, bool reloadSongs,
            string source = "default")
        {
            dms.SetStatsInstance(this);
            foreach (var d in Tree)
            {
                d.SetParents();
            }

            Map = List.ToDictionary(ds => ds.DanceId);

            var playlists =
                dms.PlayLists.Where(p => p.Type == PlayListType.SpotifyFromSearch)
                    .Where(p => p.Name != null)
                    .Select(p => new PlaylistMetadata { Id = p.Id, Name = p.Name })
                    .ToDictionary(m => m.Name, m => m);

            var saveChanges = false;
            var newDances = new List<string>();
            foreach (var ds in List)
            {
                if (ds.SongCount > 0)
                {
                    if (reloadSongs)
                    {
                        // TopN and MaxWeight
                        var filter =
                            dms.AzureParmsFromFilter(
                                new SongFilter { Dances = ds.DanceId, SortOrder = "Dances" }, 10);
                        DanceMusicCoreService.AddAzureCategories(
                            filter, "GenreTags,StyleTags,TempoTags,OtherTags", 100);
                        var results = await dms.Search(
                            null, filter, DanceMusicCoreService.CruftFilter.NoCruft, null, source);
                        ds.SetTopSongs(results.Songs);
                        var song = ds.TopSongs.FirstOrDefault();
                        var dr = song?.DanceRatings.FirstOrDefault(d => d.DanceId == ds.DanceId);

                        if (dr != null)
                        {
                            ds.MaxWeight = dr.Weight;
                        }

                        // SongTags
                        ds.SongTags = results.FacetResults == null
                            ? new TagSummary()
                            : new TagSummary(results.FacetResults, TagManager.TagMap);
                    }
                    else
                    {
                        await ds.LoadSongs(dms);
                    }

                    if (playlists.TryGetValue(ds.DanceName, out var metadata))
                    {
                        ds.SpotifyPlaylist = metadata.Id;
                    }
                }
                else
                {
                    if (ds.Dance.Description == null)
                    {
                        var dance = await dms.Dances.FindAsync(ds.DanceId);
                        if (dance == null)
                        {
                            dms.Dances.Add(
                                new Dance
                                {
                                    Id = ds.DanceId,
                                    Description = descriptionPlaceholder

                                });
                        }
                        else
                        {
                            dance.Description = descriptionPlaceholder;
                        }
                    }

                    saveChanges = true;

                    newDances.Add(ds.DanceId);
                    ds.SetTopSongs(new List<Song>());
                    ds.SongTags = new TagSummary();
                }
            }

            if (saveChanges)
            {
                await dms.SaveChanges();
            }

            await dms.UpdateIndex(newDances);
        }

        public int GetScaledRating(string danceId, int weight, int scale = 5)
        {
            var sc = FromId(danceId);
            if (sc == null)
            {
                return 0;
            }

            float max = sc.MaxWeight;
            var ret = (int)Math.Ceiling(weight * scale / max);

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
            if (danceId.Length > 3)
            {
                danceId = danceId.Substring(0, 3);
            }

            DanceStats sc;
            if (Map.TryGetValue(danceId.ToUpper(), out sc))
            {
                return sc;
            }

            if (Dances.Instance.DanceFromId(danceId.ToUpper()) == null)
            {
                return null;
            }

            Trace.WriteLineIf(TraceLevels.General.TraceError, $"Failed to find danceId {danceId}");
            // Clear out the cache to force a reload: workaround for possible cache corruption.
            // TODO: Put in the infrastructure to send app insights events when this happens
            Trace.WriteLineIf(TraceLevels.General.TraceError, "Attempting to rebuild cache");

            //DanceStatsManager.ClearCache(null, true);
            return null;
        }

        public DanceStats FromName(string name)
        {
            name = DanceObject.SeoFriendly(name);
            var stats = List.FirstOrDefault(sc => string.Equals(sc.SeoName, name));
            return stats;
        }

        public static async Task<DanceStatsInstance> LoadFromJson(string json,
            DanceMusicCoreService database)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            var instance = JsonConvert.DeserializeObject<DanceStatsInstance>(json, settings);
            if (database != null)
            {
                await instance.FixupStats(database, false);
            }

            Dances.Reset(Dances.Load(instance.GetDanceTypes(), instance.GetDanceGroups()));

            foreach (var dance in instance.List)
            {
                dance.UpdateCompetitionDances();
            }

            return instance;
        }

        public string SaveToJson()
        {
            return JsonConvert.SerializeObject(
                this,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
                    ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new CamelCaseNamingStrategy()
                    }
                });
        }


        public void UpdateSong(Song song)
        {
            lock (_queuedSongs)
            {
                _queuedSongs[song.SongId] = song;

                if (!TopSongs.ContainsKey(song.SongId))
                {
                    if (song.IsNull)
                    {
                        _otherSongs.Remove(song.SongId);
                    }
                    else
                    {
                        _otherSongs[song.SongId] = song;
                    }

                    return;
                }

                if (song.IsNull)
                {
                    TopSongs.Remove(song.SongId);
                }
                else
                {
                    TopSongs[song.SongId] = song;
                }

                foreach (var d in List)
                {
                    var songs = d.TopSongs as List<Song>;
                    if (songs == null)
                    {
                        continue;
                    }

                    var idx = songs.FindIndex(s => s.SongId == song.SongId);
                    if (idx == -1)
                    {
                        continue;
                    }

                    if (song.IsNull)
                    {
                        songs.RemoveAt(idx);
                    }
                    else
                    {
                        songs[idx] = song;
                    }
                }
            }
        }

        public IEnumerable<Song> DequeueSongs()
        {
            lock (_queuedSongs)
            {
                var ret = _queuedSongs.Values.ToList();
                _queuedSongs.Clear();
                return ret;
            }
        }

        public Song FindSongDetails(Guid songId, DanceMusicCoreService dms)
        {
            return TopSongs.GetValueOrDefault(songId) ?? _otherSongs.GetValueOrDefault(songId);
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
    }
}
