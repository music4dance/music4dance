using System;
using System.Collections.Generic;

namespace m4d.Utilities
{
    public class RecomputeMarkerService
    {
        private static readonly Dictionary<string, RecomputeMarker> Markers = new();

        private readonly string _appData;

        public RecomputeMarkerService(string appData)
        {
            _appData = appData;
        }

        private string ComputePath(string name)
        {
            return ComputePath(_appData, name);
        }

        public static string ComputePath(string dir, string name)
        {
            return System.IO.Path.Combine(dir, "marker." + name + ".txt");
        }

        public DateTime GetMarker(string name)
        {
            var marker = CreateMarker(name);
            return marker.GetTime();
        }

        public void SetMarker(string name, DateTime? time = null)
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

        public void ResetMarker(string name)
        {
            if (Markers.ContainsKey(name))
            {
                Markers.Remove(name);
            }

            System.IO.File.Delete(ComputePath(name));
        }

        private RecomputeMarker CreateMarker(string name)
        {
            if (Markers.TryGetValue(name, out var marker))
            {
                return marker;
            }

            marker = new RecomputeMarker(_appData, name);
            Markers[name] = marker;
            return marker;
        }
    }

    public class RecomputeMarker
    {
        private readonly string _dir;

        private readonly string _name;

        public RecomputeMarker(string dir, string name)
        {
            _name = name;
            _dir = dir;
        }

        private string Path => RecomputeMarkerService.ComputePath(_dir, _name);

        public DateTime GetTime()
        {
            if (!System.IO.File.Exists(Path))
            {
                return DateTime.MinValue;
            }

            var s = System.IO.File.ReadAllText(Path);
            return DateTime.TryParse(s, out var time)
                ? time
                : System.IO.File.GetLastWriteTime(Path);
        }

        public Guid GetGuid()
        {
            var s = System.IO.File.ReadAllText(Path);
            return Guid.TryParse(s, out var guid) ? guid : Guid.Empty;
        }

        public void SetTime()
        {
            System.IO.File.WriteAllText(Path, @"Semaphore");
        }

        public void SetTime(DateTime time)
        {
            System.IO.File.WriteAllText(Path, time.ToString("G"));
        }

        public void SetGuid(Guid guid)
        {
            System.IO.File.WriteAllText(Path, guid.ToString());
        }
    }
}
