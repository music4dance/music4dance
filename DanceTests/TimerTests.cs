using System;
using System.Diagnostics;
using DanceLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DanceTests
{
    [TestClass]
    public class TimerTests
    {
        private SongTimer PrimeTimer(int delay = 100)
        {
            var st = new SongTimer();

            for (var i = 0; i < 6; i++)
            {
                st.DoClick();
                System.Threading.Thread.Sleep(delay);
            }

            Debug.WriteLine(st.Rate);

            return st;
        }

        //[TestMethod]
        public void Timer()
        {
            var st = PrimeTimer();

            var delta = Math.Abs(10M - st.Rate);

            Assert.IsTrue(delta < .1M);
        }

        [TestMethod]
        public void TimerAutoReset()
        {
            var st = PrimeTimer();

            System.Threading.Thread.Sleep(110);
            st.DoClick();

            Assert.IsTrue(st.IsClear);
        }

        [TestMethod]
        public void TimerManualReset()
        {
            var st = PrimeTimer();

            st.Reset();

            Assert.IsTrue(st.IsClear);
        }

        //[TestMethod]
        public void TimerAndTiming()
        {
            var st = PrimeTimer(1875);

            var rate = st.Rate;
            var delta = Math.Abs(.533M - st.Rate);

            Assert.IsTrue(delta < .1M);

            var baseTempo = new Tempo(32M, new TempoType(TempoKind.MPM, new Meter(4, 4)));
            var timing = new SongTiming(baseTempo, 64M, DurationKind.Measure);
            timing.SetRate(rate);

            var measuredTempo = timing.Tempo;
            delta = Math.Abs(baseTempo.Rate - measuredTempo.Rate);

            Assert.IsTrue(delta < .1M);
        }
    }
}
