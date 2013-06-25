using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DanceLibrary;
using System.Diagnostics;

namespace DanceTests
{
    [TestClass]
    public class TimerTests
    {
        private SongTimer PrimeTimer(int delay = 100)
        {
            SongTimer st = new SongTimer();

            for (int i = 0; i < 6; i++)
            {
                st.DoClick();
                System.Threading.Thread.Sleep(delay);
            }

            Debug.WriteLine(st.Rate);

            return st;
        }

        [TestMethod]
        public void Timer()
        {
            SongTimer st = PrimeTimer();

            decimal rate = st.Rate;
            decimal delta = Math.Abs(10M - st.Rate);

            Assert.IsTrue(delta < .1M);
        }

        [TestMethod]
        public void TimerAutoReset()
        {
            SongTimer st = PrimeTimer();

            System.Threading.Thread.Sleep(110);
            st.DoClick();

            Assert.IsTrue(st.IsClear);
        }

        [TestMethod]
        public void TimerManualReset()
        {
            SongTimer st = PrimeTimer();

            st.Reset();

            Assert.IsTrue(st.IsClear);
        }

        [TestMethod]
        public void TimerAndTiming()
        {
            SongTimer st = PrimeTimer(1875);

            decimal rate = st.Rate;
            decimal delta = Math.Abs(.533M - st.Rate);

            Assert.IsTrue(delta < .1M);

            Tempo baseTempo = new Tempo(32M, new TempoType(TempoKind.MPM, new Meter(4, 4)));
            SongTiming timing = new SongTiming(baseTempo, 64M, DurationKind.Measure);
            timing.SetRate(rate);

            Tempo measuredTempo = timing.Tempo;
            delta = Math.Abs(baseTempo.Rate - measuredTempo.Rate);

            Assert.IsTrue(delta < .1M);
        }

    }
}
