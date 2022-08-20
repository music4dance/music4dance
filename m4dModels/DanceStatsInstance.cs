using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DanceLibrary;
using m4dModels.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace m4dModels
{
    public class DanceEnvironment
    {
        public DanceEnvironment(DanceStatsInstance stats)
        {
            Dances = stats.GetSparseStats().ToList();
            Groups = stats.GetGroupsSparse().ToList();
        }

        public List<DanceStatsSparse> Dances { get; set; }
        public List<DanceGroupSparse> Groups { get; set; }
        public List<TagGroup> TagGroups { get; set; }
    }


    public class DanceStatsInstance
    {
        private const string DescriptionPlaceholder =
            "We're busy doing research and pulling together a general description for this dance style. Please check back later for more info.";

        private readonly SongCache _cache = new();
        private readonly IEnumerable<string> _songs;

        [JsonConstructor]
        public DanceStatsInstance(
            IEnumerable<DanceStats> dances, IEnumerable<DanceStats> groups,
            IEnumerable<string> cachedSongs, IEnumerable<TagGroup> tagGroups) :
            this(dances, groups, new TagManager(tagGroups))
        {
            _songs = cachedSongs;
        }

        public DanceStatsInstance(
            IEnumerable<DanceStats> dances, IEnumerable<DanceStats> groups,
            TagManager tagManager)
        {
            TagManager = tagManager;
            Dances = dances.ToList();
            Groups = groups.ToList();
        }

        public List<DanceStats> Dances { get; set; }

        public List<DanceStats> Groups { get; set; }

        public List<string> CachedSongs => _cache.Serialize();

        [JsonProperty(PropertyName = "tagGroups")]
        public List<TagGroup> TagGroups => TagManager.TagMap.Values.OrderBy(t => t.Key).ToList();

        [JsonIgnore]
        public TagManager TagManager { get; set; }

        [JsonIgnore]
        public Dictionary<string, DanceStats> Map { get; private set; }

        public void UpdateSong(Song song)
        {
            _cache.UpdateSong(song);
        }

        public IEnumerable<Song> DequeueSongs()
        {
            return _cache.DequeueSongs();
        }

        public Song FindSongDetails(Guid songId)
        {
            return _cache.FindSongDetails(songId);
        }

        public static async Task<DanceStatsInstance> BuildInstance(TagManager tagManager,
            IEnumerable<DanceStats> groups, IEnumerable<DanceStats> dances,
            DanceMusicCoreService dms, string source)
        {
            var instance = new DanceStatsInstance(dances, groups, tagManager);
            await instance.FixupStats(dms, true, source);
            return instance;
        }

        public IEnumerable<DanceStatsSparse> GetSparseStats()
        {
            return Dances.Select(s => new DanceStatsSparse(s));
        }

        public IEnumerable<DanceGroupSparse> GetGroupsSparse()
        {
            return Groups.Select(t => new DanceGroupSparse(t));
        }

        public async Task FixupStats(DanceMusicCoreService dms, bool reloadSongs,
            string source = "default")
        {
            dms.SetStatsInstance(this);

            Map = Dances.Concat(Groups).ToDictionary(ds => ds.DanceId);

            var playlists =
                dms.PlayLists.Where(p => p.Type == PlayListType.SpotifyFromSearch)
                    .Where(p => p.Name != null)
                    .Select(p => new PlaylistMetadata { Id = p.Id, Name = p.Name })
                    .ToDictionary(m => m.Name, m => m);

            var newDances = new List<string>();

            if (reloadSongs)
            {
                foreach (var dance in Dances)
                {
                    try
                    {
                        // TopN and MaxWeight
                        var songIndex = dms.GetSongIndex(source);
                        var filter =
                            songIndex.AzureParmsFromFilter(
                                new SongFilter
                                    { Dances = dance.DanceId, SortOrder = "Dances" }, 10);
                        SongIndex.AddAzureCategories(
                            filter, "GenreTags,StyleTags,TempoTags,OtherTags", 100);
                        var results = await songIndex.Search(
                            null, filter);
                        dance.SetTopSongs(results.Songs);
                        _cache.AddSongs(results.Songs);
                        var song = dance.TopSongs.FirstOrDefault();
                        var dr = song?.DanceRatings.FirstOrDefault(d => d.DanceId == dance.DanceId);

                        if (dr != null)
                        {
                            dance.MaxWeight = dr.Weight;
                        }

                        // SongTags
                        dance.SongTags = results.FacetResults == null
                            ? new TagSummary()
                            : new TagSummary(results.FacetResults, TagManager.TagMap);
                    }
                    catch (Azure.RequestFailedException ex)
                    {
                        // This is likely because we didn't create an index
                        //  for this dance (because there weren't enough songs for the dance)
                        Trace.WriteLine(ex.Message);
                    }
                }
            }
            else
            {
                await _cache.LoadSongs(_songs, dms);
                foreach (var dance in Dances)
                {
                    dance.RestoreTopSongs(_cache);
                }
            }

            foreach (var dance in Dances)
            {
                if (playlists.TryGetValue(dance.DanceName, out var metadata))
                {
                    dance.SpotifyPlaylist = metadata.Id;
                }
            }

            foreach (var group in Groups)
            {
                group.Children = group.DanceGroup.DanceIds.Select(id => Map[id]).ToList();
                group.SongTags = TagAccumulator.MergeSummaries(
                    group.Children.Select(c => c.SongTags));
                // TODO: At some point we should as azure search for this, since 
                //   we're double-counting by the current method.
                group.SongCount = group.Children.Sum(d => d.SongCount);
                group.MaxWeight = group.Children.Max(d => d.MaxWeight);
            }

            var saveChanges = false;
            foreach (var ds in
                Dances.Concat(Groups).Where(s => s.Dance.Description == null))
            {
                var dance = await dms.Dances.FindAsync(ds.DanceId);
                if (dance == null)
                {
                    dms.Dances.Add(
                        new Dance
                        {
                            Id = ds.DanceId,
                            Description = DescriptionPlaceholder
                        });
                }
                else
                {
                    dance.Description = DescriptionPlaceholder;
                }

                saveChanges = true;

                newDances.Add(ds.DanceId);
                ds.SetTopSongs(new List<Song>());
                ds.SongTags = new TagSummary();
            }

            if (saveChanges)
            {
                await dms.SaveChanges();
            }

            await dms.SongIndex.UpdateIndex(newDances);
        }

        public DanceStats FromId(string danceId)
        {
            if (danceId.Length > 3)
            {
                danceId = danceId[..3];
            }

            if (Map.TryGetValue(danceId.ToUpper(), out var sc))
            {
                return sc;
            }

            if (DanceLibrary.Dances.Instance.DanceFromId(danceId.ToUpper()) == null)
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
            var stats = Dances.Concat(Groups).FirstOrDefault(sc => string.Equals(sc.SeoName, name));
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

            if (instance == null)
            {
                throw new Exception($"Unable to deserialize dance stats instance: {json}");
            }

            if (database != null)
            {
                await instance.FixupStats(database, false);
            }
            
            DanceLibrary.Dances.Reset(
                DanceLibrary.Dances.Load(
                    instance.Dances.Select(d => d.DanceType).ToList(),
                    instance.Groups.Select(d => d.DanceGroup).ToList()));

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

        public void ClearCache()
        {
            _jsonEnvironment = null;
            _jsonTagDatabase = null;
        }

        public string GetJsonDanceEnvironment()
        {
            if (_jsonEnvironment == null)
            {
                var environment = new DanceEnvironment(this);
                _jsonEnvironment = JsonConvert.SerializeObject(environment, JsonHelpers.CamelCaseSerializer);

            }
            return _jsonEnvironment;
        }
        private string _jsonEnvironment;

        public string GetJsonTagDatabse()
        {
            if (_jsonTagDatabase == null)
            {
                var tagDatabase = TagGroups
                    .Where(g => g.Category != "Dance" && g.PrimaryId == null)
                    .Select(g => new { g.Key, g.Count });

                _jsonTagDatabase = JsonConvert.SerializeObject(tagDatabase, JsonHelpers.CamelCaseSerializer);
            }
            return _jsonTagDatabase;
        }
        private string _jsonTagDatabase;
    }
}
