using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DanceLibrary;
using System.Diagnostics;

namespace DanceTests
{
    [TestClass]
    public class TimingTests
    {
        [TestMethod]
        public void DurationNinetyCommon()
        {
            SongTiming st = new SongTiming(new Meter(4, 4), DurationType.FromKind(DurationKind.Second), 32M, 90M);

            Debug.WriteLine(st.ToString());

            Assert.AreEqual(st.ToString(), "MPM 4/4,Second,2.1333333333333333333333333333,90");

            Assert.AreEqual(90M, st.Length);
            Assert.AreEqual(1.5M, st.GetLengthIn(DurationKind.Minute));
            Assert.AreEqual(48M, st.GetLengthIn(DurationKind.Measure));
            Assert.AreEqual(48M * 4M, st.GetLengthIn(DurationKind.Beat));
        }

        [TestMethod]
        public void DurationSixtyWaltz()
        {
            SongTiming st = new SongTiming(new Meter(3, 4), DurationType.FromKind(DurationKind.Second), 53M, 60M);

            Debug.WriteLine(st.ToString());

            Assert.AreEqual(st.ToString(), "MPM 3/4,Second,2.65,60");

            Assert.AreEqual(60M, st.Length);
            Assert.AreEqual(1M, st.GetLengthIn(DurationKind.Minute));
            Assert.AreEqual(53M, st.GetLengthIn(DurationKind.Measure));
            Assert.AreEqual(53M * 3M, st.GetLengthIn(DurationKind.Beat));
        }

    }
}
