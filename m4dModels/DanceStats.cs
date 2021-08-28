using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DanceLibrary;
using Newtonsoft.Json;

namespace m4dModels
{
    public class DanceStatsSparse : DanceType
    {
        public DanceStatsSparse(DanceStats stats) : base(stats.DanceType)
        {
            Description = stats.Description;
            SongCount = stats.SongCount;
            SongTags = stats.SongTags;
            MaxWeight = stats.MaxWeight;
        }

        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public long SongCount { get; set; }

        [JsonProperty]
        public int MaxWeight { get; set; }

        [JsonProperty]
        public TagSummary SongTags { get; set; }
    }

    public class DanceGroupSparse
    {
        public DanceGroupSparse(DanceStats stats)
        {
            Id = stats.DanceId;
            Name = stats.DanceName;
            Description = stats.Description;
            SongTags = stats.SongTags;
            BlogTag = stats.BlogTag;
            DanceIds = stats.Children.Select(d => d.DanceId).ToList();
        }

        [JsonProperty]
        public string Id { get; set; }

        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public TagSummary SongTags { get; set; }

        [JsonProperty]
        public string BlogTag { get; set; }

        [JsonProperty]
        public IList<string> DanceIds { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DanceStats
    {
        private readonly List<string> _songStrings;

        private List<Song> _topSongs;

        public DanceStats()
        {
        }

        [JsonConstructor]
        public DanceStats(string danceId, string danceName, string description, int songCount,
            int maxWeight, string songTags, IEnumerable<string> topSongs,
            IEnumerable<DanceStats> children, DanceType danceType, DanceGroup danceGroup)
        {
            Description = description;
            SongCount = songCount;
            MaxWeight = maxWeight;
            SongTags = string.IsNullOrEmpty(songTags) ? null : new TagSummary(songTags);
            _songStrings = topSongs?.ToList();

            if (danceType != null)
            {
                DanceObject = danceType;
            }
            else if (danceGroup != null)
            {
                DanceObject = danceGroup;

                Children = children.ToList();
            }

            Debug.Assert(danceId == DanceObject.Id && danceName == DanceObject.Name);
        }

        [JsonProperty]
        public string DanceId => DanceObject?.Id ?? "All";

        [JsonProperty]
        public string DanceName => DanceObject?.Name ?? "All Dances";

        [JsonProperty]
        public string BlogTag => DanceObject?.BlogTag;

        // Properties mirrored from the database Dance object
        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public long SongCount { get; set; }

        [JsonProperty]
        public int MaxWeight { get; set; }

        [JsonProperty]
        public TagSummary SongTags { get; set; }

        [JsonProperty]
        public string SpotifyPlaylist { get; set; }

        public TagSummary AggregateSongTags => Children == null
            ? SongTags
            : TagAccumulator.MergeSummaries(
                Children.Select(c => c.SongTags)
                    .Concat(Enumerable.Repeat(SongTags, 1)));

        [JsonProperty]
        public List<DanceLink> DanceLinks { get; set; }

        [JsonProperty]
        public IEnumerable<Song> TopSongs => _topSongs;

        // Structural properties
        public DanceStats Parent { get; set; }

        [JsonProperty]
        public List<DanceStats> Children { get; set; }

        [JsonProperty]
        public string SeoName => DanceObject.SeoFriendly(DanceName);

        // Dance Metadata
        public DanceObject DanceObject { get; set; }

        [JsonProperty]
        public DanceType DanceType => DanceObject as DanceType;

        [JsonProperty]
        public DanceGroup DanceGroup => DanceObject as DanceGroup;

        public Dance Dance => new Dance
        {
            Id = DanceId,
            Description = Description,
            DanceLinks = DanceLinks
        };

        public void SetTopSongs(IEnumerable<Song> songs)
        {
            _topSongs = songs.ToList();
        }

        public async Task LoadSongs(DanceMusicCoreService dms)
        {
            _topSongs = await dms.CreateSongs(_songStrings);
        }

        public void CopyDanceInfo(Dance dance, bool includeStats, DanceMusicCoreService dms)
        {
            if (dance == null)
            {
                return;
            }

            Description = dance.Description;
            DanceLinks = dance.DanceLinks;
        }

        public void SetParents()
        {
            if (Children == null)
            {
                return;
            }

            foreach (var c in Children)
            {
                c.Parent = this;
                c.SetParents();
            }
        }

        public void AggregateSongCounts(IReadOnlyDictionary<string, long> tags)
        {
            SongCount = tags.TryGetValue(DanceId, out var count) ? count : 0;
        }
    }
}
