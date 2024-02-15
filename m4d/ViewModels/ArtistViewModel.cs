using System.ComponentModel.DataAnnotations;
using AutoMapper;
using m4dModels;

namespace m4d.ViewModels;

public class ArtistViewModel : SongListModel
{
    [Key]
    public string Artist { get; set; }

    public static async Task<ArtistViewModel> Create(
        string name,
        IMapper mapper,
        CruftFilter cruft, DanceMusicService dms)
    {
        var list = (await dms.SongIndex.FindArtist(name, cruft)).Take(500);

        return new ArtistViewModel
        {
            Artist = name,
            Filter = mapper.Map<SongFilterSparse>(new SongFilter { Action = "Artist" }),
            Histories = list.Select(s => s.GetHistory(mapper)).ToList()
        };
    }
}
