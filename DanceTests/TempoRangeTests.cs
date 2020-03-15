using DanceLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.CodeAnalysis;

namespace DanceTests
{
    [TestClass]
    public class TempoRangeTests
    {
        [TestMethod]
        public void ValidConstructors()
        {
            TempoRange t = new TempoRange(30, 34);
            Assert.AreEqual(30, t.Min);
            Assert.AreEqual(34, t.Max);
        }

        [TestMethod]
        public void InvalidConstructorZero()
        {
            try
            {
                TempoRange t = new TempoRange(0, 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            Assert.Fail();
        }

        [TestMethod]
        public void InvalidConstructorOrder()
        {
            try
            {
                TempoRange t = new TempoRange(26.0M, 22.0M);
            }
            catch (ArgumentException)
            {
                return;
            }

            Assert.Fail();
        }

        [TestMethod]
        public void AverageTempo()
        {
            TempoRange t1 = new TempoRange(30, 34);
            Assert.AreEqual(32, t1.Average);

            TempoRange t2 = new TempoRange(30, 30);
            Assert.AreEqual(30, t2.Average);

            TempoRange t3 = new TempoRange(20.4M, 100.3M);
            Assert.AreEqual(60.35M, t3.Average);
        }

        [TestMethod]
        public void Formatting()
        {
            // This should give basic coverage of all of the formatting functions

            TempoRange t1 = new TempoRange(20.4M, 100.003M);
            string s1 = t1.ToString();
            StringAssert.Equals(s1, "20.4-100");

            TempoRange t2 = new TempoRange(25.01M, 25.01M);
            string s2 = t2.ToString();
            StringAssert.Equals(s2, "25.01");
        }

        [TestMethod]
        public void TestDelta()
        {
            TempoRange t = new TempoRange(20.4M, 100.003M);
            decimal d1 = t.CalculateDelta(100.003M);
            Assert.AreEqual(0M, d1);

            decimal d2 = t.CalculateDelta(20.4M);
            Assert.AreEqual(0M, d2);

            decimal d3 = t.CalculateDelta(30);
            Assert.AreEqual(0M, d3);

            decimal d4 = t.CalculateDelta(110);
            Assert.AreEqual(9.997M, d4);

            decimal d5 = t.CalculateDelta(20);
            Assert.AreEqual(-0.4M, d5);
        }

        [TestMethod]
        public void TestInclude()
        {
            TempoRange t1 = new TempoRange(24, 24);
            Assert.AreEqual(t1, new TempoRange(24, 24));

            TempoRange t2 = new TempoRange(26,27);
            TempoRange t3 = t1.Include(t2);
            TempoRange t4 = t2.Include(t3);

            Assert.AreEqual(t3, t4);
            Assert.AreEqual(t3, new TempoRange(24, 27));
        }
    }
}
