using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using m4dModels;

namespace m4dModels.Tests
{
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
        [ExpectedException(typeof(Exception))]
        public void FromValue_InvalidFormat_Throws()
        {
            DanceQueryItem.FromValue("!invalid");
        }

        [TestMethod]
        public void ToString_OutputsCorrectFormat()
        {
            var item = DanceQueryItem.FromValue("BOL+2|Fast:Tempo|Smooth:Style");
            var str = item.ToString();
            Assert.IsTrue(str.StartsWith("BOL+2|"));
            Assert.IsTrue(str.Contains("Fast:Tempo"));
            Assert.IsTrue(str.Contains("Smooth:Style"));
        }

        [TestMethod]
        public void Description_And_ShortDescription_IncludeTags()
        {
            var item = DanceQueryItem.FromValue("BOL+2|Fast:Tempo,Smooth:Style");
            Assert.AreEqual(
                @"Bolero (with at least 2 votes) [tags: Fast:Tempo,Smooth:Style]",
                item.Description);
            Assert.AreEqual(
                @"Bolero >= 2 [Fast:Tempo,Smooth:Style]",
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
    }
}
