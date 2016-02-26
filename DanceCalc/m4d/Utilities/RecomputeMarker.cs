using System;
using System.Collections.Generic;

namespace m4d.Utilities
{
    internal class RecomputeMarker
    {
        public static DateTime GetMarker(string name)
        {
            var marker = CreateMarker(name);
            return marker.GetTime();
        }

        public static void SetMarker(string name)
        {
            var marker = CreateMarker(name);
            marker.SetTime();
        }

        private RecomputeMarker(string name)
        {
            _name = name;
        }

        private readonly string  _name;

        private DateTime GetTime()
        {
            return System.IO.File.GetLastWriteTime(Path);
        }

        private void SetTime()
        {
            System.IO.File.WriteAllText(Path,@"Semaphore");
        }

        private static RecomputeMarker CreateMarker(string name)
        {
            RecomputeMarker marker;

            if (!Markers.TryGetValue(name, out marker))
            {
                marker = new RecomputeMarker(name);
                Markers[name] = marker;
            }
            return marker;
        }

        private string Path => System.IO.Path.Combine(AppData,"marker." + _name + ".txt");

        private static readonly Dictionary<string,RecomputeMarker> Markers = new Dictionary<string, RecomputeMarker>();

        private static readonly string AppData = System.Web.Hosting.HostingEnvironment.MapPath("~/app_data");
    }
}
