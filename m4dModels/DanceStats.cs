using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using DanceLibrary;

using Newtonsoft.Json;

namespace m4dModels
{
    public class DanceStatsSparse(DanceStats stats) : DanceType(stats.DanceType)
    {
        public string Description { get; set; } = stats.Description;

        public long SongCount { get; set; } = stats.SongCount;

        public int MaxWeight { get; set; } = stats.MaxWeight;

        public TagSummary SongTags { get; set; } = stats.SongTags;
        public TagSummary DanceTags { get; set; } = stats.DanceTags;
    }

    public class DanceGroupSparse(DanceStats stats)
    {
        public string Id { get; set; } = stats.DanceId;

        public string Name { get; set; } = stats.DanceName;

        public string Description { get; set; } = stats.Description;

        public string BlogTag { get; set; } = stats.BlogTag;

        public IList<string> DanceIds { get; set; } = [.. stats.Children.Select(d => d.DanceId)];
    }

    public class DanceStats
    {
        private List<Song> _topSongs;

        public DanceStats()
        {
        }

        [JsonConstructor]
        public DanceStats(string danceId, string description, int songCount,
            int maxWeight, string songTags, string danceTags, IEnumerable<string> songIds)
        {
            DanceId = danceId;
            Description = description;
            SongCount = songCount;
            MaxWeight = maxWeight;
            SongTags = string.IsNullOrEmpty(songTags) ? null : new TagSummary(songTags);
            DanceTags = string.IsNullOrEmpty(danceTags) ? null : new TagSummary(danceTags);
            SongIds = songIds?.ToList();
        }

        public string DanceId { get; set; }

        public string DanceName => DanceObject.Name;

        public string BlogTag => DanceObject?.BlogTag;

        public List<string> SongIds { get; set; }

        public long SongCount { get; set; }

        public int MaxWeight { get; set; }

        public TagSummary SongTags { get; set; }
        public TagSummary DanceTags { get; set; }

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
            _topSongs = [.. songs];
            SongIds = [.. _topSongs.Select(s => s.SongId.ToString())];
        }

        public bool RefreshTopSongs(DanceStatsInstance stats)
        {
            if (SongIds == null || SongIds.Count == 0)
            {
                return true;
            }
            var songs = stats.ListFromCache(SongIds);
            if (songs == null)
            {
                return false;
            }
            _topSongs = songs;
            return true;
        }

        public void RestoreTopSongs(SongCache songs)
        {
            _topSongs = [.. SongIds.Select(id => songs.FindSongDetails(new Guid(id)))];
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
