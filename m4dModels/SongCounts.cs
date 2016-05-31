using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DanceLibrary;

namespace m4dModels
{
    public class SongCounts
    {
        public static void ClearCache()
        {
            lock (s_map)
            {
                s_counts.Clear();
                s_map.Clear();

                DanceCategories.ClearCache();
                DanceMusicService.BlowTagCache();
                SongDetails.ResetIndex();
            }
        }

        // TODO: This is awfully kludgy, I think the real
        // solution here is probably to bulk out Dance
        // a bit more (like with count and possible parent) and 
        // then not cache as much
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
                        danceStats.Dance = dance;
                    }
                }
            }
        }

        public static IList<DanceStats> GetFlatDanceStats(DanceMusicService dms)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceVerbose,
                $"Entering GetFlatSongCounts:  dms={(dms == null ? "<<NULL>>" : "Valid")}");
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
                DanceId = "ALL",
                DanceName = "All Dances",
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

        private static List<DanceStats> s_counts = new List<DanceStats>();
        private static readonly Dictionary<string, DanceStats> s_map = new Dictionary<string, DanceStats>();

        public static void RebuildSongCounts(DanceMusicService dms)
        {
            lock (s_map)
            {
                ClearCache();
                BuildSongCounts(dms);
            }
        }

        public static IList<DanceStats> GetDanceStats(DanceMusicService dms)
        {
            lock (s_map)
            {
                if (s_counts.Count != 0) return s_counts;

                RebuildSongCounts(dms);

                return s_counts;
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

        private static void BuildSongCounts(DanceMusicService dms)
        {
            dms.Context.LoadDances();

            var used = new HashSet<string>();

            // First handle dancegroups and types under dancegroups
            foreach (var dg in Dances.Instance.AllDanceGroups)
            {
                // All groups except other have a valid 'root' node...
                var scGroup = InfoFromDance(dms, dg);
                scGroup.Children = new List<DanceStats>();

                s_counts.Add(scGroup);

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

            s_counts = s_counts.OrderByDescending(x => x.Children.Count).ToList();
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

            var dance = dms.Dances.FirstOrDefault(t => t.Id == d.Id);

            var count = 0;
            var max = 0;
            List<Song> topSongs = null;
            ICollection<ICollection<PurchaseLink>> topSpotify = null;
 
            if (dance != null)
            {
                count = dance.SongCount;
                max = dance.MaxWeight;

                topSongs = dance.TopSongs?.OrderBy(ts => ts.Rank).Select(ts => ts.Song).ToList();
                topSpotify = dms.GetPurchaseLinks(ServiceType.Spotify, topSongs);
            }

            var sc = new DanceStats()
            {
                DanceId = d.Id,
                DanceName = d.Name,
                SongCount = count,
                MaxWeight = max,
                Dance = dance,
                TopSongs = topSongs,
                TopSpotify = topSpotify,
                BlogTag = d.BlogTag,
                Children = null
            };

            return sc;
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