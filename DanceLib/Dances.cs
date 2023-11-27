using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

// ReSharper disable NonReadonlyMemberInGetHashCode


// NEXTSTEPS: Get filtering working as non-static methods
//  rename Dances to DanceLibrary
//  audit use of static DanceLibrary.Instance
//  rework dance database based on current NDCA/DanceSport rules
namespace DanceLibrary
{
    internal static class Tags
    {
        internal static readonly string Style = "Style";
        internal static readonly string All = "All";
        internal static readonly string Competitor = "Competitor";
        internal static readonly string Level = "Level";
        internal static readonly string Organization = "Organization";
    }

    public enum DanceCategoryType
    {
        Both,
        American,
        International
    }

    public class Dances
    {
        private static readonly JsonSerializerSettings s_settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        private static Dances s_instance;
        private readonly List<DanceInstance> _allDanceInstances = new();
        private readonly List<DanceObject> _allDanceObjects = new();

        private readonly Dictionary<string, DanceObject> _danceDictionary =
            new();

        private readonly List<DanceType> _npDanceTypes = new();
        private List<DanceGroup> _allDanceGroups = new();

        private List<DanceType> _allDanceTypes = new();

        public OrgSpec[] Organizations =
        {
            new OrgSpec { Name = "All" },
            new OrgSpec { Name = "DanceSport" },
            new OrgSpec { Name = "NDCA" },
            new OrgSpec { Name = "NDCA", Category = "Level", Qualifier = "Silver,Gold" },
            new OrgSpec { Name = "NDCA", Category = "Level", Qualifier = "Bronze" },
            new OrgSpec
                { Name = "NDCA", Category = "Competitor", Qualifier = "Professional,Amateur" },
            new OrgSpec { Name = "NDCA", Category = "Competitor", Qualifier = "ProAm" }
        };

        public KeyValuePair<string, string>[] Styles =
        {
            new KeyValuePair<string, string>("all", "All Styles"),
            new KeyValuePair<string, string>("is", "International Standard"),
            new KeyValuePair<string, string>("il", "International Latin"),
            new KeyValuePair<string, string>("as", "American Smooth"),
            new KeyValuePair<string, string>("ar", "American Rhythm"),
            new KeyValuePair<string, string>("s", "Social")
            /*new KeyValuePair<string,string>("p","Performance"), Add this back in if we do the work to make performance styles 1st class citazens*/
        };

        private Dances()
        {
        }


        public IEnumerable<DanceInstance> AllDanceInstances => _allDanceInstances;

        public IEnumerable<DanceObject> AllDances => _allDanceObjects;

        public IEnumerable<DanceType> AllDanceTypes => _allDanceTypes;

        public IEnumerable<DanceType> NonPerformanceDanceTypes => _npDanceTypes;

        public IEnumerable<DanceGroup> AllDanceGroups => _allDanceGroups;

        public static Dances Instance => s_instance;

        public IEnumerable<DanceObject> ExpandGroups(IEnumerable<DanceObject> dances)
        {
            var expanded = new List<DanceObject>();
            foreach (var d in dances)
            {
                switch (d)
                {
                    case DanceType _:
                        expanded.Add(d);
                        break;
                    case DanceGroup group:
                        expanded.AddRange(group.Members);
                        break;
                }
            }

            return expanded;
        }

        private void LoadDances(List<DanceType> danceTypes)
        {
            _allDanceTypes = danceTypes;
            foreach (var dt in _allDanceTypes)
            {
                _allDanceInstances.AddRange(dt.Instances);
            }

            foreach (var dt in _allDanceTypes)
            {
                _allDanceObjects.Add(dt);
                _danceDictionary.Add(dt.Id, dt);
                if (dt.Instances.All(di => di.StyleId != 'P'))
                {
                    _npDanceTypes.Add(dt);
                }
            }

            CompetitionCategory.Reset();
            foreach (var di in _allDanceInstances)
            {
                _allDanceObjects.Add(di);
                _danceDictionary.Add(di.Id, di);
                CompetitionCategory.RegisterDanceInstance(di);
            }
        }

        private void LoadGroups(List<DanceGroup> danceGroups)
        {
            _allDanceGroups = danceGroups;

            foreach (var dg in _allDanceGroups)
            {
                _allDanceObjects.Add(dg);
                _danceDictionary.Add(dg.Id, dg);
            }
        }

