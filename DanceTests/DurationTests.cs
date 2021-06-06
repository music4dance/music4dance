using DanceLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

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
                var d = new SongDuration(-1.0M);
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
                var d = new SongDuration(1.0M, new DurationType(DurationKind.Measure));
            }
            catch (ArgumentNullException)
            {
                return;
            }

            Assert.Fail();
        }

        private static SongDuration _d1 = new(90);
        private static SongDuration _d2 = new(180);
        private static SongDuration _d3 = new(181);
        private static SongDuration _d4 = new(5, new DurationType(DurationKind.Minute));

        [TestMethod]
        public void DurationShortFormats()
        {
            var s1 = _d1.Format(DurationFormat.Short);
            Equals("90s", s1);

            var s2 = _d2.Format(DurationFormat.Short);
            Equals("2m", s2);

            var s3 = _d3.Format(DurationFormat.Short);
            Equals("2m1s", s3);

            var s4 = _d4.Format(DurationFormat.Short);
            Equals("5m", s4);
        }

        [TestMethod]
        public void DurationLongFormats()
        {
            var s1 = _d1.Format(DurationFormat.Long);
            Equals("90 seconds", s1);

            var s2 = _d2.Format(DurationFormat.Long);
            Equals("2 minute(s)", s2);

            var s3 = _d3.Format(DurationFormat.Long);
            Equals("2 minutes 1 seconds", s3);

            var s4 = _d4.Format(DurationFormat.Long);
            Equals("5m", s4);
        }

        [TestMethod]
        public void DurationColonFormats()
        {
            var s1 = _d1.Format(DurationFormat.Long);
            Equals("00:01:30", s1);

            var s2 = _d2.Format(DurationFormat.Long);
            Equals("02:00", s2);

            var s3 = _d3.Format(DurationFormat.Long);
            Equals("00:02:01", s3);

            var s4 = _d4.Format(DurationFormat.Long);
            Equals("00:05:00", s4);
        }
    }
}