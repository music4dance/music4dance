using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using DanceLibrary;

namespace m4dModels
{
    [JsonObject(MemberSerialization.OptIn)]

    public class DanceStats
    {
        [JsonProperty]
        public string DanceId => DanceObject?.Id??"All";

        [JsonProperty]
        public string DanceName => DanceObject?.Name??"All Dances";
        [JsonProperty]
        public string BlogTag => DanceObject.BlogTag;

        // Properties mirrored from the database Dance object
        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public int SongCount { get; set; }
        [JsonProperty]
        public int MaxWeight { get; set; }
        [JsonProperty]
        public TagSummary SongTags { get; set; }
        public virtual ICollection<DanceLink> DanceLinks { get; set; }
        [JsonProperty]
        public IEnumerable<SongBase> TopSongs { get; set; }
        public ICollection<ICollection<PurchaseLink>> TopSpotify { get; set; }

        // Structural properties
        public DanceStats Parent { get; set; }
        [JsonProperty]
        public List<DanceStats> Children { get; set; }
        [JsonProperty]
        public string SeoName => DanceObject.SeoFriendly(DanceName);

        // Dance Metadata
        [JsonProperty]
        public DanceObject DanceObject {get; set;}

        public IReadOnlyList<CompetitionDance> CompetitionDances => _competitionDances;

        [JsonProperty]
        public IList<string> CompetitionDanceIds => _competitionDances?.Select(d => d.SpecificDance.Id).ToList();

        public void AddCompetitionDance(CompetitionDance competitionDance)
        {
            lock (this)
            {
                if (_competitionDances == null)
                {
                    _competitionDances = new List<CompetitionDance>();
                }
                _competitionDances.Add(competitionDance);
            }
        }

        public void CopyDanceInfo(Dance dance, DanceMusicService dms)
        {
            if (dance == null) return;

            Description = dance.Description;
            SongCount = dance.SongCount;
            MaxWeight = dance.MaxWeight;
            TopSongs =
                dance.TopSongs?.OrderBy(ts => ts.Rank).Select(ts => new SongDetails(ts.Song) as SongBase).ToList();
            TopSpotify = dms.GetPurchaseLinks(ServiceType.Spotify, TopSongs);
            SongTags = dance.SongTags;
            DanceLinks = dance.DanceLinks;
        }

        private List<CompetitionDance> _competitionDances;

    }
}
