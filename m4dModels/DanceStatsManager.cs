using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DanceLibrary;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;

namespace m4dModels
{


    public class DanceStatsManager
    {
        public static string AppData;
        public static DateTime LastUpdate { get; private set; }
        public static string Source { get; private set; }

        public static Dance DanceFromId(string id)
        {
            return s_instance != null ? s_instance.Map[id].Dance : new Dance {Id = id};
        }

        #region Access
        public static DanceStatsInstance GetInstance(DanceMusicService dms)
        {
            return s_lock.Lock(() => s_instance ?? (s_instance = LoadFromAppData()) ?? (s_instance = LoadFromStore(dms, true)));
        }

        public static void SetInstance(DanceStatsInstance instance)
        {
            s_lock.Lock(() =>
            {
                s_instance = instance;
            });
        }

        public static IList<DanceStats> GetFlatDanceStats(DanceMusicService dms)
        {
            return GetInstance(dms).List;
        }

        public static IList<DanceStats> GetDanceStats(DanceMusicService dms)
        {
            return GetInstance(dms).Tree;
        }
        #endregion

        #region Building

        private static readonly SmartLock s_lock = new SmartLock();
        private static DanceStatsInstance s_instance;

        public static void ClearCache(DanceMusicService dms = null, bool reload = false)
        {
            DanceStatsInstance instance = null;

            if (dms != null) instance = LoadFromStore(dms,true);
            else if (reload) instance = LoadFromAppData();

            s_lock.Lock(() =>
            {
                if (reload)
                {
                    Reloads += 1;
                }

                if (instance != null)
                {
                    s_instance = instance;
                    ClearAssociates();
                    return;
                }

                Task.Run(() => RebuildDanceStats(DanceMusicService.GetService()));
            });
        }

        private static void RebuildDanceStats(DanceMusicService dms)
        {
            var instance = LoadFromStore(dms,true);
            s_lock.Lock(() =>
            {
                s_instance = instance;
                ClearAssociates();
            });
        }

        private static void ClearAssociates()
        {
            DanceMusicService.BlowTagCache();
            Song.ResetIndex();
        }

        public static void ReloadDances(DanceMusicService dms)
        {
            s_lock.Lock(() =>
            {
                dms.Context.LoadDances();
                foreach (var dance in dms.Dances)
                {
                    DanceStats danceStats;
                    if (s_instance.Map.TryGetValue(dance.Id, out danceStats))
                    {
                        danceStats.CopyDanceInfo(dance, false, dms);
                    }
                }

                LastUpdate = DateTime.Now;
                Source = Source + " + reload";
            });
        }

        private static DanceStatsInstance LoadFromAppData()
        {
            return s_lock.Lock(() =>
            {
                if (AppData == null) return null;

                var path = System.IO.Path.Combine(AppData, "dance-tag-stats.json");
                if (!System.IO.File.Exists(path)) return null;

                LastUpdate = DateTime.Now;
                Source = "AppData";
                return DanceStatsInstance.LoadFromJson(System.IO.File.ReadAllText(path));
            });
        }

        public static DanceStatsInstance LoadFromStore(DanceMusicService dms, bool save)
        {
            return LoadFromAzure(dms,"default",save);
        }


        public static DanceStatsInstance LoadFromAzure(DanceMusicService dms, string source = "default", bool save = false, bool tagsOnly = false)
        {
            var copy = (tagsOnly && s_instance != null);
            if (save && !copy)
            {
                Dances.Reset();
            }
            var instance = new DanceStatsInstance(
                new TagManager(dms, source),
                copy ? s_instance.Tree : AzureDanceStats(dms, source), 
                copy ? null : dms, source);
            if (!save) return instance;

            LastUpdate = DateTime.Now;
            Source = "Azure";
            SaveToAppData(instance);

            s_instance = instance;
            // This will save any tag types that were created via the load from azure

            dms.UpdateAzureIndex(null, source);
            return instance;
        }

        private static void SaveToAppData(DanceStatsInstance instance)
        {
            s_lock.Lock(() =>
            {
                if (AppData == null) return;

                var json = instance.SaveToJson();
                var path = System.IO.Path.Combine(AppData, "dance-tag-stats.json");
                System.IO.File.WriteAllText(path, json, Encoding.UTF8);
            });
        }

        private static IEnumerable<DanceStats> AzureDanceStats(DanceMusicService dms, string source)
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
                Trace.TraceInformation("Ungrouped Dance: {0}", dt.Id);
            }

            return stats.OrderByDescending(x => x.Children.Count).ToList();
        }


        private static Dictionary<string, long> IndexDanceFacet(IEnumerable<FacetResult> facets)
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


        private static void AzureHandleType(DanceObject dtyp, DanceStats scGroup, IReadOnlyDictionary<string, long> tags, IReadOnlyDictionary<string, long> inferred, DanceMusicService dms)
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

        private static DanceStats InfoFromDance(DanceMusicService dms, bool includeStats, DanceObject d)
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

        public static int Reloads { get; private set; }
    }
}