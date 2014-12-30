using System.Diagnostics;

namespace m4dModels
{
    public class TraceLevels
    {
        static private TraceSwitch _general;

        public static TraceSwitch General
        {
            get
            {
                if (_general == null)
                {
                    _general = new TraceSwitch("General", "Entire application");
#if DEBUG
                    _general.Level = TraceLevel.Info;
#else
                    _general.Level = TraceLevel.Error;
#endif
                }

                return _general;
            }
        }

        public static void SetGeneralLevel(TraceLevel level)
        {
            TraceSwitch ts = General;
            ts.Level = level;
        }
    }
}