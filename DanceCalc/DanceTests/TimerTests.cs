using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DanceLibrary;
using System.Diagnostics;

namespace DanceTests
{
    [TestClass]
    public class TimerTests
    {
        private SongTimer PrimeTimer()
        {
            SongTimer st = new SongTimer();

            for (int i = 0; i < 6; i++)
            {
                st.DoClick();
                System.Threading.Thread.Sleep(100);
            }

            Debug.WriteLine(st.Rate);

            return st;
        }

        [TestMethod]
        public void Timer()
        {
            SongTimer st = PrimeTimer();

            decimal rate = st.Rate;            
            decimal delta = Math.Abs(.1M - st.Rate);

            Assert.IsTrue(delta < .01M);
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
    }
}
