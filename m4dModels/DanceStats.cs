using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DanceLibrary;
using Newtonsoft.Json;

namespace m4dModels
{
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
        public long SongCountExplicit { get; set; }

        [JsonProperty]
        public long SongCountImplicit { get; set; }

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

        public IEnumerable<DanceInstance> CompetitionDances { get; private set; }

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

            UpdateCompetitionDances();
        }

        public void UpdateCompetitionDances()
        {
            var dt = DanceObject as DanceType;
            if (dt == null)
            {
                return;
            }

            var competition = dt.Instances
                .Where(di => !string.IsNullOrWhiteSpace(di.CompetitionGroup)).ToList();
            if (competition.Any())
            {
                CompetitionDances = competition;
            }
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

        public void AggregateSongCounts(IReadOnlyDictionary<string, long> tags,
            IReadOnlyDictionary<string, long> inferred)
        {
            // SongCount

            SongCountExplicit = tags.TryGetValue(DanceId, out var expl) ? expl : 0;
            SongCountImplicit = inferred.TryGetValue(DanceId, out var impl) ? impl : 0;
            SongCount = SongCountImplicit + SongCountExplicit;
        }
    }
}
