// TODONEXT: Abstract out the loader so that we can load from SQL or a file (and eventually AzureSearch), make something to save to JSON, 
// enable quick load from JSON and then referesh if the JSON version is potentially out of date

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DanceLibrary;

namespace m4dModels
{
    public class DanceStatsManager
    {
        public static void ClearCache()
        {
            lock (s_map)
            {
                s_stats.Clear();
                s_map.Clear();
                s_categories.Clear();

                DanceMusicService.BlowTagCache();
                SongDetails.ResetIndex();
            }
        }

        static public DanceCategories GetDanceCategories(DanceMusicService dms)
        {
            lock (s_map)
            {
                if (s_categories.CountCategories == 0)
                    BuildDanceStats(dms);

                return s_categories;
            }
        }

        private static readonly DanceCategories s_categories = new DanceCategories();

        public static void ReloadDances(DanceMusicService dms)
        {
            lock (s_map)
            {
                dms.Context.LoadDances();
                foreach (var dance in dms.Dances)
                {
                    DanceStats danceStats;
                    if (s_map.TryGetValue(dance.Id, out danceStats))
                    {
                        danceStats.CopyDanceInfo(dance,dms);
                    }
                }
            }
        }

        public static IList<DanceStats> GetFlatDanceStats(DanceMusicService dms)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceVerbose,
                $"Entering GetFlatDanceStats:  dms={(dms == null ? "<<NULL>>" : "Valid")}");
            var flat = new List<DanceStats>();

            var tree = GetDanceStats(dms);
            Debug.Assert(tree != null);

            flat.AddRange(tree);

            foreach (var children in tree.Select(sc => sc.Children))
            {
                flat.AddRange(children);
            }

            var all = new DanceStats
            {
                SongCount = tree.Sum(s => s.SongCount),
                Children = null
            };

