namespace DanceLibrary
{
    public enum DurationKind
    {
        Beat = 0,
        Measure = 1,
        Second = 2,
        Minute = 3
    };

    public class DurationType
    {
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
        public override bool Equals(object obj)
        {
            return obj is DurationType duration && _dk == duration._dk;
        }

        public override int GetHashCode()
        {
            return _dk.GetHashCode();
        }

        #endregion

        private readonly DurationKind _dk;
    }
}
