using Azure.Search.Documents.Models;
using DanceLibrary;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace m4dModels;

public class DanceBuilder(DanceMusicCoreService dms, string source = "default")
{
    private readonly DanceMusicCoreService _dms = dms ?? throw new ArgumentNullException(nameof(dms));
    private readonly string _source = source;

    public async Task<DanceStatsInstance> Build()
    {
        var tagManager = await TagManager.BuildTagManager(_dms, _source);
        var songCounts = await GetSongCounts();
        var groups = await AzureDanceStats(Dances.Instance.AllDanceGroups, songCounts);
        var dances = await AzureDanceStats(Dances.Instance.AllDanceTypes, songCounts);
        var songs = await LoadSongs(dances, tagManager);

        return new DanceStatsInstance(dances, groups, tagManager, songs);
    }

    protected virtual async Task<Dictionary<string, long>> GetSongCounts()
    {
        var facets = await _dms.GetSongIndex(_source)
            .GetTagFacets("DanceTags", 100);

        return IndexDanceFacet(facets["DanceTags"]);
    }

    protected virtual async Task<IEnumerable<DanceStats>> AzureDanceStats(
        IEnumerable<DanceObject> dances,
        IReadOnlyDictionary<string, long> songCounts)
    {
        var stats = new List<DanceStats>();
        await _dms.Context.LoadDances();

        foreach (var dt in dances)
        {
            var scType = InfoFromDance(_dms, dt);
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

    protected virtual DanceStats InfoFromDance(DanceMusicCoreService dms, DanceObject d)
    {
        ArgumentNullException.ThrowIfNull(d);

        var danceStats = new DanceStats
        {
            DanceId = d.Id,
        };

        danceStats.CopyDanceInfo(
            dms.Dances.FirstOrDefault(t => t.Id == d.Id));
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
                var songIndex = dms.GetSongIndex(_source);
                var songFilter = dms.SearchService.GetSongFilter();
                songFilter.Dances = dance.DanceId;
                songFilter.SortOrder = "Dances";
                var azureFilter = songIndex.AzureParmsFromFilter(songFilter, 10);
                SongIndex.AddAzureCategories(
                    azureFilter, "GenreTags,StyleTags,TempoTags,OtherTags", 100);
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
