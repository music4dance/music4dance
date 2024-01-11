using System;
using DanceLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DanceTests
{
    [TestClass]
    public class TempoRangeTests
    {
        [TestMethod]
        public void ValidConstructors()
        {
            var t = new TempoRange(30, 34);
            Assert.AreEqual(30, t.Min);
            Assert.AreEqual(34, t.Max);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void InvalidConstructorZero()
        {
            var range = new TempoRange(0, 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void InvalidConstructorOrder()
        {
            var range = new TempoRange(26.0M, 22.0M);
        }

        [TestMethod]
        public void AverageTempo()
        {
            var t1 = new TempoRange(30, 34);
            Assert.AreEqual(32, t1.Average);

            var t2 = new TempoRange(30, 30);
            Assert.AreEqual(30, t2.Average);

            var t3 = new TempoRange(20.4M, 100.3M);
            Assert.AreEqual(60.35M, t3.Average);
        }

        [TestMethod]
        public void Formatting()
        {
            // This should give basic coverage of all of the formatting functions

            var t1 = new TempoRange(20.4M, 100.003M);
            var s1 = t1.ToString();
            Assert.AreEqual(s1, "20.40-100");

            var t2 = new TempoRange(25.01M, 25.01M);
            var s2 = t2.ToString();
            Assert.AreEqual(s2, "25.01");
        }

        [TestMethod]
        public void TestDelta()
        {
            var t = new TempoRange(20.4M, 100.003M);
            var d1 = t.CalculateDelta(100.003M);
            Assert.AreEqual(0M, d1);

            var d2 = t.CalculateDelta(20.4M);
            Assert.AreEqual(0M, d2);

            var d3 = t.CalculateDelta(30);
            Assert.AreEqual(0M, d3);

            var d4 = t.CalculateDelta(110);
            Assert.AreEqual(9.997M, d4);

            var d5 = t.CalculateDelta(20);
            Assert.AreEqual(-0.4M, d5);
        }

        [TestMethod]
        public void TestDeltaPercentFromPoint()
        {
            var t = new TempoRange(100, 100);
            var d1 = t.CalculateDeltaPercent(100);
            Assert.AreEqual(0M, d1);

            var d2 = t.CalculateDeltaPercent(110);
            Assert.AreEqual(10M, d2);

            var d3 = t.CalculateDeltaPercent(90);
            Assert.AreEqual(-10M, d3);
        }

        [TestMethod]
        public void TestDeltaPercentFromRange()
        {
            var t = new TempoRange(90, 100);
            var d1 = t.CalculateDeltaPercent(100);
            Assert.AreEqual(0M, d1);

            var d2 = t.CalculateDeltaPercent(110);
            Assert.AreEqual(10M, d2);

            var d3 = t.CalculateDeltaPercent(90);
            Assert.AreEqual(0M, d3);
        }


        [TestMethod]
        public void TestInclude()
        {
            var t1 = new TempoRange(24, 24);
            Assert.AreEqual(t1, new TempoRange(24, 24));

            var t2 = new TempoRange(26, 27);
            var t3 = t1.Include(t2);
            var t4 = t2.Include(t3);

            Assert.AreEqual(t3, t4);
            Assert.AreEqual(t3, new TempoRange(24, 27));
        }
    }
}
