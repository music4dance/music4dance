using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace m4dModels;

public class DanceBuilder2(DanceMusicCoreService dms, string source = "default") : 
    DanceBuilder(dms, source)
{

    protected override async Task<IEnumerable<Song>> LoadSongs(IEnumerable<DanceStats> dances, TagManager tagManager)
    {
        List<Song> songs = [];
        foreach (var dance in dances)
        {
            try
            {
                // TopN and MaxWeight
                var songIndex = Dms.GetSongIndex(Source);
                var songFilter = Dms.SearchService.GetSongFilter();
                songFilter.Dances = dance.DanceId;
                songFilter.SortOrder = "Dances";
                var azureFilter = songIndex.AzureParmsFromFilter(songFilter, 10);
                SongIndex.AddAzureCategories(
                    azureFilter,
                    "GenreTags,TempoTags,OtherTags,dance_ALL/TempoTags,dance_ALL/StyleTags,dance_ALL/OtherTags",
                    1000);
                var results = await songIndex.Search(
                    null, azureFilter);
                dance.SetTopSongs(results.Songs);
                songs.AddRange(results.Songs);
                var song = dance.TopSongs.FirstOrDefault();
                var dr = song?.DanceRatings.FirstOrDefault(d => d.DanceId == dance.DanceId);

                if (dr != null)
                {
                    dance.MaxWeight = dr.Weight;
                }

                // SongTags
                dance.SongTags = results.FacetResults == null
                    ? new TagSummary()
                    : new TagSummary(results.FacetResults, tagManager.TagMap);
            }
            catch (Azure.RequestFailedException ex)
            {
                // This is likely because we didn't create an index
                //  for this dance (because there weren't enough songs for the dance)
                Trace.WriteLine(ex.Message);
            }
        }

        return songs;
    }
}
