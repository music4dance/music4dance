using DanceLibrary;
using m4d.Context;
using m4d.Utilities;
using m4dModels;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;

namespace m4d.ViewModels
{
    public class SongCounts
    {
        public string DanceName { get; set; }
        public string DanceId { get; set; }
        public int SongCount { get; set; }
        public string DanceNameAndCount
        {
            get { return string.Format("{0} ({1})", DanceName, SongCount); }
        }

        public List<SongCounts> Children { get; set; }

        static public void ClearCache()
        {
            lock (s_counts)
            {
                s_counts.Clear();
            }
        }

        static public IList<SongCounts> GetFlatSongCounts(DanceMusicContext dmc)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, string.Format("Entering GetFlatSongCounts:  DMC={0}", dmc == null ? "<<NULL>>" : "Valid"));
            List<SongCounts> flat = new List<SongCounts>();

            var tree = GetSongCounts(dmc);

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
                    Trace.WriteLine(string.Format("{0}: {1}",sc.DanceId,sc.SongCount));
                }
            }
#endif
            return flat;
        }

        static private IList<SongCounts> s_counts = new List<SongCounts>();

        static public IList<SongCounts> GetSongCounts(DanceMusicContext dmc)
        {
            lock (s_counts)
            {
                if (s_counts.Count == 0)
                {
                    dmc.Dances.Load();

                    HashSet<string> used = new HashSet<string>();

                    // First handle dancegroups and types under dancegroups
                    foreach (DanceGroup dg in Dances.Instance.AllDanceGroups)
                    {
                        Dance d = dmc.Dances.FirstOrDefault(t => t.Id == dg.Id);

                        // All groups except other have a valid 'root' node...
                        var scGroup = new SongCounts()
                        {
                            DanceId = dg.Id,
                            DanceName = dg.Name,
                            SongCount = CountFromDance(d),
                            Children = new List<SongCounts>()
                        };

                        s_counts.Add(scGroup);

                        foreach (DanceObject dtypT in dg.Members)
                        {
                            DanceType dtyp = dtypT as DanceType;
                            Debug.Assert(dtyp != null);

                            HandleType(dtyp, dmc.Dances, scGroup);
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

        static private void HandleType(DanceType dtyp, DbSet<Dance> dances, SongCounts scGroup)
        {
            Dance d = dances.FirstOrDefault(t => t.Id == dtyp.Id);

            var scType = new SongCounts()
            {
                DanceId = dtyp.Id,
                DanceName = dtyp.Name,
                SongCount = CountFromDance(d),
                Children = null
            };

            scGroup.Children.Add(scType);

            foreach (DanceObject dinst in dtyp.Instances)
            {
                d = dances.FirstOrDefault(t => t.Id == dinst.Id);
                Trace.WriteLineIf(d == null, string.Format("Invalid Dance Instance: {0}",dinst.Name));
                int count = CountFromDance(d);

                if (count > 0)
                {
                    var scInstance = new SongCounts()
                    {
                        DanceId = dinst.Id,
                        DanceName = dinst.Name,
                        SongCount = count
                    };

                    if (scType.Children == null)
                        scType.Children = new List<SongCounts>();

                    scType.Children.Add(scInstance);
                    scType.SongCount += count;
                }
            }

            scGroup.SongCount += scType.SongCount;
        }

        static private int CountFromDance(Dance dance)
        {
            if (dance == null)
            {
                return 0;
            }
            else
            {
                var ratings = from dr in dance.DanceRatings where !dr.Song.IsNull && dr.Song.Purchase != null select dr;
                return ratings.Count();
            }
        }
    }
}