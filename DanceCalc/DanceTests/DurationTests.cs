using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DanceLibrary;

namespace DanceTests
{
    [TestClass]
    public class DurationTests
    {
        [TestMethod]
        public void DurationInvalidConstructorLength()
        {
            try
            {
                SongDuration d = new SongDuration(-1.0M);
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            Assert.Fail();
        }

        [TestMethod]
        public void DurationInvalidConstructorType()
        {
            try
            {
                SongDuration d = new SongDuration(1.0M,new DurationType(DurationKind.Measure));
            }
            catch (ArgumentNullException)
            {
                return;
            }

            Assert.Fail();
        }

        private static SongDuration _d1 = new SongDuration(90);
        private static SongDuration _d2 = new SongDuration(180);
        private static SongDuration _d3 = new SongDuration(181);
        private static SongDuration _d4 = new SongDuration(5, new DurationType(DurationKind.Minute));

        [TestMethod]
        public void DurationShortFormats()
        {
            string s1 = _d1.Format(DurationFormat.Short);
            StringAssert.Equals("90s", s1);

            string s2 = _d2.Format(DurationFormat.Short);
            StringAssert.Equals("2m", s2);

            string s3 = _d3.Format(DurationFormat.Short);
            StringAssert.Equals("2m1s", s3);

            string s4 = _d4.Format(DurationFormat.Short);
            StringAssert.Equals("5m", s4);
        }

        [TestMethod]
        public void DurationLongFormats()
        {
            string s1 = _d1.Format(DurationFormat.Long);
            StringAssert.Equals("90 seconds", s1);

            string s2 = _d2.Format(DurationFormat.Long);
            StringAssert.Equals("2 minute(s)", s2);

            string s3 = _d3.Format(DurationFormat.Long);
            StringAssert.Equals("2 minutes 1 seconds", s3);

            string s4 = _d4.Format(DurationFormat.Long);
            StringAssert.Equals("5m", s4);
        }

        [TestMethod]
        public void DurationColonFormats()
        {
            string s1 = _d1.Format(DurationFormat.Long);
            StringAssert.Equals("00:01:30", s1);

            string s2 = _d2.Format(DurationFormat.Long);
            StringAssert.Equals("02:00", s2);

            string s3 = _d3.Format(DurationFormat.Long);
            StringAssert.Equals("00:02:01", s3);

            string s4 = _d4.Format(DurationFormat.Long);
            StringAssert.Equals("00:05:00", s4);
        }

    }
}
