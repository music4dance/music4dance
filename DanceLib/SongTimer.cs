using System;
using System.Collections.Generic;

namespace DanceLibrary
{
    public class SongTimer
    {
        public void DoClick()
        {
            var currentTicks = DateTime.Now.Ticks;

            var deltaTicks = currentTicks - _lastTicks;
            _lastTicks = currentTicks;

            // First click or reset
            if (deltaTicks >= _maxWaitTicks)
            {
                Reset(currentTicks);
            }
            else
            {
                // Add the wait
                _totalWaitTicks += deltaTicks;
                _totalCount++;

                // this may be a bug in the emulator (or the phone) - looks like ms = rand so just use ticks
                var totalTicks = _totalWaitTicks;
                var averageWait = AverageWait;
                _maxWaitTicks = (long)(averageWait * 2);

                //System.Diagnostics.Debug.WriteLine(sb.ToString());
                System.Diagnostics.Debug.WriteLine(
                    string.Format(
                        "Click: time = {0}, ms = {1}, tck = {4}, avg = {2}, a = {3}", currentTicks,
                        ConvertTicksToMilliSeconds(totalTicks), averageWait, _totalWaitTicks,
                        totalTicks));
            }
        }

        public void Reset()
        {
            Reset(DateTime.Now.Ticks);
        }

        public void Reset(long currentTicks)
        {
            _lastTicks = currentTicks;
            _maxWaitTicks = _defaultWaitTicks;
            _totalWaitTicks = 0;
            _totalCount = 0;
        }

        // This is tempo in x per seconds
        public decimal Rate
        {
            get
            {
                var rate = AverageWait;
                if (rate != 0)
                {
                    rate = 1000 / rate;
                }

                System.Diagnostics.Debug.WriteLine("Tempo = {0}", rate);
                return rate;
            }
        }

        public decimal AverageWait
        {
            get
            {
                return _totalWaitTicks == 0 ? 0 : ConvertTicksToMilliSeconds(_totalWaitTicks / _totalCount);
            }
        }

        public bool IsClear => _totalCount == 0;

        private decimal ConvertTicksToMilliSeconds(long ticks)
        {
            return ConvertTicksToMilliSeconds((decimal)ticks);
        }

        private decimal ConvertTicksToMilliSeconds(decimal ticks)
        {
            return ticks / 10000;
        }

        private const long _defaultWaitTicks = 10 * 1000 * 10000;
        private long _maxWaitTicks = _defaultWaitTicks;
        private long _lastTicks = DateTime.Now.Ticks - _defaultWaitTicks;

        // Running totals
        private long _totalWaitTicks;
        private int _totalCount;
    }
}
