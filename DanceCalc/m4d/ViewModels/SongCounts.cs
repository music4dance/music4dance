using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using DanceLibrary;
using System.Diagnostics;
using System.Data.Entity;

using m4d.Models;

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

        static public IList<SongCounts> GetFlatSongCounts(DanceMusicContext dmc)
        {
            Trace.WriteLine(string.Format("Entering GetFlatSongCounts:  DMC={0}", dmc == null ? "<<NULL>>" : "Valid"));
            List<SongCounts> flat = new List<SongCounts>();

            var tree = GetSongCounts(dmc);

            Trace.WriteLine(string.Format("Top Level Count={0}", tree==null?"<<NULL>>":tree.Count.ToString()));
            flat.AddRange(tree);

            foreach (var sc in tree)
            {
                var children = sc.Children;
                Trace.WriteLine(string.Format("{0} Count={1}", sc.DanceName, tree==null?"<<NULL>>":tree.Count.ToString()));
                flat.AddRange(children);
            }

            SongCounts all = new SongCounts
            {
                DanceId = "ALL",
                DanceName = "All",
                SongCount = tree.Sum(s => s.SongCount),
                Children = null
            };

            flat.Insert(0,all);

            return flat;
        }

        static public IList<SongCounts> GetSongCounts(DanceMusicContext dmc)
        {
            dmc.Dances.Load();

            var data = new List<SongCounts>();

            HashSet<string> used = new HashSet<string>();

            // First handle dancegroups and types under dancegroups
            foreach (DanceGroup dg in Dances.Instance.AllDanceGroups)
            {
                Dance d = dmc.Dances.FirstOrDefault(t => t.Id == dg.Id);

                var scGroup = new SongCounts()
                {
                    DanceId = dg.Id,
                    DanceName = dg.Name,
                    SongCount = d.DanceRatings.Count,
                    Children = new List<SongCounts>()
                };

                data.Add(scGroup);

                foreach (DanceObject dtypT in dg.Members)
                {
                    DanceType dtyp = dtypT as DanceType;
                    Debug.Assert(dtyp != null);

                    HandleType(dtyp, dmc.Dances, scGroup);
                    used.Add(dtyp.Id);
                }
            }

            // Then handle ungrouped types
            var scOther = new SongCounts()
            {
                DanceId = null,
                DanceName = "Other",
                SongCount = 0,
                Children = new List<SongCounts>()
            };
            data.Add(scOther);

            foreach (DanceType dt in Dances.Instance.AllDanceTypes)
            {
                if (!used.Contains(dt.Id))
                {
                    HandleType(dt, dmc.Dances, scOther);
                }
            }

            return data;
        }

        static private void HandleType(DanceType dtyp, DbSet<Dance> dances, SongCounts scGroup)
        {
            Dance d = dances.FirstOrDefault(t => t.Id == dtyp.Id);

            var scType = new SongCounts()
            {
                DanceId = dtyp.Id,
                DanceName = dtyp.Name,
                SongCount = d.DanceRatings.Count,
                Children = null
            };

            scGroup.Children.Add(scType);

            foreach (DanceObject dinst in dtyp.Instances)
            {
                d = dances.FirstOrDefault(t => t.Id == dinst.Id);
                int count = d.DanceRatings.Count;

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

    }
}