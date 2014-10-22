using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;


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