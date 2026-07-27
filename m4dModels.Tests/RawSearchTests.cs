namespace m4dModels.Tests;

[TestClass]
public class RawSearchTests
{
    [TestMethod]
    public void ExcludePublishers_SetsAndClearsBitIndependentlyOfExcludeDances()
    {
        var rawSearch = new RawSearch { ExcludeDances = true };

        rawSearch.ExcludePublishers = true;
        Assert.AreEqual(CruftFilter.NoPublishers | CruftFilter.NoDances, rawSearch.CruftFilter);
        Assert.IsTrue(rawSearch.ExcludeDances, "Setting ExcludePublishers should not clear ExcludeDances");

        rawSearch.ExcludePublishers = false;
        Assert.AreEqual(CruftFilter.NoDances, rawSearch.CruftFilter);
        Assert.IsTrue(rawSearch.ExcludeDances);
    }

    [TestMethod]
    public void ExcludeDances_SetsAndClearsBitIndependentlyOfExcludePublishers()
    {
        var rawSearch = new RawSearch { ExcludePublishers = true };

        rawSearch.ExcludeDances = true;
        Assert.AreEqual(CruftFilter.NoPublishers | CruftFilter.NoDances, rawSearch.CruftFilter);
        Assert.IsTrue(rawSearch.ExcludePublishers, "Setting ExcludeDances should not clear ExcludePublishers");

        rawSearch.ExcludeDances = false;
        Assert.AreEqual(CruftFilter.NoPublishers, rawSearch.CruftFilter);
        Assert.IsTrue(rawSearch.ExcludePublishers);
    }

    [TestMethod]
    public void ExcludeUnconfirmedDances_SetsAndClearsBitIndependentlyOfOtherFlags()
    {
        var rawSearch = new RawSearch { ExcludePublishers = true, ExcludeDances = true };

        rawSearch.ExcludeUnconfirmedDances = true;
        Assert.AreEqual(CruftFilter.AllCruft, rawSearch.CruftFilter);
        Assert.IsTrue(rawSearch.ExcludePublishers);
        Assert.IsTrue(rawSearch.ExcludeDances);

        rawSearch.ExcludeUnconfirmedDances = false;
        Assert.AreEqual(CruftFilter.NoPublishers | CruftFilter.NoDances, rawSearch.CruftFilter);
        Assert.IsFalse(rawSearch.ExcludeUnconfirmedDances);
    }

    [TestMethod]
    public void CruftFilter_DefaultsToNoCruft_WithAllFlagsFalse()
    {
        var rawSearch = new RawSearch();

        Assert.AreEqual(CruftFilter.NoCruft, rawSearch.CruftFilter);
        Assert.IsFalse(rawSearch.ExcludePublishers);
        Assert.IsFalse(rawSearch.ExcludeDances);
        Assert.IsFalse(rawSearch.ExcludeUnconfirmedDances);
    }
}
