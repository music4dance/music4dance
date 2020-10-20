using System.ComponentModel.DataAnnotations;
using System.Linq;
using AutoMapper;
using m4dModels;

namespace m4d.ViewModels
{
    public class ArtistViewModel : SongListModel
    {
        [Key]
        public string Artist { get; set; }

        public static ArtistViewModel Create(
            string name, string user,
            IMapper mapper,
            DanceMusicCoreService.CruftFilter cruft, DanceMusicService dms)
        {
            var songs = dms.FindArtist(name, cruft).Take(500).Select(mapper.Map<SongSparse>).ToList();

            return new ArtistViewModel
            {
                Artist = name,
                UserName = user,
                Filter = mapper.Map<SongFilterSparse>(new SongFilter {Action = "Artist"}),
                Songs = songs
            };
        }
    }
}