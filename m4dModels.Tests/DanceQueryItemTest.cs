using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Diagnostics;

namespace m4dModels.Tests;

[TestClass]
public class DanceQueryItemTest
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        DanceMusicTester.LoadDances().Wait();
    }

    [TestMethod]
    public void FromValue_ParsesIdThresholdAndTags()
    {
        var item = DanceQueryItem.FromValue("BOL+2|Fast:Tempo|Smooth:Style");
        Assert.AreEqual("BOL", item.Id);
        Assert.AreEqual(2, item.Threshold);
        Assert.IsNotNull(item.TagQuery);
        Assert.IsTrue(item.TagQuery.TagList.Tags.Contains("Fast:Tempo"));
        Assert.IsTrue(item.TagQuery.TagList.Tags.Contains("Smooth:Style"));
    }

    [TestMethod]
    public void FromValue_ParsesNegativeThreshold()
    {
        var item = DanceQueryItem.FromValue("RMB-3|Fun:Other");
        Assert.AreEqual("RMB", item.Id);
        Assert.AreEqual(-3, item.Threshold);
        Assert.IsTrue(item.TagQuery.TagList.Tags.Contains("Fun:Other"));
    }

    [TestMethod]
    public void FromValue_ParsesNoThresholdOrTags()
    {
        var item = DanceQueryItem.FromValue("SWG");
        Assert.AreEqual("SWG", item.Id);
        Assert.AreEqual(1, item.Threshold);
        Assert.IsTrue(item.TagQuery.TagList.IsEmpty);
    }

    [TestMethod]
    public void FromValue_InvalidFormat_Throws()
    {
        _ = Assert.ThrowsExactly<Exception>(() => _ = DanceQueryItem.FromValue("!invalid"));
    }

    [TestMethod]
    public void ToString_OutputsCorrectFormat()
    {
        var item = DanceQueryItem.FromValue("BOL+2|Fast:Tempo|Smooth:Style");
        var str = item.ToString();
        Assert.AreEqual("BOL+2|Fast:Tempo|Smooth:Style", str);
    }

    [TestMethod]
    public void Description_IncludeTags()
    {
        var item = DanceQueryItem.FromValue("BOL+2|Fast:Tempo,Smooth:Style");
        Trace.WriteLine($"Description: {item.Description}");
        Assert.AreEqual(
            @"Bolero (with at least 2 votes, including tag Fast)",
            item.Description);
    }

    [TestMethod]
    public void ShortDescription_IncludeTags()
    {
        var item = DanceQueryItem.FromValue("BOL+2|Fast:Tempo,Smooth:Style");
        Trace.WriteLine($"ShortDescription: {item.ShortDescription}");
        Assert.AreEqual(
            @"Bolero (>=2, inc Fast)",
            item.ShortDescription);
    }

    [TestMethod]
    public void DanceProperty_ResolvesDance()
    {
        var item = DanceQueryItem.FromValue("BOL");
        Assert.IsNotNull(item.Dance);
        Assert.AreEqual("BOL", item.Dance.Id);
    }

    [TestMethod]
    public void TagQuery_ODataFilterForDanceField()
    {
        var item = DanceQueryItem.FromValue("BOL|+Fast:Tempo|-Smooth:Style");
        var odata = item.TagQuery.GetODataFilterForDanceField("dance_BOL");
        Assert.IsTrue(odata.Contains("dance_BOL/TempoTags/any(t: t eq 'Fast')"));
        Assert.IsTrue(odata.Contains("dance_BOL/StyleTags/all(t: t ne 'Smooth')"));
    }

    [TestMethod]
    public void Description_NoModifier()
    {
        var item = DanceQueryItem.FromValue("WLZ");
        Assert.AreEqual("Waltz", item.Description);
        Assert.AreEqual("Waltz", item.ShortDescription);
    }

    [TestMethod]
    public void Description_JustThreshold()
    {
        var item = DanceQueryItem.FromValue("WLZ+3");
        Assert.AreEqual("Waltz (with at least 3 votes)", item.Description);
        Assert.AreEqual("Waltz (>=3)", item.ShortDescription);
    }

    [TestMethod]
    public void Description_JustTags()
    {
        var item = DanceQueryItem.FromValue("WLZ|Pop:Music");
        var expectedDesc = $"Waltz (including tag Pop)";
        var expectedShort = $"Waltz (inc Pop)";
        Assert.AreEqual(expectedDesc, item.Description);
        Assert.AreEqual(expectedShort, item.ShortDescription);
    }

    [TestMethod]
    public void Description_ThresholdAndTags()
    {
        var item = DanceQueryItem.FromValue("WLZ+2|Pop:Music");
        var expectedDesc = $"Waltz (with at least 2 votes, including tag Pop)";
        var expectedShort = $"Waltz (>=2, inc Pop)";
        Assert.AreEqual(expectedDesc, item.Description);
        Assert.AreEqual(expectedShort, item.ShortDescription);
    }
}
