using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DanceLibrary;
using Microsoft.Azure.Search.Models;

namespace m4dModels
{
    public interface IDanceStatsManager
    {
        DateTime LastUpdate { get; }
        string Source { get; }

        void ClearCache(DanceMusicCoreService dms, bool fromStore);
        void ReloadDances(DanceMusicCoreService dms);

        DanceStatsInstance LoadFromAzure(DanceMusicCoreService dms, string source = "default", bool save = false, bool tagsOnly = false);

        DanceStatsInstance Instance { get; }

        IList<DanceStats> DanceStats { get; }

        IList<DanceStats> FlatDanceStats { get; }

        void Initialize(DanceMusicCoreService dms);

    }


    public class DanceStatsManager : IDanceStatsManager
    {
        public DateTime LastUpdate { get; private set; }
        public string Source { get; private set; }

        private string AppData { get; }

        public DanceStatsManager()
        {

        }

        public DanceStatsManager( string appData)
        {
            AppData = appData;
        }

        #region Access

        public DanceStatsInstance Instance { get; private set; }

        public void Initialize(DanceMusicCoreService dms)
        {
            if (Instance != null)
            {
                throw new Exception("Should only Initialize DanceStatsManager once");
            }

            Instance = LoadFromAppData(dms) ?? LoadFromAzure(dms, "default", true);
        }

        public IList<DanceStats> FlatDanceStats => Instance.List;

        public IList<DanceStats> DanceStats => Instance.Tree;
        #endregion

        #region Building

        public void ClearCache(DanceMusicCoreService dms, bool fromStore)
        {
            var instance = fromStore 
                ? LoadFromAzure(dms, "default", true)
                : LoadFromAppData(dms);

            if (instance != null)
            {
                Instance = instance;
                ClearAssociates();
            }
        }

        private void ClearAssociates()
        {
            DanceMusicCoreService.BlowTagCache();
            Song.ResetIndex();
        }

        public void ReloadDances(DanceMusicCoreService dms)
        {
            foreach (var dance in dms.Context.LoadDances())
            {
                if (Instance.Map.TryGetValue(dance.Id, out var danceStats))
                {
                    danceStats.CopyDanceInfo(dance, false, dms);
                }
            }

            LastUpdate = DateTime.Now;
            Source = Source + " + reload";
        }

        private DanceStatsInstance LoadFromAppData(DanceMusicCoreService dms)
        {
            if (AppData == null) return null;

            var path = System.IO.Path.Combine(AppData, "dance-tag-stats.json");
            if (!System.IO.File.Exists(path)) return null;

            LastUpdate = DateTime.Now;
            Source = "AppData";
            Instance = DanceStatsInstance.LoadFromJson(System.IO.File.ReadAllText(path), dms);
            return Instance;
        }

        private DanceStatsInstance LoadFromStore(DanceMusicCoreService dms, bool save)
        {
            return LoadFromAzure(dms,"default",save);
        }

        public DanceStatsInstance LoadFromJson(string json, DanceMusicCoreService dms)
        {
            Source = "Json";
            Instance = DanceStatsInstance.LoadFromJson(json, dms);
            return Instance;
        }

        public DanceStatsInstance LoadFromAzure(
            DanceMusicCoreService dms, string source = "default", bool save = false, bool tagsOnly = false)
        {
            var copy = (tagsOnly && Instance != null);
            if (save && !copy)
            {
                Dances.Reset();
            }
            var instance = new DanceStatsInstance(
                new TagManager(dms, source),
                copy ? Instance.Tree : AzureDanceStats(dms, source), 
                copy ? null : dms, source);
            if (!save) return instance;

            LastUpdate = DateTime.Now;
            Source = "Azure";
            SaveToAppData(instance);

            Instance = instance;
            // This will save any tag types that were created via the load from azure

            dms.UpdateAzureIndex(null, source);
            return instance;
        }

        private void SaveToAppData(DanceStatsInstance instance)
        {
            if (AppData == null) return;

            var json = instance.SaveToJson();
            var path = System.IO.Path.Combine(AppData, "dance-tag-stats.json");
            Directory.CreateDirectory(AppData);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private IEnumerable<DanceStats> AzureDanceStats(DanceMusicCoreService dms, string source)
        {
            var stats = new List<DanceStats>();
            dms.Context.LoadDances();

            var facets = dms.GetTagFacets("DanceTags,DanceTagsInferred", 100, source);

            var tags =  IndexDanceFacet(facets["DanceTags"]);
            var inferred = IndexDanceFacet(facets["DanceTagsInferred"]);

            var used = new HashSet<string>();

            // First handle dancegroups and types under dancegroups
            foreach (var dg in Dances.Instance.AllDanceGroups)
            {
                // All groups except other have a valid 'root' node...
                var scGroup = InfoFromDance(dms, false, dg);
                scGroup.Children = new List<DanceStats>();
                scGroup.AggregateSongCounts(tags, inferred);

                stats.Add(scGroup);

                foreach (var dtyp in dg.Members.Select(dtypT => dtypT as DanceType))
                {
                    AzureHandleType(dtyp, scGroup, tags, inferred, dms);
                    used.Add(dtyp.Id);
                }
            }

            // Then handle ungrouped types
            foreach (var dt in Dances.Instance.AllDanceTypes.Where(dt => !used.Contains(dt.Id)))
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Ungrouped Dance: {0}", dt.Id);
            }

            return stats.OrderByDescending(x => x.Children.Count).ToList();
        }


        private Dictionary<string, long> IndexDanceFacet(IEnumerable<FacetResult> facets)
        {
            var ret = new Dictionary<string, long>();

            foreach (var facet in facets)
            {
                var d = Dances.Instance.DanceFromName((string)facet.Value);
                if (d == null || !facet.Count.HasValue) continue;

                ret[d.Id] = facet.Count.Value;
            }

            return ret;
        }


        private void AzureHandleType(DanceObject dtyp, DanceStats scGroup, IReadOnlyDictionary<string, long> tags, IReadOnlyDictionary<string, long> inferred, DanceMusicCoreService dms)
        {
            var scType = InfoFromDance(dms, false, dtyp);
            scType.AggregateSongCounts(tags, inferred);

            scGroup.Children.Add(scType);
            scType.Parent = scGroup;

            // Only add children to MSC, for other groups they're already built in

            if (scGroup.DanceId == "MSC" || scGroup.DanceId == "PRF")
            {
                scGroup.SongCount += scType.SongCount;
            }
        }

        private DanceStats InfoFromDance(DanceMusicCoreService dms, bool includeStats, DanceObject d)
        {
            if (d == null)
            {
                throw new ArgumentNullException(nameof(d));
            }

            var danceStats = new DanceStats
            {
                DanceObject = d,
                Children = null
            };

            danceStats.CopyDanceInfo(dms.Dances.FirstOrDefault(t => t.Id == d.Id), includeStats, dms);
            return danceStats;
        }

        #endregion
    }
}