using DanceLibrary;
using m4dModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;

namespace m4dModels
{
    public class SongCounts
    {
        [Key]
        public string DanceId { get; set; }
        public string DanceName { get; set; }
        public int SongCount { get; set; }
        public int MaxWeight { get; set; }
        public string DanceNameAndCount
        {
            get { return string.Format("{0} ({1})", DanceName, SongCount); }
        }
        public Dance Dance { get; set; }
        public SongCounts Parent { get; set; }
        public List<SongCounts> Children { get; set; }

        public IEnumerable<Song> TopSongs { get; set; }
        public void SetTopSongs(int n, DanceMusicService dms)
        {
            SongFilter filter = new SongFilter
            {
                SortOrder = "Dances_10",
                Dances = DanceId,
                TempoMax = 500,
                TempoMin = 1
            };
            var songs = dms.BuildSongList(filter, true).ToList();

            if (songs.Count < 10)
            {
                filter.TempoMax = null;
                filter.TempoMin = null;
                songs = dms.BuildSongList(filter, true).ToList();
            }
            TopSongs = songs;
        }
        static public void ClearCache()
        {
            lock (s_counts)
            {
                s_counts.Clear();
                s_map.Clear();
            }
        }

        // TODO: This is awfully kludgy, I think the real
        // solution here is probably to bulk out Dance
        // a bit more (like with count and possible parent) and 
        // then not cache as much
        static public void ReloadDances(DanceMusicService dms)
        {
            foreach (Dance dance in dms.Dances.Include("DanceLinks"))
            {
                SongCounts sc = null;
                if (s_map.TryGetValue(dance.Id, out sc))
                {
                    sc.Dance = dance;
                }
            }
        }

        static public IList<SongCounts> GetFlatSongCounts(DanceMusicService dms)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, string.Format("Entering GetFlatSongCounts:  dms={0}", dms == null ? "<<NULL>>" : "Valid"));
            List<SongCounts> flat = new List<SongCounts>();

            var tree = GetSongCounts(dms);

            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, string.Format("Top Level Count={0}", tree == null ? "<<NULL>>" : tree.Count.ToString()));
            flat.AddRange(tree);

            foreach (var sc in tree)
            {
                var children = sc.Children;
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose, string.Format("{0} Count={1}", sc.DanceName, tree == null ? "<<NULL>>" : tree.Count.ToString()));
                flat.AddRange(children);
            }

            SongCounts all = new SongCounts
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
                    Trace.WriteLine(string.Format("{0}: {1}", sc.DanceId, sc.SongCount));
                }
            }
#endif
            return flat;
        }

        static private List<SongCounts> s_counts = new List<SongCounts>();
        static private Dictionary<string, SongCounts> s_map = new Dictionary<string, SongCounts>();

        static public IList<SongCounts> GetSongCounts(DanceMusicService dms)
        {
            lock (s_counts)
            {
                if (s_counts.Count == 0)
                {
                    dms.Dances.Include("DanceLinks").Load();

                    HashSet<string> used = new HashSet<string>();

                    // First handle dancegroups and types under dancegroups
                    foreach (DanceGroup dg in Dances.Instance.AllDanceGroups)
                    {
                        // All groups except other have a valid 'root' node...
                        var scGroup = InfoFromDance(dms.Dances,dg);
                        scGroup.Children = new List<SongCounts>();

                        s_counts.Add(scGroup);

                        foreach (DanceObject dtypT in dg.Members)
                        {
                            DanceType dtyp = dtypT as DanceType;
                            Debug.Assert(dtyp != null);

                            HandleType(dtyp, dms.Dances, scGroup);
                            used.Add(dtyp.Id);
                        }
                    }

                    // Then handle ungrouped types
                    foreach (DanceType dt in Dances.Instance.AllDanceTypes)
                    {
                        if (!used.Contains(dt.Id))
                        {
                            Trace.WriteLine("Ungrouped Dance: {0}", dt.Id);
                        }
                    }

                    s_counts = s_counts.OrderByDescending(x => x.Children.Count).ToList();
                }
            }

            return s_counts;
        }


        static public IDictionary<string,SongCounts> GetDanceMap(DanceMusicService dms)
        {
            lock (s_map)
            {
                if (s_map.Count == 0)
                {
                    IList<SongCounts> list = GetFlatSongCounts(dms);

                    foreach (SongCounts sc in list)
                    {
                        s_map.Add(sc.DanceId, sc);
                    }
                }
            }

            return s_map;
        }

        static public SongCounts FromName(string name, DanceMusicService dms)
        {
            SongCounts s = GetFlatSongCounts(dms).FirstOrDefault(sc => string.Equals(sc.DanceName,name,StringComparison.OrdinalIgnoreCase));
            s.SetTopSongs(10,dms);
            return s;
        }
        static public int GetScaledRating(IDictionary<string,SongCounts> map, string danceId, int weight, int scale = 5)
        {
            // TODO: Need to re-examine how we deal with international/american
            SongCounts sc = map[danceId.Substring(0, 3)];
            float max = sc.MaxWeight;
            int ret = (int)(Math.Ceiling((float)(weight * scale) / max));

            if (weight > max ||ret < 0)
            {
                Trace.WriteLine(string.Format("{0}: {1} ? {2}", danceId, weight, max));
            }
            
            return Math.Max(0,Math.Min(ret,scale));
        }
        static public string GetRatingBadge(IDictionary<string, SongCounts> map, string danceId, int weight)
        {
            int scaled = GetScaledRating(map, danceId, weight, 5);

            //return "/Content/thermometer-" + scaled.ToString() + ".png";
            return "rating-" + scaled.ToString();
        }
        static private void HandleType(DanceType dtyp, DbSet<Dance> dances, SongCounts scGroup)
        {
            Dance d = dances.FirstOrDefault(t => t.Id == dtyp.Id);

            var scType = InfoFromDance(dances,dtyp);

            scGroup.Children.Add(scType);
            scType.Parent = scGroup;

            foreach (DanceObject dinst in dtyp.Instances)
            {
                Trace.WriteLineIf(d == null, string.Format("Invalid Dance Instance: {0}",dinst.Name));
                var scInstance = InfoFromDance(dances, dinst);

                if (scInstance.SongCount > 0)
                {
                    if (scType.Children == null)
                        scType.Children = new List<SongCounts>();

                    scType.Children.Add(scInstance);
                    //scType.SongCount += scInstance.SongCount;
                }
            }

            // Only add children to MSC, for other groups they're already built in
            if (scGroup.DanceId == "MSC")
            {
                scGroup.SongCount += scType.SongCount;
            }
        }

        static private SongCounts InfoFromDance(DbSet<Dance> dances, DanceObject d)
        {
            if (d == null)
            {
                throw new ArgumentNullException("dance");
            }

            Dance dance = dances.Where(t => t.Id == d.Id).Include("DanceRatings.Song").FirstOrDefault();

            int max = 0;
            int count = 0;

            if (dance != null)
            {
                var ratings = from dr in dance.DanceRatings where dr.Song.TitleHash != 0 && dr.Song.Purchase != null select dr;
                count = ratings.Count();
                max = count > 0 ? ratings.Max(s => s.Weight) : 0;
            }

            var sc = new SongCounts()
            {
                DanceId = d.Id,
                DanceName = d.Name,
                SongCount = count,
                MaxWeight = max,
                Dance = dance,
                Children = null
            };

            return sc;
        }

    }
}