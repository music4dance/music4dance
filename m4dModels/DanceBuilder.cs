using Azure.Search.Documents.Models;

using DanceLibrary;

using System.Diagnostics;

using FacetResults =
    System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<
        Azure.Search.Documents.Models.FacetResult>>;

namespace m4dModels;

public class DanceBuilder(DanceMusicCoreService dms, string source)
{
    protected DanceMusicCoreService Dms => dms ?? throw new ArgumentNullException(nameof(dms));

    protected virtual IEnumerable<string> GlobalFacets =>
    [
        "GenreTags", "TempoTags", "OtherTags",
        "dance_ALL/StyleTags", "dance_ALL/TempoTags", "dance_ALL/OtherTags"
    ];

    protected virtual IEnumerable<string> GetDanceFacets(string danceId) =>
        GlobalFacets.Select(f => f.Replace(@"dance_ALL/", $"dance_{danceId}/"));

    public async Task<DanceStatsInstance> Build()
    {
        var tagManager = await TagManager.BuildTagManager(Dms, GlobalFacets, source);
        var songCounts = await GetSongCounts();
        var groups = await AzureDanceStats(Dances.Instance.AllDanceGroups, songCounts);
        var dances = await AzureDanceStats(Dances.Instance.AllDanceTypes, songCounts);
        var songs = await LoadSongs(dances, tagManager);

        return new DanceStatsInstance(dances, groups, tagManager, songs);
    }

    protected virtual async Task<Dictionary<string, long>> GetSongCounts()
    {
        var facets = await Dms.GetSongIndex(source)
            .GetTagFacets("DanceTags", 100);

        return IndexDanceFacet(facets["DanceTags"]);
    }

    protected virtual async Task<IEnumerable<DanceStats>> AzureDanceStats(
        IEnumerable<DanceObject> dances,
        IReadOnlyDictionary<string, long> songCounts)
    {
        var stats = new List<DanceStats>();
        _ = await Dms.Context.LoadDances();

        foreach (var dt in dances)
        {
            var scType = InfoFromDance(dt);
            scType.AggregateSongCounts(songCounts);
            stats.Add(scType);
        }

        return stats;
    }

    protected virtual Dictionary<string, long> IndexDanceFacet(IEnumerable<FacetResult> facets)
    {
        var ret = new Dictionary<string, long>();

        foreach (var facet in facets)
        {
            var d = Dances.Instance.DanceFromName((string)facet.Value);
            if (d == null || !facet.Count.HasValue)
            {
                continue;
            }

            ret[d.Id] = facet.Count.Value;
        }

        return ret;
    }

    protected virtual DanceStats InfoFromDance(DanceObject d)
    {
        ArgumentNullException.ThrowIfNull(d);

        var danceStats = new DanceStats
        {
            DanceId = d.Id,
        };

        danceStats.CopyDanceInfo(
            Dms.Dances.FirstOrDefault(t => t.Id == d.Id));
        return danceStats;
    }

    protected async Task<IEnumerable<Song>> LoadSongs(IEnumerable<DanceStats> dances, TagManager tagManager)
    {
        List<Song> songs = [];
        foreach (var dance in dances)
        {
            try
            {
                // TopN and MaxWeight
                var songIndex = Dms.GetSongIndex(source);
                var songFilter = Dms.SearchService.GetSongFilter();
                songFilter.Dances = dance.DanceId;
                songFilter.SortOrder = "Dances";
                var azureFilter = songIndex.AzureParmsFromFilter(songFilter, 10);
                SongIndex.AddAzureCategories(azureFilter,
                    string.Join(",", GetDanceFacets(dance.DanceId)), 100);
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

                dance.SongTags = results.FacetResults == null
                    ? new TagSummary()
                    : new TagSummary(ExtractSongFacets(results.FacetResults), tagManager.TagMap);

                dance.DanceTags = results.FacetResults == null
                    ? new TagSummary()
                    : new TagSummary(ExtractDanceFacets(results.FacetResults), tagManager.TagMap);

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

    protected virtual FacetResults ExtractSongFacets(FacetResults facets)
    {
        return facets
            .Where(f => !f.Key.StartsWith("dance_"))
            .ToDictionary(f => f.Key, f => f.Value);
    }

    protected virtual FacetResults ExtractDanceFacets(FacetResults facets)
    {
        return facets
            .Where(f => f.Key.StartsWith("dance_"))
            .ToDictionary(f => f.Key, f => f.Value);
    }
}
