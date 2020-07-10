using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Converters;

// ReSharper disable NonReadonlyMemberInGetHashCode

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

    public class OrgSpec
    {
        public string Name { get; set; } // NDCA or DanceSport
        public string Category { get; set; } // Level or Competitor or NULL
        public string Qualifier { get; set; } // Level = Bronze or Silter,Gold; Competitor = Professional,Amateur or ProAm

        public string Title
        {
            get
            {
                var title = "All Organizations";
                if (Name != "All")
                {
                    title = Name;
                    if (Category != null)
                    {
                        title += " (" + Qualifier + ")";
                    }
                }
                return title;
            }
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DanceObject
    {
        [JsonProperty]
        public virtual string Id { get; set; }
        [JsonProperty]
        public virtual string Name { get; set; }
        [JsonProperty]
        public virtual Meter Meter { get; set; }
        [JsonProperty]
        public virtual TempoRange TempoRange { get; set; }
        [JsonProperty]
        public virtual string BlogTag { get; set; }

        public string CleanName => SeoFriendly(Name);

        public static string SeoFriendly(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;

            name = name.Replace(' ', '-');
            name = name.ToLower();
            return name;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DanceType : DanceObject
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors"), JsonConstructor]
        public DanceType(string name,  Meter meter, string[] organizations, DanceInstance[] instances) 
        {
            Name = name;
            Meter = meter;
            Instances = new List<DanceInstance>(instances);

            if (organizations != null)
            {
                Organizations = new List<string>(organizations);
            }

            if (instances != null)
            {
                foreach (var instance in instances)
                {
                    instance.DanceType = this;
                }
            }
        }

        [JsonProperty]
        public override string Id { get; set; }

        [JsonProperty]
        public sealed override string Name {get; set;}

        [JsonProperty]
        public sealed override Meter Meter {get; set;}

        public override TempoRange TempoRange
        {
            get
            {
                Debug.Assert(Instances.Count > 0);
                var tr = Instances[0].TempoRange;
                for (var i = 1; i < Instances.Count; i++)
                {
                    tr = tr.Include(Instances[i].TempoRange);
                }
                return tr;
            }
            set
            {
                Debug.WriteLine("Set TempoRange to {0}", value);
                //Debug.Assert(false);
            }
        }

        [JsonProperty]
        public List<string> Organizations { get; set; }
        [JsonProperty]
        public List<DanceInstance> Instances { get; set; }

        [JsonProperty]
        public string GroupName { get; set; }
        public string GroupId { get; set; }

        public Uri Link {get;set;}

        public string ShortName => Name.Replace(" ", "");

        public override bool Equals(object obj)
        {
            var other = obj as DanceType;
            return other != null && Name == other.Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DanceInstance : DanceObject
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors"), JsonConstructor]
        public DanceInstance(string style, TempoRange tempoRange, DanceException[] exceptions)
        {
            Style = style;
            TempoRange = tempoRange;
            Exceptions = new List<DanceException>(exceptions);

            foreach (var exception in exceptions)
            {
                exception.DanceInstance = this;
            }
        }

        public DanceType DanceType { get; internal set;}

        [JsonProperty]
        public sealed override TempoRange TempoRange { get; set; }

        public TempoRange DanceSportTempo
        {
            get
            {
                if (Exceptions == null)
                    return TempoRange;

                foreach (var ex in Exceptions.Where(ex => ex.Organization == "DanceSport"))
                {
                    return ex.TempoRange;
                }
                return TempoRange;
            }
        }

        public TempoRange NDCATempoA
        {
            get
            {
                if (Exceptions == null)
                    return TempoRange;

                foreach (var ex in Exceptions)
                {
                    if (ex.Organization == "NDCA" &&
                        ((Id[3] == 'A' && (ex.Level == "All" || ex.Level == "Silver,Gold")) ||
                         (Id[3] == 'I' && (ex.Competitor == "All" || ex.Competitor == "Professional,Amateur"))))
                    {
                        return ex.TempoRange;
                    }
                }
                return TempoRange;
            }
        }

        public TempoRange NDCATempoB {
            get
            {
                if (Exceptions == null)
                    return TempoRange;

                foreach (var ex in Exceptions)
                {
                    if (ex.Organization == "NDCA" &&
                        ((Id[3] == 'A' && (ex.Level == "All" || ex.Level == "Bronze")) ||
                         (Id[3] == 'I' && (ex.Competitor == "All" || ex.Competitor == "ProAm"))))
                    {
                        return ex.TempoRange;
                    }
                }
                return TempoRange;
            }
         }

        public override string Id
        {
            get => DanceType.Id + StyleId;
            set => Debug.WriteLine("Set Id to {0}", value);
        }

        public override Meter Meter
        {
            get => DanceType.Meter;
            set => Debug.WriteLine("Set Meter to {0}", value);
        }

        public override string Name
        {
            get => ShortStyle + ' ' + DanceType.Name;
            set => Debug.WriteLine("Set Name to {0}", value);
        }

        [JsonProperty]
        public string Style {get; set;}

        [JsonProperty]
        public string CompetitionGroup { get; set; }

        [JsonProperty]
        [DefaultValue(0)]
        public int CompetitionOrder { get; set; }

        [JsonProperty]
        public List<DanceException> Exceptions { get; set; }

        public TempoRange FilteredTempo
        {
            get 
            {
                var exceptions = GetFilteredExceptions();

                // Include the general tempo iff the exceptions don't fully cover the
                //  selected filters for the instance in question
                TempoRange tempoRange = null;
                if (IncludeGeneral(exceptions))
                {
                    tempoRange = TempoRange;
                }

                // Now include all of the tempos in the exceptions that are covered by
                //  the selected filter

                return exceptions.Aggregate(tempoRange, (current, de) => de.TempoRange.Include(current));
            }
        }

        public string ShortStyle
        {
            get 
            {
                var words = Style.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Debug.Assert(words.Length > 0);
                return words[0];
            }
        }

        public char StyleId
        {
            get
            {
                var ss = ShortStyle;
                Debug.Assert(!string.IsNullOrEmpty(ss));
                return ShortStyle[0];
            }
        }

        private string MergeInclusion(string oldInc, string newInc)
        {
            var ret = oldInc;

            if (newInc == Tags.All)
            {
                ret = Tags.All;
            }
            else if (string.IsNullOrEmpty(oldInc))
            {
                ret = newInc;
            }
            else if (!string.Equals(oldInc,newInc))
            {
                ret =  oldInc + "," + newInc;
            }

            return ret;
        }

        private bool IncludeGeneral(ReadOnlyCollection<DanceException> exceptions)
        {
            // No exceptions, so definitely need general
            if (exceptions.Count == 0)
                return true;

            var competitors = "";
            var levels = "";
            var orgs = "";

            foreach (var de in exceptions)
            {
                competitors = MergeInclusion(competitors, de.Competitor);
                levels = MergeInclusion(levels, de.Level);
                orgs = MergeInclusion(orgs, de.Organization);
            }

            return !FilterObject.IsCovered(orgs, competitors, levels);
        }

        private ReadOnlyCollection<DanceException> GetFilteredExceptions()
        {
            var exceptions = new List<DanceException>();

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var de in Exceptions)
            {
                if (FilterObject.GetValue(Tags.Competitor, de.Competitor) &&
                    FilterObject.GetValue(Tags.Level, de.Level) &&
                    FilterObject.GetValue(Tags.Organization, de.Organization))
                {
                    exceptions.Add(de);
                }
            }

            return new ReadOnlyCollection<DanceException>(exceptions);
        }

        public bool CalculateTempoMatch(decimal tempo, decimal epsilon, out decimal delta, out decimal deltaPercent, out decimal median)
        {
            var ret = false; 
            var filteredTempo = FilteredTempo;
            delta = filteredTempo.CalculateDelta(tempo);
            median = (filteredTempo.Min + filteredTempo.Max) / 2;
            deltaPercent = (delta * 100) / median;

            // First check to see if the instance in general matches
            if (Math.Abs(deltaPercent) < epsilon)
            {
                // Then see if any of the exception filters fire
                ret = true;
            }

            return ret;
        }

        public bool CalculateBeatMatch(decimal tempo, decimal epsilon, out decimal delta, out decimal deltaPercent, out decimal median)
        {
            var b = new Tempo(tempo, new TempoType(TempoKind.BPM)); // Tempo in beats per minute
            var t = b.Convert(new TempoType(TempoKind.MPM, Meter));

            return CalculateTempoMatch(t.Rate, epsilon, out delta, out deltaPercent, out median);
        }

        /// <summary>
        /// Does some basic filtering against absolute (non-tempo based) filters
        ///     If Meter doesn't match, this dance won't work
        ///     If Style doesn't match, we won't get a valid result
        ///     If Orginization/Level/Competitor are null it doesn't make sense, so fail
        /// </summary>
        /// <param name="meter"></param>
        /// <returns></returns>
        public bool CanMatch(Meter meter)
        {
            // Meter is an absolute match
            if (!DanceType.Meter.Equals(meter))
                return false;

            // Style is an absolute match
            if (!FilterObject.GetValue(Tags.Style, Style))
                return false;

            // If no originizations are checked, we can't match
            if (!FilterObject.GetValue(Tags.Organization, Tags.All))
                return false;

            // If NDCA only is checked and either Level or Competitor empty we can't match
            if (!FilterObject.GetValue(Tags.Organization, "DanceSport"))
            {
                if (!FilterObject.GetValue(Tags.Competitor, Tags.All) || !FilterObject.GetValue(Tags.Level, Tags.All))
                    return false;
            }

            return true;
        }

        public override string ToString()
        {
            return $"{Style} ({FilteredTempo}MPM)";
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DanceException
    {
        [JsonConstructor]
        public DanceException(string organization, TempoRange tempoRange, string competitor, string level)
        {
            // Not sure why default value isn't handling these cases, but don't care that much
            if (string.IsNullOrEmpty(competitor))
            {
                competitor = "All";
            }
            if (string.IsNullOrEmpty(level))
            {
                level = "All";
            }

            Organization = organization;
            TempoRange = tempoRange;
            Competitor = competitor;
            Level = level;
        }

        [JsonProperty]
        public string Organization {get; set; }

        [JsonProperty]
        public TempoRange TempoRange {get; set; }

        [JsonProperty]
        [DefaultValue("All")]
        public string Competitor {get; set; }

        [JsonProperty]
        [DefaultValue("All")]
        public string Level {get; set; }

        public DanceInstance DanceInstance { get; internal set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class DanceGroup : DanceObject
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors"), JsonConstructor]
        public DanceGroup(string name, string id, string[] danceIds)
        {
            Name = name;
            Id = id;

            Debug.Assert(danceIds != null);
            DanceIds = danceIds.ToList();
        }
        
        [JsonProperty]
        public override string Id { get; set; }

        [JsonProperty]
        public override string Name { get; set; }

        public override Meter Meter 
        {
            get
            {
                Debug.Assert(Members.Count > 0);
                return Members[0].Meter;
            }
            set
            {
                Debug.WriteLine("Set Meter to {0}", value);
                //Debug.Assert(false);
            }
        }

        public override TempoRange TempoRange
        {
            get
            {
                Debug.Assert(Members != null && Members.Count > 0);

                var range = Members[0].TempoRange;

                for (var i = 1; i < Members.Count; i++)
                {
                    range = range.Include(Members[i].TempoRange);
                }

                return range;
            }
            set
            {
                Debug.WriteLine("Set TempoRange to {0}", value);
                //Debug.Assert(false);
            }
        }

        [JsonProperty]
        public List<string> DanceIds { get; set; }

        public IList<DanceObject> Members => Dances.Instance.FromIds(DanceIds);
    }

    public class DanceSample : IComparable<DanceSample>
    {
        public DanceSample(DanceInstance di, decimal delta)
        {
            _rgdi.Add(di);
            TempoDelta = delta;
        }

        public void Add(DanceInstance di)
        {
            _rgdi.Add(di);
        }

        public DanceType DanceType => _rgdi[0].DanceType;

        public string Style
        {
            get 
            { 
                var sb = new StringBuilder();
                foreach (var di in _rgdi)
                {
                    sb.Append(di.Style);
                    sb.Append(", ");
                }
                sb.Remove(sb.Length-2,2);
                return sb.ToString();
            }
        }

        public decimal TempoDelta { get; set; }

        public string TempoDeltaString
        {
            get
            {
                if (Math.Abs(TempoDelta) < .01M)
                    return "";
                return TempoDelta < 0 ? $"{TempoDelta:F2}MPM" : $"+{TempoDelta:F2}MPM";
            }
        }

        public decimal TempoDeltaPercent { get; set; }

        public string TempoDeltaPercentString
        {
            get
            {
                if (Math.Abs(TempoDeltaPercent) < .01M)
                    return "Exact";
                return TempoDeltaPercent < 0 ? $"{TempoDeltaPercent:F1}%" : $"+{TempoDeltaPercent:F1}%";
            }
        }

        public int CompareTo(DanceSample other)
        {
            return Math.Abs(TempoDelta).CompareTo(Math.Abs(other.TempoDelta));
        }

        public ReadOnlyCollection<DanceInstance> Instances => new ReadOnlyCollection<DanceInstance>(_rgdi);

        public override string ToString()
        {
            return $"{DanceType.Name}: Style=({Style}), Delta=({TempoDeltaString})";
        }

        private readonly List<DanceInstance> _rgdi = new List<DanceInstance>();
    }

    public enum DanceCategoryType
    {
        Both,
        American,
        International
    };

    public class CompetitionGroup
    {
        public string Name { get; set; }

        public List<CompetitionCategory> Categories { get; set; }

        public static CompetitionGroup Get(string name)
        {
            return new CompetitionGroup
            {
                Name = name,
                Categories = CompetitionCategory.GetCategoryList(name).ToList()
            };
        }
    }

    public class CompetitionCategory
    {
        public const string Standard = "International Standard";
        public const string Latin = "International Latin";
        public const string Smooth = "American Smooth";
        public const string Rhythm = "American Rhythm";
        public const string Ballroom = "Ballroom";

        internal static void RegisterDanceInstance(DanceInstance dance)
        {
            if (string.IsNullOrWhiteSpace(dance.CompetitionGroup)) return;

            var name = BuildCanonicalName(dance.Style);
            var category = GetCategory(name);
            if (category == null)
            {
                var group = (List<CompetitionCategory>)GetCategoryList(dance.CompetitionGroup);
                category = new CompetitionCategory {Name=dance.Style,Group=dance.CompetitionGroup};
                group.Add(category);
                s_mapCategories[name] = category;
            }

            if (dance.CompetitionOrder > 0)
            {
                category._round.Add(dance);
                category._round.Sort((c1, c2) => c1.CompetitionOrder.CompareTo(c2.CompetitionOrder));
            }
            else
            {
                category._extra.Add(dance);
            }
        }

        internal static void Reset()
        {
            s_mapGroups.Clear();
            s_mapCategories.Clear();
        }

        internal static IEnumerable<CompetitionCategory> GetCategoryList(string name)
        {
            if (s_mapGroups.TryGetValue(name, out var categories)) return categories;

            categories = new List<CompetitionCategory>();
            s_mapGroups[name] = categories;
            return categories;
        }

        public static CompetitionCategory GetCategory(string name)
        {
            return (s_mapCategories.TryGetValue(BuildCanonicalName(name), out var category)) ? category : null;
        }

        private static readonly Dictionary<string, List<CompetitionCategory>> s_mapGroups = new Dictionary<string, List<CompetitionCategory>>();
        private static readonly Dictionary<string, CompetitionCategory> s_mapCategories = new Dictionary<string, CompetitionCategory>();

        public string Name { get; private set; }
        public string Group { get; private set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public DanceCategoryType CategoryType => Name.StartsWith("International") ? DanceCategoryType.International : DanceCategoryType.American;
        public string CanonicalName => BuildCanonicalName(Name);
        public string FullRoundName => $"{Name} {(Round.Count == 4 ? "four" : "five")} dance round";

        public IList<DanceInstance> Round => _round;
        public IList<DanceInstance> Extras => _extra;

        private readonly List<DanceInstance> _round = new List<DanceInstance>();
        private readonly List<DanceInstance> _extra = new List<DanceInstance>();

        public static string BuildCanonicalName(string name)
        {
            return name.ToLowerInvariant().Replace(' ', '-');
        }
    }

    public class Dances
    {
        private Dances()
        {
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

                foreach (var dt in dg.DanceIds.Select(DanceFromId).OfType<DanceType>())
                {
                    dt.GroupName = dg.Name;
                    dt.GroupId = dg.Id;
                }
            }
        }


        public IEnumerable<DanceInstance> AllDanceInstances => _allDanceInstances;

        public IEnumerable<DanceObject> AllDances => _allDanceObjects;

        public IEnumerable<DanceType> AllDanceTypes => _allDanceTypes;

        public IEnumerable<DanceType> NonPerformanceDanceTypes => _npDanceTypes;

        public IEnumerable<DanceGroup> AllDanceGroups => _allDanceGroups;

        public DanceObject DanceFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            return _allDanceObjects.FirstOrDefault(d => string.Equals(d.Name,name,StringComparison.OrdinalIgnoreCase));
        }

        public DanceObject DanceFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            DanceObject ret;
            return _danceDictionary.TryGetValue(id.ToUpper(), out ret) ? ret : null;
        }

        public OrgSpec[] Organizations = {
            new OrgSpec { Name = "All"},
            new OrgSpec { Name = "DanceSport"},
            new OrgSpec { Name = "NDCA"},
            new OrgSpec { Name = "NDCA", Category="Level", Qualifier="Silver,Gold"},
            new OrgSpec { Name = "NDCA", Category="Level", Qualifier="Bronze"},
            new OrgSpec { Name = "NDCA", Category="Competitor", Qualifier="Professional,Amateur"},
            new OrgSpec { Name = "NDCA", Category="Competitor", Qualifier="ProAm"},
        };

        public KeyValuePair<string, string>[] Styles = {
            new KeyValuePair<string,string>("all","All Styles"),
            new KeyValuePair<string,string>("is","International Standard"),
            new KeyValuePair<string,string>("il","International Latin"),
            new KeyValuePair<string,string>("as","American Smooth"),
            new KeyValuePair<string,string>("ar","American Rhythm"),
            new KeyValuePair<string,string>("s","Social"),
            /*new KeyValuePair<string,string>("p","Performance"), Add this back in if we do the work to make performance styles 1st class citazens*/
        };

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

        private List<DanceType> _allDanceTypes = new List<DanceType>();
        private readonly List<DanceInstance> _allDanceInstances = new List<DanceInstance>();
        private List<DanceGroup> _allDanceGroups = new List<DanceGroup>();
        private readonly List<DanceObject> _allDanceObjects = new List<DanceObject>();
        private readonly Dictionary<string, DanceObject> _danceDictionary = new Dictionary<string, DanceObject>();
        private readonly List<DanceType> _npDanceTypes = new List<DanceType>();

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
            var dances = new Dictionary<string,DanceSample>();
            foreach (var di in _allDanceInstances.Where(di => di.StyleId != 'P').Where(di => meter == null || di.CanMatch(meter)))
            {
                decimal delta;
                decimal deltaPercent;
                decimal median;
                bool match;

                match = meter == null ? 
                    di.CalculateBeatMatch(rate, epsilon, out delta, out deltaPercent, out median) : 
                    di.CalculateTempoMatch(rate, epsilon, out delta, out deltaPercent, out median);
                    
                // This tempo and style matches the dance instance
                if (match)
                {
                    DanceSample ds;
                    if (dances.ContainsKey(di.DanceType.Name))
                    {
                        ds = dances[di.DanceType.Name];
                        ds.TempoDelta = SignedMin(ds.TempoDelta, delta);
                        ds.TempoDeltaPercent = SignedMin(ds.TempoDeltaPercent,deltaPercent);
                        ds.Add(di);
                    }
                    else
                    {
                        ds = new DanceSample(di, delta) {TempoDeltaPercent = deltaPercent};
                        dances.Add(di.DanceType.Name, ds);
                    }
                }
            }

            // Now sort so the good matches show up on top
            var dancelist = dances.Select(dance => dance.Value).ToList();
            dancelist.Sort();

            return dancelist;
        }

        public List<string> ExpandDanceList(string dances)
        {
            var initialList = ParseDanceList(dances.ToUpper());

            // Would use hashset, but looks like not available on phone?
            var set = new Dictionary<string, string>();

            if (initialList != null)
            {
                foreach (var dance in initialList)
                {
                    DoExpand(dance, set);
                }
            }

            return set.Keys.ToList();
        }

        public List<string> ExpandMsc(IEnumerable<string> dances)
        {
            if (dances == null) return new List<string>();

            // Would use hashset, but looks like not available on phone?
            var set = new Dictionary<string, string>();
            foreach (var dance in dances)
            {
                if (string.Equals(dance, "MSC", StringComparison.OrdinalIgnoreCase))
                    DoExpand(dance, set);
                else
                    set[dance] = dance;
            }

            return set.Keys.ToList();
        }

        public List<string> ExpandMsc(string dances)
        {
            var initialList = ParseDanceList(dances.ToUpper());
            return ExpandMsc(initialList);
        }

        public IList<DanceObject> FromIds(IEnumerable<string> dances)
        {
            var dos = new List<DanceObject>();
            if (dances == null) return dos;

            foreach (var s in dances)
            {
                DanceObject d;
                if (_danceDictionary.TryGetValue(s.ToUpper(), out d))
                {
                    dos.Add(d);
                }

            }

            return dos;            
        }
        public IList<DanceObject> FromIds(string dances)
        {
            dances = dances?.ToUpper();
            var list = ParseDanceList(dances);

            return FromIds(list);
        }

        public IList<DanceObject> FromNames(IEnumerable<string> dances)
        {
            var dos = new List<DanceObject>();
            if (dances == null) return dos;

            dos.AddRange(dances.Select(s => AllDances.FirstOrDefault(d => string.Equals(s, d.Name, StringComparison.OrdinalIgnoreCase))).Where(dobj => dobj != null));

            return dos;
        }

        public IList<DanceObject> FromNames(string dances)
        {
            return FromNames(ParseDanceList(dances));
        }


        private IEnumerable<string> ParseDanceList(string dances)
        {
            IEnumerable<string> ret = null;
            if (!string.IsNullOrWhiteSpace(dances))
            {
                var a = dances.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (a.Length > 0)
                {
                    ret = a;
                }
            }

            return ret;
        }

        private void DoExpand(string dance, Dictionary<string,string> set)
        {
            DanceObject dobj;
            if (set.ContainsKey(dance) || !_danceDictionary.TryGetValue(dance.ToUpper(), out dobj)) return;

            set.Add(dance, dance);

            // TODO: Revisit making dance objects generically have children...
            var type = dobj as DanceType;
            if (type != null)
            {
                var dt = type;
                if (dt.Instances == null) return;

                foreach (var child in dt.Instances)
                {
                    DoExpand(child.Id, set);
                }
            }
            else if (dobj is DanceGroup)
            {
                var dg = (DanceGroup) dobj;
                foreach  (var id in dg.DanceIds)
                {
                    DoExpand(id, set);
                }
            }
        }

        public static Dances Load(List<DanceType> danceTypes = null,List<DanceGroup> danceGroups = null)
        {
            var dances = new Dances();

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            dances.LoadDances(danceTypes??JsonConvert.DeserializeObject<List<DanceType>>(DanceLibrary.JsonDances, settings));
            dances.LoadGroups(danceGroups?? JsonConvert.DeserializeObject<List<DanceGroup>>(DanceLibrary.DanceGroups, settings));

            return dances;
        }

        public static Dances Reset(Dances instance=null)
        {
            return s_instance = instance??Load();
        }

        public static Dances Instance => s_instance ?? (s_instance = Load());

        private static Dances s_instance;
    }
}
