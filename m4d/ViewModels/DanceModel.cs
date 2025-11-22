using AutoMapper;

using m4dModels;

namespace m4d.ViewModels;

public class DanceModel : SongListModel
{
    public DanceModel(Dance dance, DanceMusicService database, IMapper mapper)
    {
        var ds = database.DanceStats.Map[dance.Id];
        DanceId = dance.Id;
        DanceName = dance.Name;
        Description = dance.Description;
        Links = ds.DanceLinks;
        SongTags = ds.SongTags;
        DanceTags = ds.DanceTags;
        if (!ds.RefreshTopSongs(database.DanceStats))
        {
            database.ActivityLog.Add(
                new ActivityLog("RefreshTopN", null, new { danceId = ds.DanceId }));
        }
        if (ds.TopSongs != null)
        {
            var songs = ds.TopSongs.ToList();
            Histories = [.. songs.Select(s => s.GetHistory(mapper))];
            SpotifyPlaylist = ds.SpotifyPlaylist;
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
