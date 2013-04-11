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
        static internal readonly string Category = "Category";
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

        public string Category
        {
            get { return Element.Attribute(Tags.Category).Value; }
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

                Tempo tempo = null;
                if (IncludeGeneral(exceptions))
                {
                    tempo = _tempo;
                }

                foreach (DanceException de in exceptions)
                {
                    tempo = de.Tempo.Include(tempo);
                }

                return tempo;
            }
        }

        private bool IncludeGeneral(ReadOnlyCollection<DanceException> exceptions)
        {
            // No exceptions, so definitely need general
            if (exceptions.Count == 0)
                return true;

            StringBuilder competitors = new StringBuilder();
            StringBuilder levels = new StringBuilder();
            StringBuilder orgs = new StringBuilder();

            foreach (DanceException de in exceptions)
            {
                if (de.Competitor == Tags.All)
                    competitors = new StringBuilder(Tags.All);
                else
                    competitors.AppendFormat("{0},", de.Competitor);

                if (de.Level == Tags.All)
                    levels = new StringBuilder(Tags.All);
                else
                    levels.AppendFormat("{0},", de.Level);

                if (de.Organization == Tags.All)
                    orgs = new StringBuilder(Tags.All);
                else
                    orgs.AppendFormat("{0},", de.Organization);
            }

            return !FilterObject.IsCovered(orgs.ToString(), competitors.ToString(), levels.ToString());
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
                // Then see if any of the exception filters file
                ret = true;
            }

            return ret;
        }

        public override string ToString()
        {
            return string.Format("{0} {1} ({2}MPM)", Style, Category, FilteredTempo);
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

        public string Category
        {
            get 
            { 
                StringBuilder sb = new StringBuilder();
                foreach (DanceInstance di in _rgdi)
                {
                    sb.Append(di.Category);
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
            return string.Format("{0}: Style=({1}), Category=({2}), Delta=({3})", DanceType.Name, Style, Category, TempoDeltaString);
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
                // Meter and Category are absolute filters, so pull just skip the complicated stuff if these don't match
                if (di.DanceType.Meter.Equals(meter) && FilterObject.GetValue(Tags.Category,di.Category))
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
