using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using m4dModels;

namespace m4d.ViewModels
{
    public class DanceModel : SongListModel
    {
        public DanceModel(Dance dance, DanceMusicService database, IMapper mapper)
        {
            var ds = database.DanceStats.Map[dance.Id];
            var songs = ds.TopSongs.ToList();
            DanceId = dance.Id;
            DanceName = dance.Name;
            Histories = songs.Select(s => s.GetHistory(mapper)).ToList();
            Description = dance.Description;
            SpotifyPlaylist = ds.SpotifyPlaylist;
            Links = ds.DanceLinks;
            Count = songs.Count;
        }

        public string DanceId { get; set; }

        public string DanceName { get; set; }
        public string Description { get; set; }
        public string SpotifyPlaylist { get; set; }
        public List<DanceLink> Links { get; set; }
    }
}
