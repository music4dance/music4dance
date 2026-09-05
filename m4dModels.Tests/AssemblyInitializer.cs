using System.Globalization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Assembly-level initialization for all tests.
/// </summary>
[TestClass]
public class AssemblyInitializer
{
    [AssemblyInitialize]
    public static void AssemblySetup(TestContext context)
    {
        // Tests assume "." as the decimal separator and no thousands grouping (e.g. parsing
        // "125.0" as 125, building OData filters with "99.5"). On a non-English OS locale
        // (e.g. German, which uses "," for decimals and "." for grouping) these break in
        // culture-dependent ways. Pin the test process to InvariantCulture so results don't
        // depend on the machine's regional settings.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }
}
