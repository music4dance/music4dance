using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DanceLibrary
{
    static internal class Tags
    {
        static internal readonly string Style = "Style";
        static internal readonly string All = "All";
        static internal readonly string Competitor = "Competitor";
        static internal readonly string Level = "Level";
        static internal readonly string Organization = "Organization";
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DanceType
    {
        [JsonConstructor]
        public DanceType(string name, string description, Meter meter, DanceInstance[] instances)
        {
            Name = name;
            Description = description;
            Meter = meter;
            Instances = new List<DanceInstance>(instances);

            if (instances != null)
            {
                foreach (DanceInstance instance in instances)
                {
                    instance.DanceType = this;
                }
            }
        }

        [JsonProperty]
        public string Name {get; protected set;}

        [JsonProperty]
        public Meter Meter {get; protected set;}

        [JsonProperty]
        public List<DanceInstance> Instances { get; protected set; }

        public string Description {get;set;}

        public Uri Link {get;set;}

        public string ShortName
        {
            get { return Name.Replace(" ", ""); }
        }

        public override bool Equals(object obj)
        {
            DanceType other = obj as DanceType;
            if (other == null)
                return false;
            else
                return this.Name == other.Name;
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DanceInstance
    {
        [JsonConstructor]
        public DanceInstance(string style, Tempo tempo, DanceException[] exceptions)
        {
            Style = style;
            Tempo = tempo;
            Exceptions = new List<DanceException>(exceptions);

            if (exceptions != null)
            {
                foreach (DanceException exception in exceptions)
                {
                    exception.DanceInstance = this;
                }
            }
        }

        public DanceType DanceType { get; internal set;}

        [JsonProperty]
        public string Style {get; protected set;}

        [JsonProperty]
        public Tempo Tempo {get; protected set;}

        [JsonProperty]
        public List<DanceException> Exceptions { get; protected set; }

        public Tempo FilteredTempo
        {
            get 
            {
                ReadOnlyCollection<DanceException> exceptions = GetFilteredExceptions();

                // Include the general tempo iff the exceptions don't fully cover the
                //  selected filters for the instnace in question
                Tempo tempo = null;
                if (IncludeGeneral(exceptions))
                {
                    tempo = Tempo;
                }

                // Now include all of the tempos in the exceptions that are covered by
                //  the selected filter
                foreach (DanceException de in exceptions)
                {
                    tempo = de.Tempo.Include(tempo);
                }

                return tempo;
            }
        }

        private string MergeInclusion(string oldInc, string newInc)
        {
            string ret = oldInc;

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

            string competitors = "";
            string levels = "";
            string orgs = "";

            foreach (DanceException de in exceptions)
            {
                competitors = MergeInclusion(competitors, de.Competitor);
                levels = MergeInclusion(levels, de.Level);
                orgs = MergeInclusion(orgs, de.Organization);
            }

            return !FilterObject.IsCovered(orgs, competitors, levels);
        }

        private ReadOnlyCollection<DanceException> GetFilteredExceptions()
        {
            List<DanceException> exceptions = new List<DanceException>();

            foreach (DanceException de in Exceptions)
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


        public bool CalculateMatch(decimal tempo, decimal epsilon, out decimal delta, out decimal deltaPercent, out decimal median)
        {
            bool ret = false; 
            Tempo filteredTempo = FilteredTempo;
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
            return string.Format("{0} ({1}MPM)", Style, FilteredTempo);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DanceException
    {
        [JsonConstructor]
        public DanceException(string organization, Tempo tempo, string competitor, string level)
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
            Tempo = tempo;
            Competitor = competitor;
            Level = level;
        }

        [JsonProperty]
        public string Organization {get; protected set; }

        [JsonProperty]
        public Tempo Tempo {get; protected set; }

        [JsonProperty]
        [DefaultValue("All")]
        public string Competitor {get; protected set; }

        [JsonProperty]
        [DefaultValue("All")]
        public string Level {get; protected set; }

        public DanceInstance DanceInstance { get; internal set; }
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

        public DanceType DanceType
        {
            get 
            { 
                return _rgdi[0].DanceType;
            }
        }

        public string Style
        {
            get 
            { 
                StringBuilder sb = new StringBuilder();
                foreach (DanceInstance di in _rgdi)
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
                else if (TempoDelta < 0)
                    return string.Format("{0:F2}MPM",TempoDelta); 
                else
                    return string.Format("+{0:F2}MPM", TempoDelta); 
            }
        }

        public decimal TempoDeltaPercent { get; set; }

        public string TempoDeltaPercentString
        {
            get
            {
                if (Math.Abs(TempoDeltaPercent) < .01M)
                    return "Exact";
                else if (TempoDeltaPercent < 0)
                    return string.Format("{0:F1}%", TempoDeltaPercent);
                else
                    return string.Format("+{0:F1}%", TempoDeltaPercent); 
            }
        }

        public int CompareTo(DanceSample other)
        {
            return Math.Abs(TempoDelta).CompareTo(Math.Abs(other.TempoDelta));
        }

        public ReadOnlyCollection<DanceInstance> Instances
        {
            get { return new ReadOnlyCollection<DanceInstance>(_rgdi); }
        }

        public override string ToString()
        {
            return string.Format("{0}: Style=({1}), Delta=({2})", DanceType.Name, Style, TempoDeltaString);
        }

        private List<DanceInstance> _rgdi = new List<DanceInstance>();
    }
        
    public class Dances
    {
        public Dances()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings { 
                NullValueHandling = NullValueHandling.Include, 
                DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate };

            string json = DanceLibrary.JsonDances;
            _allDanceTypes = JsonConvert.DeserializeObject<List<DanceType>>(json, settings);

            foreach (DanceType dt in _allDanceTypes)
            {
                _allDanceInstances.AddRange(dt.Instances);
            }

            Instance = this;
        }

        internal static Dances Instance { get; set; }

        public IEnumerable<DanceInstance> AllDances()
        {
            return _allDanceInstances;
        }

        public string GetJSON()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
            };

            string json = JsonConvert.SerializeObject(_allDanceTypes, Formatting.Indented, settings);

            return json;
        }

        private List<DanceType> _allDanceTypes = new List<DanceType>();
        private List<DanceInstance> _allDanceInstances = new List<DanceInstance>();

        private decimal SignedMin(decimal a, decimal b)
        {
            decimal abs = Math.Min(Math.Abs(a), Math.Abs(b));

            return abs * Math.Sign(a);
        }

        public IEnumerable<DanceSample> DancesFiltered(Meter meter, Decimal tempo, decimal epsilon)
        {
            // Cut a fairly wide swath on what we include in the list
            Dictionary<string,DanceSample> dances = new Dictionary<string,DanceSample>();
            foreach (DanceInstance di in _allDanceInstances)
            {
                // Meter is absolute, and null values in some of the other classes are also absolue so check those first
                if (di.CanMatch(meter))
                {
                    decimal delta;
                    decimal deltaPercent;
                    decimal median; 
                    bool match = di.CalculateMatch(tempo, epsilon, out delta, out deltaPercent, out median);
                    
                    // This tempo and style matches the dance instance
                    if (match)
                    {
                        DanceSample ds = null;
                        if (dances.ContainsKey(di.DanceType.Name))
                        {
                            ds = dances[di.DanceType.Name];
                            ds.TempoDelta = SignedMin(ds.TempoDelta, delta);
                            ds.TempoDeltaPercent = SignedMin(ds.TempoDeltaPercent,deltaPercent);
                            ds.Add(di);
                        }
                        else
                         {
                            ds = new DanceSample(di,delta);
                            ds.TempoDeltaPercent = deltaPercent;
                            dances.Add(di.DanceType.Name, ds);
                        }
                    }
                }
            }

            // Now sort so the good matches show up on top
            List<DanceSample> dancelist = dances.Select(dance => dance.Value).ToList();
            dancelist.Sort();

            return dancelist;
        }

        private XDocument _dances;
    }
}
