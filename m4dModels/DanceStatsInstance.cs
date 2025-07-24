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
    public class DanceEnvironment(DanceStatsInstance stats)
    {
        public List<DanceStatsSparse> Dances { get; set; } = [.. stats.GetSparseStats()];
        public List<DanceGroupSparse> Groups { get; set; } = [.. stats.GetGroupsSparse()];
        public List<TagGroup> TagGroups { get; set; }
    }


    public class DanceStatsInstance
    {
        private const string DescriptionPlaceholder =
            "We're busy doing research and pulling together a general description for this dance style. Please check back later for more info.";

        private readonly SongCache _cache = new();
        private readonly IEnumerable<string> _songs;

        public DanceStatsInstance(
            IEnumerable<DanceStats> dances, IEnumerable<DanceStats> groups,
            TagManager tagManager, IEnumerable<Song> songs = null)
        {
            Dances = [.. dances];
            Groups = [.. groups];
            TagManager = tagManager;
            _cache.AddSongs(songs ?? []);
        }

        [JsonConstructor]
        public DanceStatsInstance(
            IEnumerable<DanceStats> dances, IEnumerable<DanceStats> groups,
            IEnumerable<string> cachedSongs, IEnumerable<TagGroup> tagGroups) :
            this(dances, groups, new TagManager(tagGroups))
        {
            _songs = cachedSongs;
        }

        public List<DanceStats> Dances { get; set; }

        public List<DanceStats> Groups { get; set; }

        public List<string> CachedSongs => _cache.Serialize();

        [JsonProperty(PropertyName = "tagGroups")]
        public List<TagGroup> TagGroups => [.. TagManager.TagMap.Values.OrderBy(t => t.Key)];

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

        public static async Task<DanceStatsInstance> BuildInstance(
            DanceMusicCoreService dms, string source)
        {
            var builder = dms.SearchService.NextVersion 
                ? new DanceBuilderNext(dms, source)
                : new DanceBuilder(dms, source);

            var instance = await builder.Build();
            await instance.FixupStats(dms);
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

        public IReadOnlyDictionary<string, long> GetCounts()
        {
            return Dances.ToDictionary(d => d.DanceId, d => d.SongCount);
        }

        public IReadOnlyDictionary<string, DanceMetrics> GetMetrics()
        {
            return Dances.ToDictionary(d => d.DanceId, d => new DanceMetrics
            {
                Id = d.DanceId,
                SongCount = d.SongCount,
                MaxWeight = d.MaxWeight
            });
        }

        public async Task FixupStats(DanceMusicCoreService dms)
        {
            dms.SetStatsInstance(this);

            Map = Dances.Concat(Groups).ToDictionary(ds => ds.DanceId);

            var playlists =
                dms.PlayLists.Where(p => p.Type == PlayListType.SpotifyFromSearch)
                    .Where(p => p.Name != null)
                    .Select(p => new PlaylistMetadata { Id = p.Id, Name = p.Name })
                    .ToDictionary(m => m.Name, m => m);

            var newDances = new List<string>();

            if (_songs != null && _songs.Any()) {
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
                group.Children = [.. group.DanceGroup.DanceIds.Where(id => Map.ContainsKey(id)).Select(id => Map[id])];
                group.SongTags = TagAccumulator.MergeSummaries(
                    group.Children.Select(c => c.SongTags));
                // TODO: At some point we should ask azure search for this, since 
                //   we're double-counting by the current method.
                group.DanceTags = TagAccumulator.MergeSummaries(
                    group.Children.Select(c => c.DanceTags));
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
                ds.SetTopSongs([]);
                ds.SongTags = new TagSummary();
                ds.DanceTags = new TagSummary();
            }

            if (saveChanges)
            {
                await dms.SaveChanges();
            }

            if (newDances.Count > 0)
            {
                await dms.SongIndex.UpdateIndex(newDances);
            }
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

        public List<Song> ListFromCache(IEnumerable<string> ids)
        {
            var songs = ids.Select(id => _cache.FindSongDetails(new Guid(id))).ToList();
            if (songs.Count != ids.Count())
            {
                Trace.WriteLine($"Failed to find all songs in cache: {string.Join(",", ids)}");
                return null;
            }
            return songs;
        }

        public static async Task<DanceStatsInstance> LoadFromJson(string json,
            DanceMusicCoreService database, IDanceStatsManager manager = null)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            var instance = JsonConvert.DeserializeObject<DanceStatsInstance>(json, settings) ?? throw new Exception($"Unable to deserialize dance stats instance: {json}");
            if (database != null)
            {
                await instance.FixupStats(database);
            }

            manager ??= database.DanceStatsManager;
            await manager.InitializeDanceLibrary();

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
