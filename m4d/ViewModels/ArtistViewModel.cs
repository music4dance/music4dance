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
        var filter = dms.SearchService.GetSongFilter();
        filter.Action = "Artist";

        return new ArtistViewModel
        {
            Artist = name,
            Filter = mapper.Map<SongFilterSparse>(filter),
            Histories = [.. list.Select(s => s.GetHistory(mapper))]
        };
    }
}
