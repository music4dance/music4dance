using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DanceLibrary;

namespace DanceTests
{
    [TestClass]
    public class MeterTests
    {
        [TestMethod]
        public void ValidStringConstructors()
        {
            Meter m1 = new Meter("3/4");
            Assert.AreEqual<int>(3, m1.Numerator, "Numerator wasn't set correctly in constructor");
            Assert.AreEqual<int>(4, m1.Denominator, "Denominator wasn't set correctly in constructor");

            Meter m2 = new Meter("4/4");
            Assert.AreEqual<int>(4, m2.Numerator, "Numerator wasn't set correctly in constructor");
            Assert.AreEqual<int>(4, m2.Denominator, "Denominator wasn't set correctly in constructor");

            Meter m3 = new Meter("2/4");
            Assert.AreEqual<int>(2, m3.Numerator, "Numerator wasn't set correctly in constructor");
            Assert.AreEqual<int>(4, m3.Denominator, "Denominator wasn't set correctly in constructor");
        }

        [TestMethod]
        public void InvalidStringConstructorFormat()
        {
            try
            {
                Meter m1 = new Meter("Measure 3/4");
            }
            catch (ArgumentOutOfRangeException e)
            {
                StringAssert.Contains(e.Message, Meter.MeterSyntaxError);
            }
        }

        [TestMethod]
        public void InvalidStringConstructorRandom()
        {
            try
            {
                Meter m1 = new Meter("asdjfi;w#(fkjldks");
            }
            catch (ArgumentOutOfRangeException e)
            {
                StringAssert.Contains(e.Message, Meter.MeterSyntaxError);
            }
        }

        [TestMethod]
        public void InvalidStringConstructorNumerator()
        {
            try
            {
                Meter m1 = new Meter("-3/4");
            }
            catch (ArgumentOutOfRangeException e)
            {
                StringAssert.Contains(e.Message, Meter.PositiveIntegerNumerator);
            }
        }

        [TestMethod]
        public void InvalidStringConstructorDenominator()
        {
            try
            {
                Meter m1 = new Meter("3/-4");
            }
            catch (ArgumentOutOfRangeException e)
            {
                StringAssert.Contains(e.Message, Meter.PositiveIntegerDenominator);
            }
        }

        [TestMethod]
        public void ValidNumericalConstructors()
        {
            Meter m1 = new Meter(3,4);
            Assert.AreEqual<int>(3, m1.Numerator, "Numerator wasn't set correctly in constructor");
            Assert.AreEqual<int>(4, m1.Denominator, "Denominator wasn't set correctly in constructor");

            Meter m2 = new Meter(4,4);
            Assert.AreEqual<int>(4, m2.Numerator, "Numerator wasn't set correctly in constructor");
            Assert.AreEqual<int>(4, m2.Denominator, "Denominator wasn't set correctly in constructor");

            Meter m3 = new Meter(2,4);
            Assert.AreEqual<int>(2, m3.Numerator, "Numerator wasn't set correctly in constructor");
            Assert.AreEqual<int>(4, m3.Denominator, "Denominator wasn't set correctly in constructor");
        }

        [TestMethod]
        public void InvalidConstructorNumerator()
        {
            try
            {
                Meter m1 = new Meter(-3,4);
            }
            catch (ArgumentOutOfRangeException e)
            {
                StringAssert.Contains(e.Message, Meter.PositiveIntegerNumerator);
            }
        }

        [TestMethod]
        public void InvalidConstructorDenominator()
        {
            try
            {
                Meter m1 = new Meter(3,-4);
            }
            catch (ArgumentOutOfRangeException e)
            {
                StringAssert.Contains(e.Message, Meter.PositiveIntegerDenominator);
            }
        }

        [TestMethod]
        public void InvalidConstructorZero()
        {
            try
            {
                Meter m1 = new Meter(0,0);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Don't assert anything more than that an AOR exception was thrown
            }
        }

        [TestMethod]
        public void TestStringOutput()
        {
            string c = "3/4";

            Meter m = new Meter(3, 4);

            string s1 = m.ToString();
            StringAssert.Equals(s1, c);
        }

        [TestMethod]
        public void TestHash()
        {
            Meter m1 = new Meter(3, 4);
            Assert.AreEqual(3 * 1009 + 4, m1.GetHashCode(), "Invalid Hash returned for 3/4");

            Meter m2 = new Meter(4, 4);
            Assert.AreEqual(4 * 1009 + 4, m2.GetHashCode(), "Invalid Hash returned for 4/4");

            Meter m3 = new Meter(2, 4);
            Assert.AreEqual(2 * 1009 + 4, m3.GetHashCode(), "Invalid Hash returned for 2/4");
        }

    }
}
