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

        public static Guid GetGuid(string name)
        {
            var marker = CreateMarker(name);
            return marker.GetGuid();
        }

        public static void SetMarker(string name, DateTime? time = null)
        {
            var marker = CreateMarker(name);
            if (time.HasValue)
            {
                marker.SetTime(time.Value);
            }
            else
            {
                marker.SetTime();
            }
        }

        public static void SetMarker(string name, Guid guid)
        {
            var marker = CreateMarker(name);
            marker.SetGuid(guid);
        }

        public static void ResetMarker(string name)
        {
            if (Markers.ContainsKey(name))
            {
                Markers.Remove(name);
            }
            System.IO.File.Delete(ComputePath(name));
        }
        private RecomputeMarker(string name)
        {
            _name = name;
        }

        private readonly string  _name;

        private DateTime GetTime()
        {
            if (!System.IO.File.Exists(Path)) return DateTime.MinValue;

            var s = System.IO.File.ReadAllText(Path);
            DateTime time;
            return DateTime.TryParse(s, out time) ? time : System.IO.File.GetLastWriteTime(Path);
        }

        private Guid GetGuid()
        {
            var s = System.IO.File.ReadAllText(Path);
            Guid guid;
            return Guid.TryParse(s, out guid) ? guid : Guid.Empty;
        }

        private void SetTime()
        {
            System.IO.File.WriteAllText(Path,@"Semaphore");
        }

        private void SetTime(DateTime time)
        {
            System.IO.File.WriteAllText(Path, time.ToString("G"));
        }

        public void SetGuid(Guid guid)
        {
            System.IO.File.WriteAllText(Path, guid.ToString());
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

        private string Path => ComputePath(_name);

        private static readonly Dictionary<string,RecomputeMarker> Markers = new Dictionary<string, RecomputeMarker>();

        private static readonly string AppData = System.Web.Hosting.HostingEnvironment.MapPath("~/app_data");

        private static string ComputePath(string name)
        {
            return System.IO.Path.Combine(AppData, "marker." + name + ".txt");
        }
    }
}