            flat.Insert(0, all);

#if TRACE
            if (TraceLevels.General.TraceVerbose)
            {
                foreach (var sc in flat)
                {
                    Trace.WriteLine($"{sc.DanceId}: {sc.SongCount}");
                }
            }
#endif
            return flat;
        }

        private static List<DanceStats> s_stats = new List<DanceStats>();
        private static readonly Dictionary<string, DanceStats> s_map = new Dictionary<string, DanceStats>();

        public static void RebuildDanceStats(DanceMusicService dms)
        {
            lock (s_map)
            {
                ClearCache();
                BuildDanceStats(dms);
            }
        }

        public static IList<DanceStats> GetDanceStats(DanceMusicService dms)
        {
            lock (s_map)
            {
                if (s_stats.Count != 0) return s_stats;

                RebuildDanceStats(dms);

                return s_stats;
            }
        }

        public static IDictionary<string,DanceStats> GetDanceMap(DanceMusicService dms)
        {
            lock (s_map)
            {
                if (s_map.Count != 0) return s_map;

                var list = GetFlatDanceStats(dms);

                foreach (var sc in list)
                {
                    s_map.Add(sc.DanceId, sc);
                }

                s_categories.Initialize(dms);

                return s_map;
            }
        }

        public static DanceStats FromName(string name, DanceMusicService dms)
        {
            name = DanceObject.SeoFriendly(name);
            return  GetFlatDanceStats(dms).FirstOrDefault(sc => string.Equals(sc.SeoName,name));
        }

        public static DanceStats FromId(string id, DanceMusicService dms)
        {
            return FromId(id, GetDanceMap(dms));
        }

        public static DanceStats FromId(string id, IDictionary<string,DanceStats> map)
        {
            return LookupDanceStats(map, id);
        }

        public static int GetScaledRating(IDictionary<string,DanceStats> map, string danceId, int weight, int scale = 5)
        {
            // TODO: Need to re-examine how we deal with international/american
            var sc = LookupDanceStats(map, danceId);
            if (sc == null) return 0;

            float max = sc.MaxWeight;
            var ret = (int)(Math.Ceiling(weight * scale / max));

            if (TraceLevels.General.TraceInfo && (weight > max || ret < 0))
            {
                Trace.WriteLine($"{danceId}: {weight} ? {max}");
            }
            
            return Math.Max(0,Math.Min(ret,scale));
        }
        public static string GetRatingBadge(IDictionary<string, DanceStats> map, string danceId, int weight)
        {
            var scaled = GetScaledRating(map, danceId, weight);

            //return "/Content/thermometer-" + scaled.ToString() + ".png";
            return "rating-" + scaled;
        }

        public static DanceRatingInfo GetRatingInfo(IDictionary<string, DanceStats> map, string danceId, int weight)
        {
            var sc = LookupDanceStats(map, danceId);
            if (sc == null) return null;

            return new DanceRatingInfo
            {
                DanceId = danceId,
                DanceName = sc.DanceName,
                Weight = weight,
                Max = sc.MaxWeight,
                Badge = GetRatingBadge(map, danceId, weight)
            };
        }

        public static IEnumerable<DanceRatingInfo> GetRatingInfo(IDictionary<string, DanceStats> map, SongBase song)
        {
            return song.DanceRatings.Select(dr => GetRatingInfo(map, dr.DanceId, dr.Weight)).ToList();
        }

        private static void BuildDanceStats(DanceMusicService dms)
        {
            dms.Context.LoadDances();

            var used = new HashSet<string>();

            // First handle dancegroups and types under dancegroups
            foreach (var dg in Dances.Instance.AllDanceGroups)
            {
                // All groups except other have a valid 'root' node...
                var scGroup = InfoFromDance(dms, dg);
                scGroup.Children = new List<DanceStats>();

                s_stats.Add(scGroup);

                foreach (var dtyp in dg.Members.Select(dtypT => dtypT as DanceType))
                {
                    Debug.Assert(dtyp != null);

                    HandleType(dtyp, scGroup, dms);
                    used.Add(dtyp.Id);
                }
            }

            // Then handle ungrouped types
            foreach (var dt in Dances.Instance.AllDanceTypes.Where(dt => !used.Contains(dt.Id)))
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Ungrouped Dance: {0}", dt.Id);
            }

            s_stats = s_stats.OrderByDescending(x => x.Children.Count).ToList();
        }

        private static void HandleType(DanceType dtyp, DanceStats scGroup, DanceMusicService dms)
        {
            var d = dms.Dances.FirstOrDefault(t => t.Id == dtyp.Id);

            var scType = InfoFromDance(dms,dtyp);

            scGroup.Children.Add(scType);
            scType.Parent = scGroup;

            foreach (var dinst in dtyp.Instances)
            {
                Trace.WriteLineIf(d == null, $"Invalid Dance Instance: {dinst.Name}");
                var scInstance = InfoFromDance(dms, dinst);

                if (scInstance.SongCount <= 0) continue;

                if (scType.Children == null)
                    scType.Children = new List<DanceStats>();

                scType.Children.Add(scInstance);
                //scType.SongCount += scInstance.SongCount;
            }

            // Only add children to MSC, for other groups they're already built in

            if (scGroup.DanceId == "MSC" || scGroup.DanceId == "PRF")
            {
                scGroup.SongCount += scType.SongCount;
            }
        }

        private static DanceStats InfoFromDance(DanceMusicService dms, DanceObject d)
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

            danceStats.CopyDanceInfo(dms.Dances.FirstOrDefault(t => t.Id == d.Id), dms);
            return danceStats;
        }

        [SuppressMessage("ReSharper", "InvertIf")]
        private static DanceStats LookupDanceStats(IDictionary<string, DanceStats> map, string danceId)
        {
            if (danceId.Length > 3) danceId = danceId.Substring(0, 3);

            DanceStats sc;
            if (map.TryGetValue(danceId, out sc)) return sc;

            Trace.WriteLineIf(TraceLevels.General.TraceError, $"Failed to find danceId {danceId}");
            // Clear out the cache to force a reload: workaround for possible cache corruption.
            // TODO: Put in the infrastructure to send app insights events when this happens
            if (Dances.Instance.DanceFromId(danceId) != null)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, $"Attempting to rebuild cache");

                ClearCache();
                Reloads += 1;
            }
            return null;
        }

        public static int Reloads { get; private set; }
    }
}