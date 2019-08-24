using System.Diagnostics;

namespace m4dModels
{
    public class TraceLevels
    {
        private static TraceSwitch _general;

        public static TraceSwitch General
        {
            get
            {
                if (_general == null)
                {
                    // ReSharper disable once UseObjectOrCollectionInitializer
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
            var ts = General;
            ts.Level = level;
        }
    }
}