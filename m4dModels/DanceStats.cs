using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DanceLibrary;

namespace m4dModels
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DanceStats
    {
        public DanceStats()
        {
        }

        [JsonConstructor]
        public DanceStats(string danceId, string danceName, string description, int songCount, int maxWeight, string songTags, IEnumerable<string> topSongs, IEnumerable<DanceStats> children, DanceType danceType, DanceGroup danceGroup)
        {
            Description = description;
            SongCount = songCount;
            MaxWeight = maxWeight;
            SongTags = string.IsNullOrEmpty(songTags) ? null : new TagSummary(songTags);
            // TODO: We've got a chicken and egg problem here - we should be able to load tags first and then pull in stats so that we can have TagMap available here
            TopSongs = topSongs?.Select(s => new Song(s,null)).ToList();

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
        public string DanceId => DanceObject?.Id??"All";

        [JsonProperty]
        public string DanceName => DanceObject?.Name??"All Dances";
        [JsonProperty]
        public string BlogTag => DanceObject?.BlogTag;

        // Properties mirrored from the database Dance object
        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public long SongCount { get; set; }
        [JsonProperty]
        public long SongCountExplicit { get; set; }
        [JsonProperty]
        public long SongCountImplicit { get; set; }
        [JsonProperty]
        public int MaxWeight { get; set; }
        [JsonProperty]
        public TagSummary SongTags { get; set; }

        public TagSummary AggregateSongTags => (Children == null) ? 
            SongTags :
            TagAccumulator.MergeSummaries(Children.Select(c => c.SongTags).Concat(Enumerable.Repeat(SongTags,1)));

        [JsonProperty]
        public virtual ICollection<DanceLink> DanceLinks { get; set; }
        [JsonProperty]
        public IEnumerable<Song> TopSongs { get; set; }

        public ICollection<ICollection<PurchaseLink>> TopSpotify => DanceMusicService.GetPurchaseLinks(ServiceType.Spotify, TopSongs);

        // Structural properties
        public DanceStats Parent { get; set; }
        [JsonProperty]
        public List<DanceStats> Children { get; set; }
        [JsonProperty]
        public string SeoName => DanceObject.SeoFriendly(DanceName);

        // Dance Metadata
        public DanceObject DanceObject {get; set;}

        [JsonProperty]
        public DanceType DanceType => DanceObject as DanceType;

        [JsonProperty]
        public DanceGroup DanceGroup => DanceObject as DanceGroup;

        public Dance Dance => new Dance
        {
            Id = DanceId,
            Description = Description,
            DanceLinks = DanceLinks,
        };

        public IEnumerable<DanceInstance> CompetitionDances { get; private set; }

        public void CopyDanceInfo(Dance dance, bool includeStats, DanceMusicService dms)
        {
            if (dance == null) return;

            Description = dance.Description;
            DanceLinks = dance.DanceLinks;

            var dt = DanceObject as DanceType;
            if (dt == null) return;

            var competion = dt.Instances.Where(di => !string.IsNullOrWhiteSpace(di.CompetitionGroup)).ToList();
            if (competion.Any()) CompetitionDances = competion;
        }

        public void SetParents()
        {
            if (Children == null) return;
            foreach (var c in Children)
            {
                c.Parent = this;
                c.SetParents();
            }
        }

        public void RebuildTopSongs(DanceStatsInstance danceStats)
        {
            TopSongs = TopSongs?.Select(s => new Song(s.Serialize(null), danceStats)).ToList();
        }

        public void AggregateSongCounts(IReadOnlyDictionary<string, long> tags, IReadOnlyDictionary<string, long> inferred)
        {
            // SongCount
            long expl;
            long impl;

            SongCountExplicit = tags.TryGetValue(DanceId, out expl) ? expl : 0;
            SongCountImplicit = inferred.TryGetValue(DanceId, out impl) ? impl : 0;
            SongCount = SongCountImplicit + SongCountExplicit;
        }

        public DanceStats CloneForUser(string userName, DanceStatsInstance danceStats)
        {
            return new DanceStats
            {
                Description = Description,
                SongCount = SongCount,
                MaxWeight = MaxWeight,
                SongTags = SongTags,
                DanceObject = DanceObject,
                Parent = Parent,
                Children = Children,
                TopSongs = TopSongs?.Select(s => new Song(s.SongId, s.Serialize(null), danceStats, userName)).ToList()
            };
        }
    }
}
