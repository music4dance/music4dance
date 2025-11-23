using DanceLibrary;

using System.Diagnostics;
using System.Runtime.Serialization;

namespace m4dModels
{
    [DataContract]
    public class DanceRating : TaggableObject
    {
        private static readonly HashSet<string> s_validClasses = ["style", "tempo", "other"];

        private static bool s_builtDanceMap;

        private static readonly Dictionary<string, string> InitialDanceMap =
            new()
            {
                { "SLOWANDCROSSSTEPWALTZ", "CSW,SWZ" },
                { "SOCIALTANGO", "TNG" },
                { "VIENNESE", "VWZ" }, { "MODERATETOFASTWALTZ", "VWZ" },
                { "SLOWDANCEFOXTROT", "SFT" },
                { "FOXTROTSLOWDANCE", "SFT" },
                { "FOXTROTSANDTRIPLESWING", "SFT,ECS" },
                { "FOXTROTTRIPLESWING", "SFT,ECS" },
                { "TRIPLESWINGFOXTROT", "SFT,ECS" },
                { "TRIPLESWING", "ECS" },
                { "SWINGEC", "ECS" },
                { "SWINGJIVE", "JIV" },
                { "SWINGWC", "WCS" },
                { "WCSWING", "WCS" },
                { "SINGLETIMESWING", "SSW" },
                { "STREETSWING", "HST" },
                { "DISCOFOX", "HST" },
                { "HUSTLESTREETSWING", "HST" },
                { "HUSTLECHACHA", "HST,CHA" },
                { "CHACHAHUSTLE", "HST,CHA" },
                { "CHACHACHA", "CHA" },
                { "CLUBTWOSTEP", "NC2" }, { "NIGHTCLUB2STEP", "NC2" },
                { "TANGOARGENTINO", "ATN" },
                { "MERENGUETECHNOMERENGUE", "MRG" },
                { "RUMBABOLERO", "RMB,BOL" },
                { "RUMBATWOSTEP", "RMB,NC2" },
                { "SLOWDANCERUMBA", "RMB" },
                { "RUMBASLOWDANCE", "RMB" },
                { "SWINGSEASTANDWESTCOASTLINDYHOPANDJIVE", "SWG" },
                { "TRIPLESWINGTWOSTEP", "SWG,NC2" },
                { "TWOSTEPFOXTROTSINGLESWING", "SWG,FXT,NC2" },
                { "SWINGANDLINDYHOP", "ECS,LHP" },
                { "POLKATECHNOPOLKA", "PLK" },
                { "SALSAMAMBO", "SLS,MBO" },
                { "LINDY", "LHP" }
            };

        [DataMember]
        public string DanceId { get; set; }

        [DataMember]
        public string DanceName { get; set; }

        [DataMember]
        public int Max { get; set; }

        [DataMember]
        public string Badge { get; set; }

        [DataMember]
        public int Weight { get; set; }

        protected override HashSet<string> ValidClasses => s_validClasses;

        public static Dictionary<string, string> DanceMap
        {
            get
            {
                lock (InitialDanceMap)
                {
                    if (s_builtDanceMap)
                    {
                        return InitialDanceMap;
                    }

                    foreach (var d in Dance.DanceLibrary.AllDanceGroups)
                    {
                        AddDanceToMap(d);
                    }

                    foreach (var d in Dance.DanceLibrary.AllDanceTypes)
                    {
                        AddDanceToMap(d);
                    }

                    s_builtDanceMap = true;
                }

                return InitialDanceMap;
            }
        }

        public override void RegisterChangedTags(TagList added, TagList removed, string user,
            object data)
        {
            base.RegisterChangedTags(added, removed, user, data);

            if (data == null)
            {
                return;
            }

            if (data is not Song song)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, "Bad Song");
                return;
            }

            song.ChangeTag(Song.AddedTags + ":" + DanceId, added);
            song.ChangeTag(Song.RemovedTags + ":" + DanceId, removed);
        }

        public static IEnumerable<DanceRatingDelta> BuildDeltas(string dances, int delta)
        {
            var drds = new List<DanceRatingDelta>();

            var dl = dances.Split([',', '/'], StringSplitOptions.RemoveEmptyEntries);

            foreach (var ds in dl)
            {
                string[] ids = null;
                if (DanceMap.TryGetValue(Song.CleanDanceName(ds), out var list))
                {
                    ids = list.Split([','], StringSplitOptions.RemoveEmptyEntries);
                }
                else if (Dances.Instance.DanceFromId(ds) != null)
                {
                    ids = [ds];
                }

                if (ids != null)
                {
                    drds.AddRange(
                        ids.Select(
                            id => new DanceRatingDelta
                            { DanceId = id, Delta = delta }));
                }
                else
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Unknown Dance(s): {dances}");
                }
            }

            return drds;
        }

        private static void AddDanceToMap(DanceObject dance)
        {
            var name = Song.CleanDanceName(dance.Name);
            InitialDanceMap.Add(name, dance.Id);
        }
    }

    // Transitory object - move to ViewModel?
}
