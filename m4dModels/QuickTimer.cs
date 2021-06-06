using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace m4dModels
{
    public class QuickTimer
    {
        public void ReportTime(string label)
        {
            // TODO: Cleanup the formatting
            if (!_active) return;

            var next = DateTime.Now;
            var delta = next - _lastTime;
            Trace.WriteLine($"{label}- {delta.TotalMinutes}");
            _lastTime = next;

            TimeSpan total;
            if (_totals.TryGetValue(label, out total)) delta += total;
            _totals[label] = delta;
        }

        public void ReportTotals()
        {
            if (!_active) return;

            Trace.WriteLine("-------TOTALS------");
            foreach (var pair in _totals) Trace.WriteLine($"{pair.Key}- {pair.Value.TotalMinutes}");
        }

        private DateTime _lastTime = DateTime.Now;
        private readonly bool _active = TraceLevels.General.TraceVerbose;
        private readonly Dictionary<string, TimeSpan> _totals = new Dictionary<string, TimeSpan>();
    }
}