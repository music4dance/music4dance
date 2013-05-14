using System;
using System.Collections.Generic;

namespace DanceLibrary
{
    public class SongTimer
    {
        public void DoClick()
        {
            DateTime current = DateTime.Now;

            TimeSpan delta = current - _last;
            _last = current;

            // First click or reset
            if (delta >= _maxWait)
            {
                Reset();
            }
            else
            {
                _intervals.Enqueue(delta);
                TimeSpan a = new TimeSpan();

                foreach (TimeSpan t in _intervals)
                {
                    a += t;
                }

                // this may be a bug in the emulator (or the phone) - looks like ms = rand so just grab ticks and convert
                long tick = a.Ticks;
                long ms = tick / 10000;
                _average = ms / _intervals.Count;
                _maxWait = new TimeSpan(0, 0, 0, 0, (int) _average * 2);

                //System.Diagnostics.Debug.WriteLine(sb.ToString());
                System.Diagnostics.Debug.WriteLine(string.Format("Click: time = {0}, ms = {1}, tck = {4}, avg = {2}, a = {3}", current, ms, _average, a, tick));
            }
        }

        public void Reset()
        {
            _intervals.Clear();
            _last = DateTime.Now;
            _maxWait = _defaultWait;
        }

        // This is tempo in x per seconds
        public Decimal Rate
        {
            get 
            {
                Decimal t = 0M;
                if (_average != 0)
                    t = new Decimal(_average) / 1000;
                System.Diagnostics.Debug.WriteLine("Rate = {0}", t);
                return t;
            }
        }

        public bool IsClear
        {
            get { return _intervals.Count == 0; }
        }

        private static readonly TimeSpan _defaultWait = new TimeSpan(0, 0, 10);
        private TimeSpan _maxWait = _defaultWait;
        private DateTime _last = DateTime.Now - _defaultWait;

        private const int _maxCounts = 50;
        private Queue<TimeSpan> _intervals = new Queue<TimeSpan>(_maxCounts);

        private long _average; // Average in milliseconds
    }
}
