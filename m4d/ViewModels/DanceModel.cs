using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using m4dModels;

namespace m4d.ViewModels
{
    public class DanceModel : SongListModel
    {
        public DanceModel(Dance dance, string userName, DanceMusicService database, IMapper mapper)
        {
            var ds = database.DanceStats.Map[dance.Id];
            var songs = ds.TopSongs.ToList();
            DanceId = dance.Id;
            DanceName = dance.Name;
            UserName = userName;
            Histories = songs.Select(s => s.GetHistory(mapper)).ToList();
            Description = dance.Description;
            Links = ds.DanceLinks;
            Count = songs.Count;
            Validate = false;
        }

        public string DanceId { get; set; }

        public string DanceName { get; set; }
        public string Description { get; set; }
        public List<DanceLink> Links { get; set; }
    }
}
