// TODONEXT: Get JSON loading working,  
//  Figure out how to do JSON loading on start-up and throw a background task to update when appropriate, 
//  Figure out if we can manage AzureSearch loading...
//  

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DanceLibrary;

namespace m4dModels
{
    public class DanceStatsInstance
    {
        public List<DanceStats> Tree { get; set; }
        public List<DanceStats> List => _flat ?? (_flat = Flatten());
        public Dictionary<string, DanceStats> Map => _map ?? (_map = List.ToDictionary(ds => ds.DanceId));

        public int GetScaledRating(string danceId, int weight, int scale = 5)
        {
            var sc = FromId(danceId);
            if (sc == null) return 0;

            float max = sc.MaxWeight;
            var ret = (int)(Math.Ceiling(weight * scale / max));

            if (TraceLevels.General.TraceInfo && (weight > max || ret < 0))
            {
                Trace.WriteLine($"{danceId}: {weight} ? {max}");
            }

            return Math.Max(0, Math.Min(ret, scale));
        }
        public string GetRatingBadge(string danceId, int weight)
        {
            var scaled = GetScaledRating(danceId, weight);

            //return "/Content/thermometer-" + scaled.ToString() + ".png";
            return "rating-" + scaled;
        }

        public DanceRatingInfo GetRatingInfo(string danceId, int weight)
        {
            var sc = FromId(danceId);
            if (sc == null) return null;

            return new DanceRatingInfo
            {
                DanceId = danceId,
                DanceName = sc.DanceName,
                Weight = weight,
                Max = sc.MaxWeight,
                Badge = GetRatingBadge(danceId, weight)
            };
        }

        public IEnumerable<DanceRatingInfo> GetRatingInfo(SongBase song)
        {
            return song.DanceRatings.Select(dr => GetRatingInfo(dr.DanceId, dr.Weight)).ToList();
        }

        public DanceStats FromId(string danceId)
        {
            if (danceId.Length > 3) danceId = danceId.Substring(0, 3);

            DanceStats sc;
            if (Map.TryGetValue(danceId, out sc)) return sc;

            if (Dances.Instance.DanceFromId(danceId) == null) return null;

            Trace.WriteLineIf(TraceLevels.General.TraceError, $"Failed to find danceId {danceId}");
            // Clear out the cache to force a reload: workaround for possible cache corruption.
            // TODO: Put in the infrastructure to send app insights events when this happens
            Trace.WriteLineIf(TraceLevels.General.TraceError, "Attempting to rebuild cache");

            DanceStatsManager.ClearCache(true);
            return null;
        }

        public DanceStats FromName(string name)
        {
            name = DanceObject.SeoFriendly(name);
            return List.FirstOrDefault(sc => string.Equals(sc.SeoName, name));
        }


        private List<DanceStats> Flatten()
        {
            var flat = new List<DanceStats>();

            flat.AddRange(Tree);

            foreach (var children in Tree.Select(ds => ds.Children))
            {
                flat.AddRange(children);
            }

            var all = new DanceStats
            {
                SongCount = Tree.Sum(s => s.SongCount),
                Children = null
            };

            flat.Insert(0, all);

            return flat;
        }

        private List<DanceStats> _flat;
        private Dictionary<string, DanceStats> _map;
    }

    public class DanceStatsManager
    {
        #region Access
        public static DanceStatsInstance GetInstance(DanceMusicService dms)
        {
            lock (s_lock)
            {
                return s_instance ?? (s_instance = new DanceStatsInstance { Tree = BuildDanceStats(dms) });
            }
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

        private static readonly object s_lock = new object();
        private static DanceStatsInstance s_instance;

        public static void ClearCache(bool reload = false)
        {
            lock (s_lock)
            {
                s_instance = null;

                DanceMusicService.BlowTagCache();
                SongDetails.ResetIndex();
                Reloads += 1;
            }
        }

        public static void RebuildDanceStats(DanceMusicService dms)
        {
            lock (s_lock)
            {
                ClearCache();
                GetInstance(dms);
            }
        }

        public static void ReloadDances(DanceMusicService dms)
        {
            lock (s_lock)
            {
                dms.Context.LoadDances();
                foreach (var dance in dms.Dances)
                {
                    DanceStats danceStats;
                    if (s_instance.Map.TryGetValue(dance.Id, out danceStats))
                    {
                        danceStats.CopyDanceInfo(dance, dms);
                    }
                }
            }
        }

        private static List<DanceStats> BuildDanceStats(DanceMusicService dms)
        {
            var stats = new List<DanceStats>();
            dms.Context.LoadDances();

            var used = new HashSet<string>();

            // First handle dancegroups and types under dancegroups
            foreach (var dg in Dances.Instance.AllDanceGroups)
            {
                // All groups except other have a valid 'root' node...
                var scGroup = InfoFromDance(dms, dg);
                scGroup.Children = new List<DanceStats>();

                stats.Add(scGroup);

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

            return stats.OrderByDescending(x => x.Children.Count).ToList();
        }

        private static void HandleType(DanceType dtyp, DanceStats scGroup, DanceMusicService dms)
        {
            var d = dms.Dances.FirstOrDefault(t => t.Id == dtyp.Id);

            var scType = InfoFromDance(dms, dtyp);

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

        #endregion

        public static int Reloads { get; private set; }
    }
}