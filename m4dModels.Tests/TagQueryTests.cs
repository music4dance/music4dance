namespace m4dModels.Tests;

[TestClass]
public class TagQueryTests
{
    private class MockDms : DanceMusicCoreService
    {
        private readonly List<string> _rings;
        public MockDms(IEnumerable<string> rings = null)
            : base(null, null, null)
        {
            _rings = rings != null ? [.. rings] : [];
        }
        public override ICollection<TagGroup> GetTagRings(TagList tags)
        {
            // Return TagGroups with Key set to the tag string
            var result = new List<TagGroup>();
            foreach (var tag in tags.ToStringList())
            {
                result.Add(new TagGroup { Key = tag });
            }
            return result;
        }
    }

    [TestMethod]
    public void TagQuery_ConstructsAndParsesTags()
    {
        var tq = new TagQuery("+Pop:Music|-Jazz:Music");
        Assert.IsNotNull(tq.TagList);
        Assert.IsTrue(tq.TagList.ToStringList().Contains("+Pop:Music"));
        Assert.IsTrue(tq.TagList.ToStringList().Contains("-Jazz:Music"));
    }

    [TestMethod]
    public void TagQuery_DescribeTags_IncludesAndExcludes()
    {
        var tq = new TagQuery("+Pop:Music|-Jazz:Music");
        var sep = "";
        var desc = tq.Description(ref sep);
        // Expecting the full description string for these tags
        var expected = "including tag Pop, excluding tag Jazz";
        Assert.AreEqual(expected, desc);
    }

    [TestMethod]
    public void TagQuery_Description_ExcludeDanceTags_False_DefaultIncludesDanceALL()
    {
        var tq = new TagQuery("+Pop:Music|-Jazz:Music");
        var sep = "";
        var desc = tq.Description(ref sep);
        var expected = "including tag Pop, excluding tag Jazz";
        Assert.AreEqual(expected, desc);
    }

    [TestMethod]
    public void TagQuery_ShortDescription_ExcludeDanceTags_False_DefaultIncludesDanceALL()
    {
        var tq = new TagQuery("+Pop:Music|-Jazz:Music");
        var sep = "";
        var desc = tq.ShortDescription(ref sep);
        var expected = "inc Pop, excl Jazz";
        Assert.AreEqual(expected, desc);
    }

    [TestMethod]
    public void TagQuery_TagFromFacetId_And_TagFromClassName()
    {
        Assert.AreEqual("Music", TagQuery.TagFromFacetId("GenreTags"));
        Assert.AreEqual("Music", TagQuery.TagFromClassName("Genre"));
        Assert.AreEqual("Style", TagQuery.TagFromClassName("Style"));
    }

    [TestMethod]
    public void TagQuery_GetODataFilter_ReturnsNullForEmpty()
    {
        var tq = new TagQuery("");
        var dms = new MockDms();
        Assert.IsNull(tq.GetODataFilter(dms));
    }

    [TestMethod]
    public void TagQuery_GetODataFilter_BasicInclude()
    {
        var tq = new TagQuery("+Pop:Music");
        var dms = new MockDms(new[] { "Pop:Music" });
        var odata = tq.GetODataFilter(dms);
        Assert.IsTrue(odata.Contains("Pop"));
        Assert.IsTrue(odata.Contains("any"));
    }

    [TestMethod]
    public void TagQuery_GetODataFilter_BasicExclude()
    {
        var tq = new TagQuery("-Jazz:Music");
        var dms = new MockDms(["Jazz:Music"]);
        var odata = tq.GetODataFilter(dms);
        Assert.IsTrue(odata.Contains("Jazz"));
        Assert.IsTrue(odata.Contains("all"));
    }

    [TestMethod]
    public void TagQuery_GetODataFilter_IncludeAndExclude()
    {
        var tq = new TagQuery("+Pop:Music|-Jazz:Music");
        var dms = new MockDms(["Pop:Music", "Jazz:Music"]);
        var odata = tq.GetODataFilter(dms);
        Assert.IsTrue(odata.Contains("Pop"));
        Assert.IsTrue(odata.Contains("Jazz"));
        Assert.IsTrue(odata.Contains("any"));
        Assert.IsTrue(odata.Contains("all"));
    }

    [TestMethod]
    public void TagQuery_Description_ExcludeDanceTags_True_OnlyWhenTagsPresent()
    {
        var tq = new TagQuery("^");
        var sep = ", ";
        var desc = tq.Description(ref sep);
        // Should be empty string when no tags present
        var expected = "";
        Assert.AreEqual(expected, desc);
    }

    [TestMethod]
    public void TagQuery_Description_NoExtraWordIssue()
    {
        var tq = new TagQuery("+Episode 10:Other");
        var sep = "";
        var desc = tq.Description(ref sep);
        var expected = "including tag Episode 10";
        Assert.AreEqual(expected, desc);
    }

    [TestMethod]
    public void TagQuery_ShortDescription_ExcludeDanceTags_True_OnlyWhenTagsPresent()
    {
        var tq = new TagQuery("^");
        var sep = ", ";
        var desc = tq.ShortDescription(ref sep);
        // Should be empty string when no tags present
        var expected = "";
        Assert.AreEqual(expected, desc);
    }
}
