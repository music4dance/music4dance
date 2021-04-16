using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using m4dModels;

namespace m4d.ViewModels
{
    public class DanceModel : SongListModel
    {
        public DanceModel(Dance dance, string userName, DanceStatsInstance stats, IMapper mapper)
        {
            var ds = stats.Map[dance.Id];
            var songs = ds.TopSongsForUser(userName, stats);
            DanceId = dance.Id;
            DanceName = dance.Name;
            UserName = userName;
            Songs = songs.Select(mapper.Map<SongSparse>).ToList();
            Histories = songs.Select(s => s.GetHistory(mapper)).ToList();
            Description = dance.Description;
            Links = ds.DanceLinks;
            Count = Songs.Count;
            Validate = false;
        }

        public string DanceId { get; set; }

        public string DanceName { get; set; }
        public string Description { get; set; }
        public List<DanceLink> Links { get; set; }
    }
}