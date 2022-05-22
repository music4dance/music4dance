using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;
using DanceLibrary;

namespace m4dModels
{
    public interface IDanceStatsManager
    {
        DateTime LastUpdate { get; }
        string Source { get; }

        DanceStatsInstance Instance { get; }

        IList<DanceStats> Dances { get; }

        IList<DanceStats> Groups { get; }

        Task ClearCache(DanceMusicCoreService dms, bool fromStore);
        Task ReloadDances(DanceMusicCoreService dms);

        Task<DanceStatsInstance>
            LoadFromAzure(DanceMusicCoreService dms, string source = "default");

        Task Initialize(DanceMusicCoreService dms);
    }


    public class DanceStatsManager : IDanceStatsManager
    {
        private const string FileName = "dance-environment";

        public DanceStatsManager()
        {
        }

        public DanceStatsManager(string appRoot)
        {
            AppRoot = appRoot;
        }

        private string AppRoot { get; }
        private string AppData => Path.Combine(AppRoot, "AppData");
        private string Content => Path.Combine(AppRoot, "content");
        public DateTime LastUpdate { get; private set; }
        public string Source { get; private set; }

        #region Access

        public DanceStatsInstance Instance { get; private set; }

        public async Task Initialize(DanceMusicCoreService dms)
        {
            if (Instance != null)
            {
                throw new Exception("Should only Initialize DanceStatsManager once");
            }

            Instance = await LoadFromAppData(dms) ?? await LoadFromAzure(dms);
        }

        public IList<DanceStats> Dances => Instance.Dances;

        public IList<DanceStats> Groups => Instance.Groups;

        #endregion

        #region Building

        public async Task ClearCache(DanceMusicCoreService dms, bool fromStore)
        {
            var instance = fromStore
                ? await LoadFromAzure(dms)
                : await LoadFromAppData(dms);

            if (instance != null)
            {
                Instance = instance;
                ClearAssociates();
            }
        }

        private void ClearAssociates()
        {
        }

        public async Task ReloadDances(DanceMusicCoreService dms)
        {
            foreach (var dance in await dms.Context.LoadDances())
            {
                if (Instance.Map.TryGetValue(dance.Id, out var danceStats))
                {
                    danceStats.CopyDanceInfo(dance);
                }
            }

            LastUpdate = DateTime.Now;
            Source = Source + " + reload";
        }

        private async Task<DanceStatsInstance> LoadFromAppData(DanceMusicCoreService dms)
        {
            if (AppData == null)
            {
                return null;
            }

            var path = Path.Combine(AppData, $"{FileName}.json");
            if (!File.Exists(path))
            {
                return null;
            }

            LastUpdate = DateTime.Now;
            Source = "AppData";
            Instance = await DanceStatsInstance.LoadFromJson(
                await File.ReadAllTextAsync(path), dms);
            return Instance;
        }

        public async Task<DanceStatsInstance> LoadFromJson(string json, DanceMusicCoreService dms)
        {
            Source = "Json";
            Instance = await DanceStatsInstance.LoadFromJson(json, dms);
            return Instance;
        }

        public async Task<DanceStatsInstance> LoadFromAzure(
            DanceMusicCoreService dms, string source = "default")
        {
            var dancesJson = await File.ReadAllTextAsync(Path.Combine(Content, "dances.txt"));
            var groupsJson = await File.ReadAllTextAsync(Path.Combine(Content, "dancegroups.txt"));
            DanceLibrary.Dances.Reset(DanceLibrary.Dances.Load(dancesJson, groupsJson));

            var songCounts = await GetSongCounts(dms, source);
            var instance = await DanceStatsInstance.BuildInstance(
                await TagManager.BuildTagManager(dms, source),
                await AzureDanceStats(DanceLibrary.Dances.Instance.AllDanceGroups, songCounts, dms),
                await AzureDanceStats(DanceLibrary.Dances.Instance.AllDanceTypes, songCounts, dms),
                dms, source);

            LastUpdate = DateTime.Now;
            Source = "Azure";
            SaveToAppData(instance);

            Instance = instance;
            // This will save any tag types that were created via the load from azure

            await dms.GetSongIndex(source).UpdateAzureIndex(null, dms);
            return instance;
        }

        private void SaveToAppData(DanceStatsInstance instance)
        {
            if (AppData == null)
            {
                return;
            }

            var json = instance.SaveToJson();
            var path = Path.Combine(AppData, $"{FileName}.json");
            Directory.CreateDirectory(AppData);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private async Task<Dictionary<string, long>> GetSongCounts(DanceMusicCoreService dms,
            string source)
        {
            var facets = await dms.GetSongIndex(source)
                .GetTagFacets("DanceTags", 100);

            return IndexDanceFacet(facets["DanceTags"]);
        }

        private async Task<IEnumerable<DanceStats>> AzureDanceStats(
            IEnumerable<DanceObject> dances,
            IReadOnlyDictionary<string, long> songCounts, DanceMusicCoreService dms)
        {
            var stats = new List<DanceStats>();
            await dms.Context.LoadDances();

            foreach (var dt in dances)
            {
                var scType = InfoFromDance(dms, dt);
                scType.AggregateSongCounts(songCounts);
                stats.Add(scType);
            }

            return stats;
        }

        private Dictionary<string, long> IndexDanceFacet(IEnumerable<FacetResult> facets)
        {
            var ret = new Dictionary<string, long>();

            foreach (var facet in facets)
            {
                var d = DanceLibrary.Dances.Instance.DanceFromName((string)facet.Value);
                if (d == null || !facet.Count.HasValue)
                {
                    continue;
                }

                ret[d.Id] = facet.Count.Value;
            }

            return ret;
        }


        private DanceStats InfoFromDance(DanceMusicCoreService dms, DanceObject d)
        {
            if (d == null)
            {
                throw new ArgumentNullException(nameof(d));
            }

            var danceStats = new DanceStats
            {
                DanceId = d.Id,
            };

            danceStats.CopyDanceInfo(
                dms.Dances.FirstOrDefault(t => t.Id == d.Id));
            return danceStats;
        }

        #endregion
    }
}
