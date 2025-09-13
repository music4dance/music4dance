using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
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
            var sep = ", ";
            var desc = tq.Description(ref sep);
            Assert.IsTrue(desc.Contains("including tag"));
            Assert.IsTrue(desc.Contains("excluding tag"));
            Assert.IsTrue(desc.Contains("Pop"));
            Assert.IsTrue(desc.Contains("Jazz"));
        }

        [TestMethod]
        public void TagQuery_Description_IncludeDancesAllInSongTags()
        {
            var tq = new TagQuery("^+Pop:Music|-Jazz:Music");
            var sep = ", ";
            var desc = tq.Description(ref sep);
            Assert.IsTrue(desc.Contains("including song or dance tag"));
            Assert.IsTrue(desc.Contains("excluding song or dance tag"));
            Assert.IsTrue(desc.Contains("Pop"));
            Assert.IsTrue(desc.Contains("Jazz"));
            // Should NOT contain the old problematic "song and dance tags" prefix
            Assert.IsFalse(desc.Contains("song and dance tags"));
        }

        [TestMethod]
        public void TagQuery_ShortDescription_IncludeDancesAllInSongTags()
        {
            var tq = new TagQuery("^+Pop:Music|-Jazz:Music");
            var sep = ", ";
            var desc = tq.ShortDescription(ref sep);
            Assert.IsTrue(desc.Contains("song+dance"));
            Assert.IsTrue(desc.Contains("inc"));
            Assert.IsTrue(desc.Contains("excl"));
            Assert.IsTrue(desc.Contains("Pop"));
            Assert.IsTrue(desc.Contains("Jazz"));
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
        public void TagQuery_Description_IncludeDancesAllInSongTags_OnlyWhenTagsPresent()
        {
            // Test the edge case: ^ prefix but no actual tags after filtering
            var tq = new TagQuery("^"); // Empty tag query with ^ prefix
            var sep = ", ";
            var desc = tq.Description(ref sep);
            // Should be empty or not contain "song or dance tag" text
            Assert.IsFalse(desc.Contains("song or dance tag"));
        }

        [TestMethod]
        public void TagQuery_Description_NoExtraWordIssue()
        {
            // Test that we don't get "songs song and dance tags" issue
            var tq = new TagQuery("^+Episode 10:Other");
            var sep = ", ";
            var desc = tq.Description(ref sep);
            // Should contain "including song or dance tag Episode 10"
            Assert.IsTrue(desc.Contains("including song or dance tag"));
            Assert.IsTrue(desc.Contains("Episode 10"));
            // Should NOT contain duplicate "song" words
            Assert.IsFalse(desc.Contains("songs song"));
        }

        [TestMethod]
        public void TagQuery_ShortDescription_OnlyWhenTagsPresent()
        {
            // Test that song+dance prefix only appears when there are actual tags
            var tq = new TagQuery("^"); // Empty tag query with ^ prefix
            var sep = ", ";
            var desc = tq.ShortDescription(ref sep);
            Assert.IsFalse(desc.Contains("song+dance"));
        }
    }
}
