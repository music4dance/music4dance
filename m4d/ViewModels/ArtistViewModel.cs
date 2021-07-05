using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using m4dModels;

namespace m4d.ViewModels
{
    public class ArtistViewModel : SongListModel
    {
        [Key]
        public string Artist { get; set; }

        public static async Task<ArtistViewModel> Create(
            string name, string user,
            IMapper mapper,
            DanceMusicCoreService.CruftFilter cruft, DanceMusicService dms)
        {
            var list = (await dms.FindArtist(name, cruft)).Take(500);

            return new ArtistViewModel
            {
                Artist = name,
                UserName = user,
                Filter = mapper.Map<SongFilterSparse>(new SongFilter { Action = "Artist" }),
                Songs = list.Select(mapper.Map<SongSparse>).ToList(),
                Histories = list.Select(s => s.GetHistory(mapper)).ToList()
            };
        }
    }
}
