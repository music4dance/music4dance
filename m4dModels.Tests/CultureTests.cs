using System.Globalization;

namespace m4dModels.Tests;

/// <summary>
/// Guards against regressing the culture pin in <see cref="AssemblyInitializer"/>. Without
/// it, running the suite on a non-English OS locale (e.g. German, which uses "," for decimals
/// and "." for grouping) breaks decimal parsing/formatting throughout the suite - see
/// local/sandbox-bugs.txt.
/// </summary>
[TestClass]
public class CultureTests
{
    [TestMethod]
    public void CurrentCulture_UsesInvariantDecimalFormatting()
    {
        Assert.AreEqual(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        Assert.AreEqual(125.0, double.Parse("125.0", CultureInfo.CurrentCulture));
        Assert.AreEqual("99.5", 99.5.ToString(CultureInfo.CurrentCulture));
    }
}
