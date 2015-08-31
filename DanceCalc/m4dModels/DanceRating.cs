using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using DanceLibrary;

namespace m4dModels
{
    [DataContract]
    public class DanceRating : TaggableObject
    {
        [DataMember]
        public Guid SongId { get; set; }
        public virtual Song Song { get; set; }
        [DataMember]
        public string DanceId { get; set; }
        public virtual Dance Dance { get; set; }
        [DataMember]
        public int Weight { get; set; }

        public override char IdModifier => 'X';

        public override string TagIdBase => DanceId + SongId.ToString("N");

        public override void RegisterChangedTags(TagList added, TagList removed, ApplicationUser user, DanceMusicService dms, object data)
        {
            base.RegisterChangedTags(added, removed, user, dms, data);

            if (data == null) return;

            var song = data as Song;
            if (song == null)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, "Bad Song");
                return;
            }

            song.ChangeTag(SongBase.AddedTags + ":" + DanceId, added, dms);
            song.ChangeTag(SongBase.RemovedTags + ":" + DanceId, removed, dms);
        }

        public static IEnumerable<DanceRatingDelta> BuildDeltas(string dances, int delta)
        {
            var drds = new List<DanceRatingDelta>();

            var dl = dances.Split(new[] { ',','/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var ds in dl)
            {
                string list;
                string[] ids = null;
                if (DanceMap.TryGetValue(SongBase.CleanDanceName(ds), out list))
                {
                    ids = list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }
                else if (Dances.Instance.DanceDictionary.ContainsKey(ds))
                {
                    ids = new[] {ds};
                }

                if (ids != null)
                {
                    drds.AddRange(ids.Select(id => new DanceRatingDelta {DanceId = id, Delta = delta}));
                }
                else
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Unknown Dance(s): {dances}");
                }
            }
            return drds;
        }

        public static Dictionary<string, string> DanceMap
        {
            get 
            {
                lock (InitialDanceMap)
                {
                    if (_builtDanceMap) return InitialDanceMap;

                    foreach (var d in Dance.DanceLibrary.DanceDictionary.Values)
                    {
                        var name = SongBase.CleanDanceName(d.Name);
                        InitialDanceMap.Add(name, d.Id);
                    }

                    _builtDanceMap = true;
                }

                return InitialDanceMap;
            }
        }

        private static bool _builtDanceMap;
        private static readonly Dictionary<string, string> InitialDanceMap = new Dictionary<string, string>()
        {
            {"SLOWANDCROSSSTEPWALTZ","CSW,SWZ"},
            {"SOCIALTANGO","TNG"},
            {"VIENNESE","VWZ"},{"MODERATETOFASTWALTZ","VWZ"},
            {"SLOWDANCEFOXTROT","SFT"},
            {"FOXTROTSLOWDANCE","SFT"},
            {"FOXTROTSANDTRIPLESWING","SFT,ECS"},
            {"FOXTROTTRIPLESWING","SFT,ECS"},
            {"TRIPLESWINGFOXTROT","SFT,ECS"},
            {"TRIPLESWING","ECS"},
            {"SWINGEC","ECS"},
            {"SWINGJIVE","JIV"},
            {"SWINGWC","WCS"},
            {"WCSWING","WCS"},
            {"SINGLESWING","SWG"},
            {"SINGLETIMESWING","SWG"},
            {"STREETSWING","HST"},
            {"DISCOFOX","HST"},
            {"HUSTLESTREETSWING","HST"},
            {"HUSTLECHACHA","HST,CHA"},
            {"CHACHAHUSTLE","HST,CHA"},
            {"CHACHACHA","CHA"},
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
}
