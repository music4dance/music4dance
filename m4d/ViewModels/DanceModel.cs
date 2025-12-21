using AutoMapper;

namespace m4d.ViewModels;

public class DanceModel : SongListModel
{
    public DanceModel(DanceStats danceStats, DanceStatsInstance statsInstance, IMapper mapper)
    {
        DanceId = danceStats.DanceId;
        DanceName = danceStats.DanceName;
        Description = danceStats.Description;
        Links = danceStats.DanceLinks;
        SongTags = danceStats.SongTags;
        DanceTags = danceStats.DanceTags;

        if (!danceStats.RefreshTopSongs(statsInstance))
        {
            // Note: ActivityLog requires database access, skipping when DB unavailable
            // This is a non-critical logging operation
        }

        if (danceStats.TopSongs != null)
        {
            var songs = danceStats.TopSongs.ToList();
            Histories = [.. songs.Select(s => s.GetHistory(mapper))];
            SpotifyPlaylist = danceStats.SpotifyPlaylist;
        }
    }

    public string DanceId { get; set; }

    public string DanceName { get; set; }
    public string Description { get; set; }
    public string SpotifyPlaylist { get; set; }
    public TagSummary SongTags { get; set; }
    public TagSummary DanceTags { get; set; }
    public List<DanceLink> Links { get; set; }
}
