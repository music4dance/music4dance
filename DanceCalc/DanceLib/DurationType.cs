using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DanceLibrary
{
    public enum DurationKind { Beat=0, Measure=1, Second=2, Minute=3 };

    public class DurationType : IConversand
    {
        static DurationType()
        {
            s_commonDurations = new List<DurationType>(4);
            s_commonDurations.Add(new DurationType(DurationKind.Beat));
            s_commonDurations.Add(new DurationType(DurationKind.Measure));
            s_commonDurations.Add(new DurationType(DurationKind.Second));
            s_commonDurations.Add(new DurationType(DurationKind.Minute));
        }

        static public DurationType FromKind(DurationKind dk)
        {
            return s_commonDurations[(int)dk];
        }

        public DurationType(DurationKind dk)
        {
            _dk = dk;
        }

        public DurationType(string s)
        {
            if (s.Equals("Beat"))
            {
                _dk = DurationKind.Beat;
            }
            else if (s.Equals("Measure"))
            {
                _dk = DurationKind.Measure;
            }
            else if (s.Equals("Second"))
            {
                _dk = DurationKind.Second;
            }
            else if (s.Equals("Minute"))
            {
                _dk = DurationKind.Minute;
            }
            else
            {
                System.Diagnostics.Debug.Assert(false);
            }
        }

        public DurationKind DurationKind
        {
            get { return _dk; }
        }

        public override string ToString()
        {
            switch (_dk)
            {
                case DurationKind.Beat: return "Beat";
                case DurationKind.Measure: return "Measure";
                case DurationKind.Second: return "Second";
                case DurationKind.Minute: return "Minute";
                default: System.Diagnostics.Debug.Assert(false); return "#ERROR#";
            }
        }

        public override bool Equals(object obj)
        {
            DurationType duration = obj as DurationType;
            if (duration == null)
                return false;
            else
                return _dk == duration._dk;
        }

        public override int GetHashCode()
        {
            return _dk.GetHashCode();
        }

        public static ReadOnlyCollection<DurationType> CommonDurations
        {
            get
            {
                return new ReadOnlyCollection<DurationType>(s_commonDurations);
            }
        }

        private DurationKind _dk;

        private static List<DurationType> s_commonDurations;

        // Implement IConversand
        public Kind Kind
        {
            get { return Kind.Duration; }
        }

        public string Name
        {
            get { return this.ToString(); }
        }

        public string Label
        {
            get { return TypeName; }
        }

        public static string TypeName
        {
            get { return "Duration"; }
        }
    }
}
