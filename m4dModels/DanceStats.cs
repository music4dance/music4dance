using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using DanceLibrary;

namespace m4dModels
{
    [JsonObject(MemberSerialization.OptIn)]

    public class DanceStats
    {
        public string DanceId => DanceObject?.Id??"All";

        public string DanceName => DanceObject?.Name??"All Dances";
        public string BlogTag => DanceObject.BlogTag;

        // Properties mirrored from the database Dance object
        public string Description { get; set; }

        public int SongCount { get; set; }
        public int MaxWeight { get; set; }
        public TagSummary SongTags { get; set; }
        public virtual ICollection<DanceLink> DanceLinks { get; set; }
        public IEnumerable<SongBase> TopSongs { get; set; }
        public ICollection<ICollection<PurchaseLink>> TopSpotify { get; set; }

        // Structural properties
        public DanceStats Parent { get; set; }
        public List<DanceStats> Children { get; set; }
        public string SeoName => DanceObject.SeoFriendly(DanceName);

        // Dance Metadata
        public DanceObject DanceObject {get; set;}

        public IReadOnlyList<CompetitionDance> CompetitionDances => _competitionDances;

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
