using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DanceLibrary
{
    public enum DurationKind
    {
        Beat = 0,
        Measure = 1,
        Second = 2,
        Minute = 3
    };

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

        public static DurationType FromKind(DurationKind dk)
        {
            return s_commonDurations[(int)dk];
        }

        #region Constructors

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

        #endregion


        #region Properties

        public DurationKind DurationKind => _dk;

        public override string ToString()
        {
            switch (_dk)
            {
                case DurationKind.Beat: return "Beat";
                case DurationKind.Measure: return "Measure";
                case DurationKind.Second: return "Second";
                case DurationKind.Minute: return "Minute";
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return "#ERROR#";
            }
        }

        #endregion

        #region Operators

        public static implicit operator DurationKind(DurationType dt)
        {
            return dt.DurationKind;
        }

        public static implicit operator DurationType(DurationKind dk)
        {
            return s_commonDurations[(int)dk];
        }

        public override bool Equals(object obj)
        {
            var duration = obj as DurationType;
            if (duration == null)
            {
                return false;
            }
            else
            {
                return _dk == duration._dk;
            }
        }

        public override int GetHashCode()
        {
            return _dk.GetHashCode();
        }

        #endregion

        public static ReadOnlyCollection<DurationType> CommonDurations =>
            new ReadOnlyCollection<DurationType>(s_commonDurations);

        private DurationKind _dk;

        private static List<DurationType> s_commonDurations;

        // Implement IConversand
        public Kind Kind => Kind.Duration;

        public string Name => ToString();

        public string Label => TypeName;

        public static string TypeName => "Duration";
    }
}
