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
        public DanceStats(string danceId, string danceName, string description, int songCount, int maxWeight, string songTags, IEnumerable<string> topSongs, DanceStats[] children, DanceType danceType, DanceGroup danceGroup)
        {
            Description = description;
            SongCount = songCount;
            MaxWeight = maxWeight;
            SongTags = new TagSummary(songTags);
            TopSongs = topSongs.Select(s => new SongDetails(s)).ToList();

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
        public virtual ICollection<DanceLink> DanceLinks { get; set; }
        [JsonProperty]
        public IEnumerable<SongBase> TopSongs { get; set; }

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


        public IEnumerable<DanceInstance> CompetitionDances { get; private set; }

        public void CopyDanceInfo(Dance dance, bool includeStats, DanceMusicService dms)
        {
            if (dance == null) return;

            Description = dance.Description;
            if (includeStats)
            {
                SongCount = dance.SongCount;
                MaxWeight = dance.MaxWeight;
                TopSongs =
                    dance.TopSongs?.OrderBy(ts => ts.Rank).Select(ts => new SongDetails(ts.Song) as SongBase).ToList();
                SongTags = dance.SongTags;
            }
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
    }
}
