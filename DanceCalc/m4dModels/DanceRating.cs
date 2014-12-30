using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DanceLibrary;

namespace m4dModels
{
    public class DanceRating : TaggableObject
    {
        public Guid SongId { get; set; }
        public virtual Song Song { get; set; }

        public string DanceId { get; set; }
        public virtual Dance Dance { get; set; }

        public int Weight { get; set; }

        public override char IdModifier
        {
            get { return 'X'; }
        }

        public override string TagIdBase
        {
            get { return DanceId + SongId.ToString("N"); }
        }

        public override void RegisterChangedTags(TagList added, TagList removed, ApplicationUser user, DanceMusicService dms, object data)
        {
            base.RegisterChangedTags(added, removed, user, dms, data);

            if (data != null)
            {
                Song song = data as Song;
                if (song == null)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceError, "Bad Song");
                    return;
                }

                song.ChangeTag(Song.AddedTags + ":" + DanceId, added, dms);
                song.ChangeTag(Song.RemovedTags + ":" + DanceId, removed, dms);
            }
        }

        public override void Dump()
        {
            base.Dump();

            string output = string.Format("DanceId={0},SongId={1},Name={2},Value={3}", DanceId, SongId, Dance.Name, Weight);
            Trace.WriteLine(output);
        }

        public static IEnumerable<DanceRatingDelta> BuildDeltas(string dances, int delta)
        {
            List<DanceRatingDelta> drds = new List<DanceRatingDelta>();

            string[] dl = dances.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string ds in dl)
            {
                string list = null;
                string[] ids = null;
                if (DanceMap.TryGetValue(SongDetails.CleanDanceName(dances), out list))
                {
                    ids = list.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }
                else if (Dances.Instance.DanceDictionary.ContainsKey(ds))
                {
                    ids = new string[] {ds};
                }

                if (ids != null)
                {
                    foreach (var id in ids)
                    {
                        drds.Add(new DanceRatingDelta { DanceId = id, Delta = delta });
                    }
                }
                else
                {
                    Trace.WriteLine(string.Format("Unknown Dance(s): {0}", dances));
                }
            }
            return drds;
        }

        public static Dictionary<string, string> DanceMap
        {
            get 
            {
                lock (s_danceMap)
                {
                    if (!s_builtDM)
                    {
                        foreach (DanceObject d in Dance.DanceLibrary.DanceDictionary.Values)
                        {
                            string name = SongDetails.CleanDanceName(d.Name);
                            s_danceMap.Add(name, d.Id);
                        }

                        s_builtDM = true;
                    }
                }

                return s_danceMap;
            }
        }

        private static bool s_builtDM = false;
        private static readonly Dictionary<string, string> s_danceMap = new Dictionary<string, string>()
        {
            {"CROSSSTEPWALTZ","SWZ"}, {"SLOWANDCROSSSTEPWALTZ","SWZ"},
            {"SOCIALTANGO","TNG"},
            {"VIENNESE","VWZ"},{"MODERATETOFASTWALTZ","VWZ"},
            {"SLOWDANCEFOXTROT","SFT"},
            {"FOXTROTSLOWDANCE","SFT"},
            {"FOXTROTSANDTRIPLESWING","SFT,ECS"},
            {"FOXTROTTRIPLESWING","SFT,ECS"},
            {"TRIPLESWINGFOXTROT","SFT,ECS"},
            {"TRIPLESWING","ECS"},
            {"WCSWING","WCS"},
            {"SINGLESWING","SWG"},
            {"SINGLETIMESWING","SWG"},
            {"STREETSWING","HST"},
            {"HUSTLESTREETSWING","HST"},
            {"HUSTLECHACHA","HST,CHA"},
            {"CHACHAHUSTLE","HST,CHA"},
            {"CLUBTWOSTEP","NC2"},{"NIGHTCLUB2STEP","NC2"},
            {"TANGOARGENTINO","ATN"},
            {"MERENGUETECHNOMERENGUE","MRG"},
            {"RUMBABOLERO", "RMB,BOL" },
            {"RUMBATWOSTEP", "RMB,NC2" },
            {"SLOWDANCERUMBA", "RMB" },
            {"RUMBASLOWDANCE", "RMB" },
            {"SWINGSEASTANDWESTCOASTLINDYHOPANDJIVE", "SWG"},
            {"TRIPLESWINGTWOSTEP", "SWG,NC2"},
            {"TWOSTEPFOXTROTSINGLESWING", "SWG,FXT,NC2"},
            {"SWINGANDLINDYHOP", "ECS,LHP"},
            {"POLKATECHNOPOLKA", "PLK"},
            {"SALSAMAMBO", "SLS,MBO"},
            {"LINDY", "LHP"}
        };
    }

    // Transitory object - move to ViewModel?
    public class DanceRatingDelta
    {
        public DanceRatingDelta()
        {

        }

        public DanceRatingDelta(string id, int delta)
        {
            DanceId = id;
            Delta = delta;
        }

        public DanceRatingDelta(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(@"value");
            }
            string[] parts = value.Split('+', '-');

            int sign = value.Contains('-') ? -1 : 1;
            int offset = 1;

            DanceId = parts[0];
            if (parts.Length > 1)
            {
                int.TryParse(parts[1], out offset);
            }

            Delta = sign * offset;
        }

        public override string ToString()
        {
            return string.Format("{0}{1}{2}", DanceId, Delta < 0 ? "-" : "+", Math.Abs(Delta));
        }

        public string DanceId { get; set; }
        public int Delta { get; set; }
    }
}
