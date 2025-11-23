namespace DanceLibrary.Tests;

[TestClass]
public class MeterTests
{
    [TestMethod]
    public void ValidStringConstructors()
    {
        var m1 = new Meter("3/4");
        Assert.AreEqual(3, m1.Numerator, "Numerator wasn't set correctly in constructor");
        Assert.AreEqual(
            4, m1.Denominator,
            "Denominator wasn't set correctly in constructor");

        var m2 = new Meter("4/4");
        Assert.AreEqual(4, m2.Numerator, "Numerator wasn't set correctly in constructor");
        Assert.AreEqual(
            4, m2.Denominator,
            "Denominator wasn't set correctly in constructor");

        var m3 = new Meter("2/4");
        Assert.AreEqual(2, m3.Numerator, "Numerator wasn't set correctly in constructor");
        Assert.AreEqual(
            4, m3.Denominator,
            "Denominator wasn't set correctly in constructor");
    }

    [TestMethod]
    public void InvalidStringConstructorFormat()
    {
        try
        {
            _ = new Meter("Measure 3/4");
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
            _ = new Meter("asdjfi;w#(fkjldks");
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
            _ = new Meter("-3/4");
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
            _ = new Meter("3/-4");
        }
        catch (ArgumentOutOfRangeException e)
        {
            StringAssert.Contains(e.Message, Meter.PositiveIntegerDenominator);
        }
    }

    [TestMethod]
    public void ValidNumericalConstructors()
    {
        var m1 = new Meter(3, 4);
        Assert.AreEqual(3, m1.Numerator, "Numerator wasn't set correctly in constructor");
        Assert.AreEqual(
            4, m1.Denominator,
            "Denominator wasn't set correctly in constructor");

        var m2 = new Meter(4, 4);
        Assert.AreEqual(4, m2.Numerator, "Numerator wasn't set correctly in constructor");
        Assert.AreEqual(
            4, m2.Denominator,
            "Denominator wasn't set correctly in constructor");

        var m3 = new Meter(2, 4);
        Assert.AreEqual(2, m3.Numerator, "Numerator wasn't set correctly in constructor");
        Assert.AreEqual(
            4, m3.Denominator,
            "Denominator wasn't set correctly in constructor");
    }

    [TestMethod]
    public void InvalidConstructorNumerator()
    {
        try
        {
            _ = new Meter(-3, 4);
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
            _ = new Meter(3, -4);
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
            _ = new Meter(0, 0);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Don't assert anything more than that an AOR exception was thrown
        }
    }

    [TestMethod]
    public void TestStringOutput()
    {
        var c = "3/4";

        var m = new Meter(3, 4);

        var s1 = m.ToString();
        _ = Equals(s1, c);
    }

    [TestMethod]
    public void TestHash()
    {
        var m1 = new Meter(3, 4);
        Assert.AreEqual(3 * 1009 + 4, m1.GetHashCode(), "Invalid Hash returned for 3/4");

        var m2 = new Meter(4, 4);
        Assert.AreEqual(4 * 1009 + 4, m2.GetHashCode(), "Invalid Hash returned for 4/4");

        var m3 = new Meter(2, 4);
        Assert.AreEqual(2 * 1009 + 4, m3.GetHashCode(), "Invalid Hash returned for 2/4");
    }
}
