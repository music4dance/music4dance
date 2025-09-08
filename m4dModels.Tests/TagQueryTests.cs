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
                _rings = rings != null ? new List<string>(rings) : new List<string>();
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
            var dms = new MockDms(new[] { "Jazz:Music" });
            var odata = tq.GetODataFilter(dms);
            Assert.IsTrue(odata.Contains("Jazz"));
            Assert.IsTrue(odata.Contains("all"));
        }

        [TestMethod]
        public void TagQuery_GetODataFilter_IncludeAndExclude()
        {
            var tq = new TagQuery("+Pop:Music|-Jazz:Music");
            var dms = new MockDms(new[] { "Pop:Music", "Jazz:Music" });
            var odata = tq.GetODataFilter(dms);
            Assert.IsTrue(odata.Contains("Pop"));
            Assert.IsTrue(odata.Contains("Jazz"));
            Assert.IsTrue(odata.Contains("any"));
            Assert.IsTrue(odata.Contains("all"));
        }
    }
}