        public DanceObject DanceFromName(string name)
        {
            return string.IsNullOrEmpty(name)
                ? null
                : _allDanceObjects.FirstOrDefault(
                d =>
                    string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public DanceObject DanceFromId(string id)
        {
            return string.IsNullOrEmpty(id) ? null : _danceDictionary.TryGetValue(id.ToUpper(), out var ret) ? ret : null;
        }

        public string GetJSON()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
            };

            var json = JsonConvert.SerializeObject(_allDanceTypes, Formatting.Indented, settings);

            return json;
        }

        private static decimal SignedMin(decimal a, decimal b)
        {
            var abs = Math.Min(Math.Abs(a), Math.Abs(b));

            return abs * Math.Sign(a);
        }

        public IEnumerable<DanceSample> DancesFiltered(Tempo tempo, decimal epsilon)
        {
            var meter = tempo.TempoType.Meter;
            var rate = tempo.Rate;

            // Cut a fairly wide swath on what we include in the list
            var dances = new Dictionary<string, DanceSample>();
            foreach (var di in _allDanceInstances.Where(di => di.StyleId != 'P')
                .Where(di => meter == null || di.CanMatch(meter)))
            {

                var match = meter == null
                    ? di.CalculateBeatMatch(rate, epsilon, out var delta, out var deltaPercent, out _)
                    : di.CalculateTempoMatch(
                        rate, epsilon, out delta, out deltaPercent,
                        out _);

                // This tempo and style matches the dance instance
                if (match)
                {
                    DanceSample ds;
                    if (dances.ContainsKey(di.DanceType.Name))
                    {
                        ds = dances[di.DanceType.Name];
                        ds.TempoDelta = SignedMin(ds.TempoDelta, delta);
                        ds.TempoDeltaPercent = SignedMin(ds.TempoDeltaPercent, deltaPercent);
                        ds.Add(di);
                    }
                    else
                    {
                        ds = new DanceSample(di, delta) { TempoDeltaPercent = deltaPercent };
                        dances.Add(di.DanceType.Name, ds);
                    }
                }
            }

            // Now sort so the good matches show up on top
            var dancelist = dances.Select(dance => dance.Value).ToList();
            dancelist.Sort();

            return dancelist;
        }

        public List<string> ExpandMsc(IEnumerable<string> dances)
        {
            if (dances == null)
            {
                return new List<string>();
            }

            // Would use hashset, but looks like not available on phone?
            var set = new Dictionary<string, string>();
            foreach (var dance in dances)
            {
                if (string.Equals(dance, "MSC", StringComparison.OrdinalIgnoreCase))
                {
                    DoExpand(dance, set);
                }
                else
                {
                    set[dance] = dance;
                }
            }

            return set.Keys.ToList();
        }

        public IList<DanceObject> FromIds(IEnumerable<string> dances)
        {
            var dos = new List<DanceObject>();
            if (dances == null)
            {
                return dos;
            }

            foreach (var s in dances)
            {
                if (_danceDictionary.TryGetValue(s.ToUpper(), out var d))
                {
                    dos.Add(d);
                }
            }

            return dos;
        }

        public IList<DanceObject> FromNames(IEnumerable<string> dances)
        {
            var dos = new List<DanceObject>();
            if (dances == null)
            {
                return dos;
            }

            dos.AddRange(
                dances
                    .Select(
                        s => AllDances.FirstOrDefault(
                            d =>
                                string.Equals(s, d.Name, StringComparison.OrdinalIgnoreCase)))
                    .Where(dobj => dobj != null));

            return dos;
        }

        private void DoExpand(string dance, Dictionary<string, string> set)
        {
            if (set.ContainsKey(dance) ||
                !_danceDictionary.TryGetValue(dance.ToUpper(), out var dobj))
            {
                return;
            }

            set.Add(dance, dance);

            // TODO: Revisit making dance objects generically have children...
            if (dobj is DanceType type)
            {
                var dt = type;
                if (dt.Instances == null)
                {
                    return;
                }

                foreach (var child in dt.Instances)
                {
                    DoExpand(child.Id, set);
                }
            }
            else if (dobj is DanceGroup dg)
            {
                foreach (var id in dg.DanceIds)
                {
                    DoExpand(id, set);
                }
            }
        }

        private static List<T> LoadFromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<List<T>>(json, s_settings);
        }

        public static Dances Load(string typesJson, string groupsJson)
        {
            return Load(
                LoadFromJson<DanceType>(typesJson),
                LoadFromJson<DanceGroup>(groupsJson));
        }

        public static Dances Load(List<DanceType> danceTypes, List<DanceGroup> danceGroups)
        {
            var dances = new Dances();

            dances.LoadDances(danceTypes);
            dances.LoadGroups(danceGroups);

            return dances;
        }

        public static Dances Reset(Dances instance)
        {
            return s_instance = instance;
        }
    }
}
