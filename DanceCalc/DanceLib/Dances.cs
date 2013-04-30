using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DanceLibrary
{
    static internal class Tags
    {
        static internal readonly string Name = "Name";
        static internal readonly string Meter = "Meter";
        static internal readonly string Tempo = "Tempo";
        static internal readonly string Style = "Style";
        static internal readonly string All = "All";
        static internal readonly string Competitor = "Competitor";
        static internal readonly string Level = "Level";
        static internal readonly string Organization = "Organization";
        static internal readonly string DanceException = "DanceException";
        static internal readonly string DanceType = "DanceType";
        static internal readonly string DanceInstance = "DanceInstance";
    }

    public class DanceObject
    {
        public DanceObject(XElement el)
        {
            Element = el;
            el.AddAnnotation(this);
        }

        public XElement Parent
        {
            get { return Element.Parent; }
        }

        public XElement Element { get; private set; }

        public string GetDefaultAttribute(string name, string def)
        {
            XAttribute x = Element.Attribute(name);
            if (x == null)
            {
                return def;
            }
            else
            {
                return x.Value;
            }        
        }

        public bool TryGetDecimalAttribute(string name, out decimal ret)
        {
            ret = 0M;
            XAttribute x = Element.Attribute(name);

            return x != null && decimal.TryParse(x.Value, out ret);
        }

        public Decimal GetDecimalAttribute(string name)
        {
            decimal ret = 0M;
            if (!TryGetDecimalAttribute(name,out ret))
                throw new ArgumentOutOfRangeException();

            return ret;
        }
    }

    public class DanceType : DanceObject
    {
        public DanceType(XElement el) : 
            base(el)
        {
        }

        public string Name
        {
            get {return Element.Attribute(Tags.Name).Value;}
        }

        public string MeterString
        {
            get { return Element.Attribute(Tags.Meter).Value; }
        }

        public Meter Meter
        {
            get
            {
                return new Meter(MeterString);
            }
        }

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

    public class DanceInstance : DanceObject
    {
        public DanceInstance(XElement el)
            : base(el)
        {
            _tempo = new Tempo(this, Tags.Tempo);
        }

        public DanceType DanceType
        {
            get { return Element.Parent.Annotation<DanceType>(); }
        }

        public string Style
        {
            get { return Element.Attribute(Tags.Style).Value; }
        }

        public Tempo Tempo
        {
            get { return _tempo; }
        }

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
                    tempo = _tempo;
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

            IEnumerable<XElement> elements = from el in Element.Elements(Tags.DanceException) select el;
            foreach (XElement e in elements)
            {
                DanceException de = e.Annotation<DanceException>();
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

        private Tempo _tempo;
    }

    public class DanceException : DanceObject
    {
        public DanceException(XElement el)
            : base(el)
        {
            _tempo = new Tempo(this, Tags.Tempo);
        }

        public string Competitor
        {
            get { return GetDefaultAttribute(Tags.Competitor,Tags.All); }
        }

        public string Level
        {
            get { return GetDefaultAttribute(Tags.Level, Tags.All); }
        }

        public string Organization
        {
            get { return GetDefaultAttribute(Tags.Organization, Tags.All); }
        }

        public Tempo Tempo
        {
            get { return _tempo; }
        }

        private Tempo _tempo;
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
            string s = DanceLibrary.Dances;
            TextReader r = new StringReader(s);
            _dances = XDocument.Load(r);

            // Now annotate the dances with the glue objects
            IEnumerable<XElement> elements = from el in _dances.Elements().Elements(Tags.DanceType) select el;
            foreach (XElement e in elements)
            {
                DanceType dt = new DanceType(e);
                IEnumerable<XElement> sub = from se in e.Elements(Tags.DanceInstance) select se;
                foreach (XElement d in sub)
                {
                    DanceInstance di = new DanceInstance(d);
                    IEnumerable<XElement> sub2 = from se in d.Elements(Tags.DanceException) select se;
                    foreach (XElement d2 in sub2)
                    {
                        DanceException de = new DanceException(d2);
                    }
                }
            }

            Instance = this;
        }

        internal static Dances Instance { get; set; }

        public IEnumerable<DanceInstance> AllDances()
        {
            IEnumerable<XElement> elements = from el in _dances.Elements().Elements().Elements(Tags.DanceInstance) select el;

            List<DanceInstance> dances = new List<DanceInstance>();
            foreach (XElement e in elements)
            {
                dances.Add(e.Annotation<DanceInstance>());
            }

            return dances;
        }

        private decimal SignedMin(decimal a, decimal b)
        {
            decimal abs = Math.Min(Math.Abs(a), Math.Abs(b));

            return abs * Math.Sign(a);
        }

        public IEnumerable<DanceSample> DancesFiltered(Meter meter, Decimal tempo, decimal epsilon)
        {
            IEnumerable<XElement> elements = from el in _dances.Elements().Elements().Elements(Tags.DanceInstance) select el;

            // Cut a fairly wide swath on what we include in the list
            Dictionary<string,DanceSample> dances = new Dictionary<string,DanceSample>();
            foreach (XElement e in elements)
            {
                DanceInstance di = e.Annotation<DanceInstance>();
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
