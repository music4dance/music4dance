namespace m4dModels.Tests;

[TestClass]
public class DanceQueryTests
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        DanceMusicTester.LoadDances().Wait();
    }

    [TestMethod]
    public void GetODataFilter_ExclusiveGroups_OrsWithinGroupAndsBetweenGroups()
    {
        // "All" (exclusive) of Waltz (WLZ group: CSW, SWZ, VWZ, TGV) and Foxtrot
        // (FXT group: CFT, SFT, QST, PBD) should require at least one Waltz style
        // AND at least one Foxtrot style, not every style of both.
        var query = new DanceQuery("AND,WLZ,FXT");
        var odata = query.GetODataFilter(null);

        Assert.IsTrue(query.IsExclusive);

        const string expected =
            "(((dance_CSW/Votes ge 1) or (dance_SWZ/Votes ge 1) or (dance_VWZ/Votes ge 1) or (dance_TGV/Votes ge 1)) " +
            "and ((dance_CFT/Votes ge 1) or (dance_SFT/Votes ge 1) or (dance_QST/Votes ge 1) or (dance_PBD/Votes ge 1)))";
        Assert.AreEqual(expected, odata);
    }

    [TestMethod]
    public void GetODataFilter_InclusiveGroups_OrsEverything()
    {
        // "Any" (inclusive) of Waltz and Foxtrot should match a song with at least
        // one style from either group - equivalent to OR'ing every expanded dance.
        var query = new DanceQuery("WLZ,FXT");
        var odata = query.GetODataFilter(null);

        Assert.IsFalse(query.IsExclusive);
        Assert.IsFalse(odata.Contains(" and "));
        Assert.IsTrue(odata.Contains("dance_CSW/Votes ge 1"));
        Assert.IsTrue(odata.Contains("dance_CFT/Votes ge 1"));
    }

    [TestMethod]
    public void GetODataFilter_SimpleDance_NoGroupExpansion()
    {
        var query = new DanceQuery("BOL");
        var odata = query.GetODataFilter(null);
        Assert.AreEqual("((dance_BOL/Votes ge 1))", odata);
    }

    [TestMethod]
    public void GetODataFilter_SingleGroup_OrsMembersWithNoAnd()
    {
        // A lone group (nothing to be exclusive with) should still just OR its members.
        var query = new DanceQuery("WLZ");
        var odata = query.GetODataFilter(null);

        Assert.IsFalse(query.IsExclusive);
        const string expected =
            "(((dance_CSW/Votes ge 1) or (dance_SWZ/Votes ge 1) or (dance_VWZ/Votes ge 1) or (dance_TGV/Votes ge 1)))";
        Assert.AreEqual(expected, odata);
    }

    [TestMethod]
    public void GetODataFilter_MixedSimpleDanceAndGroup_Exclusive()
    {
        // "All" of a plain dance and a group: the plain dance needs no OR, the group's
        // members are OR'd, and the two are AND'd together.
        var query = new DanceQuery("AND,BOL,WLZ");
        var odata = query.GetODataFilter(null);

        Assert.IsTrue(query.IsExclusive);
        const string expected =
            "((dance_BOL/Votes ge 1) " +
            "and ((dance_CSW/Votes ge 1) or (dance_SWZ/Votes ge 1) or (dance_VWZ/Votes ge 1) or (dance_TGV/Votes ge 1)))";
        Assert.AreEqual(expected, odata);
    }

    [TestMethod]
    public void GetODataFilter_GroupWithThresholdAndTags_AppliesToEveryMember()
    {
        // A threshold/tag modifier on a group selection must apply to each expanded
        // member individually, since it's their per-dance vote counts/tags being tested.
        var query = new DanceQuery("WLZ+2|Fast:Tempo");
        var odata = query.GetODataFilter(null);

        foreach (var id in new[] { "CSW", "SWZ", "VWZ", "TGV" })
        {
            Assert.IsTrue(
                odata.Contains($"(dance_{id}/Votes ge 2 and dance_{id}/TempoTags/any(t: t eq 'Fast'))"),
                $"Missing threshold/tag filter for {id} in: {odata}");
        }
    }
}
