using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using DanceLibrary;

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
        public string SeoName
        {
            get { return DanceObject.SeoFriendly(DanceName); }
        }

        public IEnumerable<Song> TopSongs { get; private set; }

        public ICollection<ICollection<PurchaseLink>> TopSpotify { get; private set; }

        public IReadOnlyList<CompetitionDance> CompetitionDances { get {return _competitionDances;} }

        public void AddCompetitionDance(CompetitionDance competitionDance)
        {
            lock (this)
            {
                if (_competitionDances == null)
                {
                    _competitionDances = new List<CompetitionDance>();
                }
                _competitionDances.Add(competitionDance);
            }
            
            
        }
        private List<CompetitionDance> _competitionDances;


        static public void ClearCache()
        {
            lock (s_counts)
            {
                s_counts.Clear();
                s_map.Clear();
            }

            DanceCategories.ClearCache();
        }

        // TODO: This is awfully kludgy, I think the real
        // solution here is probably to bulk out Dance
        // a bit more (like with count and possible parent) and 
        // then not cache as much
        static public void ReloadDances(DanceMusicService dms)
        {
            lock (s_map)
            {
                dms.Context.LoadDances();
                foreach (var dance in dms.Dances)
                {
                    SongCounts sc;
                    if (s_map.TryGetValue(dance.Id, out sc))
                    {
                        sc.Dance = dance;
                    }
                }
            }
        }

        static public IList<SongCounts> GetFlatSongCounts(DanceMusicService dms)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, string.Format("Entering GetFlatSongCounts:  dms={0}", dms == null ? "<<NULL>>" : "Valid"));
            var flat = new List<SongCounts>();

            var tree = GetSongCounts(dms);
            Debug.Assert(tree != null);

            flat.AddRange(tree);

            foreach (var children in tree.Select(sc => sc.Children))
            {
                flat.AddRange(children);
            }

            var all = new SongCounts
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
        static private readonly Dictionary<string, SongCounts> s_map = new Dictionary<string, SongCounts>();

        static public IList<SongCounts> GetSongCounts(DanceMusicService dms)
        {
            lock (s_counts)
            {
                if (s_counts.Count != 0) return s_counts;

                dms.Context.LoadDances();

                var used = new HashSet<string>();

                // First handle dancegroups and types under dancegroups
                foreach (var dg in Dances.Instance.AllDanceGroups)
                {
                    // All groups except other have a valid 'root' node...
                    var scGroup = InfoFromDance(dms,dg);
                    scGroup.Children = new List<SongCounts>();

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
                    Trace.WriteLine("Ungrouped Dance: {0}", dt.Id);
                }

                s_counts = s_counts.OrderByDescending(x => x.Children.Count).ToList();
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
            name = DanceObject.SeoFriendly(name);
            return  GetFlatSongCounts(dms).FirstOrDefault(sc => string.Equals(sc.SeoName,name));
        }
        static public int GetScaledRating(IDictionary<string,SongCounts> map, string danceId, int weight, int scale = 5)
        {
            // TODO: Need to re-examine how we deal with international/american
            var sc = map[danceId.Substring(0, 3)];
            float max = sc.MaxWeight;
            var ret = (int)(Math.Ceiling(weight * scale / max));

            if (weight > max ||ret < 0)
            {
                Trace.WriteLine(string.Format("{0}: {1} ? {2}", danceId, weight, max));
            }
            
            return Math.Max(0,Math.Min(ret,scale));
        }
        static public string GetRatingBadge(IDictionary<string, SongCounts> map, string danceId, int weight)
        {
            var scaled = GetScaledRating(map, danceId, weight);

            //return "/Content/thermometer-" + scaled.ToString() + ".png";
            return "rating-" + scaled;
        }

        static public DanceRatingInfo GetRatingInfo(IDictionary<string, SongCounts> map, string danceId, int weight)
        {
            var sc = map[danceId];
            return new DanceRatingInfo
            {
                DanceId = danceId,
                DanceName = sc.DanceName,
                Weight = weight,
                Max = sc.MaxWeight,
                Badge = GetRatingBadge(map, danceId, weight)
            };
        }

        static public IEnumerable<DanceRatingInfo> GetRatingInfo(IDictionary<string, SongCounts> map, SongBase song)
        {
            return song.DanceRatings.Select(dr => GetRatingInfo(map, dr.DanceId, dr.Weight)).ToList();
        }

        static private void HandleType(DanceType dtyp, SongCounts scGroup, DanceMusicService dms)
        {
            var d = dms.Dances.FirstOrDefault(t => t.Id == dtyp.Id);

            var scType = InfoFromDance(dms,dtyp);

            scGroup.Children.Add(scType);
            scType.Parent = scGroup;

            foreach (var dinst in dtyp.Instances)
            {
                Trace.WriteLineIf(d == null, string.Format("Invalid Dance Instance: {0}",dinst.Name));
                var scInstance = InfoFromDance(dms, dinst);

                if (scInstance.SongCount > 0)
                {
                    if (scType.Children == null)
                        scType.Children = new List<SongCounts>();

                    scType.Children.Add(scInstance);
                    //scType.SongCount += scInstance.SongCount;
                }
            }

            // Only add children to MSC, for other groups they're already built in

            if (scGroup.DanceId == "MSC" || scGroup.DanceId == "PRF")
            {
                scGroup.SongCount += scType.SongCount;
            }
        }

        static private SongCounts InfoFromDance(DanceMusicService dms, DanceObject d)
        {
            if (d == null)
            {
                throw new ArgumentNullException("d");
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

                topSongs = dance.TopSongs.OrderBy(ts => ts.Rank).Select(ts => ts.Song).ToList();
                topSpotify = dms.GetPurchaseLinks(ServiceType.Spotify, topSongs);
            }

            var sc = new SongCounts()
            {
                DanceId = d.Id,
                DanceName = d.Name,
                SongCount = count,
                MaxWeight = max,
                Dance = dance,
                TopSongs = topSongs,
                TopSpotify = topSpotify,
                Children = null
            };

            return sc;
        }
    }
}