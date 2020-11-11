using System.Linq;
using AutoMapper;
using m4dModels;

namespace m4d.ViewModels
{
    public class DanceModel : SongListModel
    {
        public DanceModel(Dance dance, string userName, DanceStatsInstance stats, IMapper mapper)
        {
            DanceId = dance.Id;
            DanceName = dance.Name;
            UserName = userName;
            Songs = stats.Map[dance.Id]
                .TopSongsForUser(userName, stats)
                .Select(mapper.Map<SongSparse>)
                .ToList();
            Description = dance.SmartLinks();
            Count = Songs.Count;
        }

        public string DanceId { get; set; }

        public string DanceName { get; set; }
        public string Description { get; set; }
    }
}