using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public string Description { get; set; }

        public long SongCount { get; set; }

        public int MaxWeight { get; set; }

        public TagSummary SongTags { get; set; }
    }

    public class DanceGroupSparse
    {
        public DanceGroupSparse(DanceStats stats)
        {
            Id = stats.DanceId;
            Name = stats.DanceName;
            Description = stats.Description;
            BlogTag = stats.BlogTag;
            DanceIds = stats.Children.Select(d => d.DanceId).ToList();
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string BlogTag { get; set; }

        public IList<string> DanceIds { get; set; }
    }

    public class DanceStats
    {
        private List<Song> _topSongs;

        public DanceStats()
        {
        }

        [JsonConstructor]
        public DanceStats(string danceId, string description, int songCount,
            int maxWeight, string songTags, IEnumerable<string> songIds)
        {
            DanceId = danceId;
            Description = description;
            SongCount = songCount;
            MaxWeight = maxWeight;
            SongTags = string.IsNullOrEmpty(songTags) ? null : new TagSummary(songTags);
            SongIds = songIds?.ToList();
        }

        public string DanceId { get; set; }

        public string DanceName => DanceObject.Name;

        public string BlogTag => DanceObject?.BlogTag;

        public List<string> SongIds { get; set; }

        public long SongCount { get; set; }

        public int MaxWeight { get; set; }

        public TagSummary SongTags { get; set; }

        // Properties mirrored from the database Dance object
        public string Description { get; set; }

        public string SpotifyPlaylist { get; set; }

        public List<DanceLink> DanceLinks { get; set; }

        [JsonIgnore]
        public IEnumerable<Song> TopSongs => _topSongs;

        [JsonIgnore]
        public List<DanceStats> Children { get; set; }

        [JsonIgnore]
        public string SeoName => DanceObject.SeoFriendly(DanceName);

        // Dance Metadata
        [JsonIgnore]
        public DanceObject DanceObject => Dances.Instance.DanceFromId(DanceId);

        [JsonIgnore]
        public DanceType DanceType => DanceObject as DanceType;

        [JsonIgnore]
        public DanceGroup DanceGroup => DanceObject as DanceGroup;

        [JsonIgnore]
        public Dance Dance => new()
        {
            Id = DanceId,
            Description = Description,
            DanceLinks = DanceLinks
        };

        public void SetTopSongs(IEnumerable<Song> songs)
        {
            _topSongs = songs.ToList();
            SongIds = _topSongs.Select(s => s.SongId.ToString()).ToList();
        }

        public bool RefreshTopSongs(DanceStatsInstance stats)
        {
            if (SongIds == null || !SongIds.Any())
            {
                return true;
            }
            var songs = stats.ListFromCache(SongIds);
            if (songs == null) {
                return false;
            }
            _topSongs = songs;
            return true;
        }

        public void RestoreTopSongs(SongCache songs)
        {
            _topSongs = SongIds.Select(id => songs.FindSongDetails(new Guid(id))).ToList();
            if (_topSongs.Any(s => s == null))
            {
                Trace.WriteLine($"Bad restore of top songs for {DanceId}");
            }
        }

        public void CopyDanceInfo(Dance dance)
        {
            if (dance == null)
            {
                return;
            }

            Description = dance.Description;
            DanceLinks = dance.DanceLinks;
        }

        public void AggregateSongCounts(IReadOnlyDictionary<string, long> tags)
        {
            SongCount = tags.TryGetValue(DanceId, out var count) ? count : 0;
        }
    }
}
