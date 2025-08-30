using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class DanceQueryTest
    {
        private static DanceMusicCoreService _database = null;
        [ClassInitialize]
        public static async Task ClassInitialize(TestContext _)
        {
            _database =  await DanceMusicTester.CreateService("Cleanup");
            //var t = DanceMusicTester.LoadDances().Result;
            //Trace.WriteLine($"Loaded dances = {t}");
        }

        [TestMethod]
        public void BasicDanceQuery()
        {
            var q = new DanceQuery();
            Assert.AreEqual(string.Empty, q.Query);
            Assert.IsFalse(q.IsExclusive);
            Assert.AreEqual(0, q.Dances.Count());

            q = new DanceQuery("ALL");
            Assert.AreEqual(string.Empty, q.Query);
            Assert.IsFalse(q.IsExclusive);
            Assert.AreEqual(0, q.Dances.Count());

            q = new DanceQuery("AND,FXT,SWG");
            Assert.AreEqual("AND,FXT,SWG", q.Query);
            Assert.IsTrue(q.IsExclusive);
            Assert.AreEqual(2, q.Dances.Count());
            Assert.IsTrue(q.HasDance("fxt"));
            Assert.IsFalse(q.HasDance("BOL"));
        }

        [TestMethod]
        public void DanceQueryMath()
        {
            var q = new DanceQuery();
            q = q.AddDance("BOL");
            Assert.AreEqual("BOL", q.Query);
            Assert.IsFalse(q.IsExclusive);
            Assert.AreEqual(1, q.Dances.Count());
            Assert.IsTrue(q.HasDance("BOL"));
            Assert.IsFalse(q.HasDance("RMB"));

            q = q.AddDance("RMB");
            Assert.AreEqual("BOL,RMB", q.Query);
            Assert.IsFalse(q.IsExclusive);
            Assert.AreEqual(2, q.Dances.Count());
            Assert.IsTrue(q.HasDance("BOL"));
            Assert.IsTrue(q.HasDance("RMB"));

            var qX = q.MakeExclusive();
            Assert.AreEqual("AND,BOL,RMB", qX.Query);
            Assert.IsTrue(qX.IsExclusive);
            Assert.AreEqual(2, qX.Dances.Count());
            Assert.IsTrue(qX.HasDance("BOL"));
            Assert.IsTrue(qX.HasDance("RMB"));

            q = qX.MakeInclusive();
            Assert.AreEqual("BOL,RMB", q.Query);
            Assert.IsFalse(q.IsExclusive);
            Assert.AreEqual(2, q.Dances.Count());
            Assert.IsTrue(q.HasDance("BOL"));
            Assert.IsTrue(q.HasDance("RMB"));
        }

        [TestMethod]
        public void InferredOperators_AreMappedToExplicit()
        {
            // ANDX should behave as AND
            var qAndX = new DanceQuery("ADX,BOL,RMB");
            var qAnd = new DanceQuery("AND,BOL,RMB");
            Assert.AreEqual(qAnd.GetODataFilter(_database), qAndX.GetODataFilter(_database));
            Assert.AreEqual(qAnd.IsExclusive, qAndX.IsExclusive);

            // OOX should behave as inclusive (no prefix)
            var qOneOfX = new DanceQuery("OOX,BOL,RMB");
            var qOneOf = new DanceQuery("BOL,RMB");
            Assert.AreEqual(qOneOf.GetODataFilter(_database), qOneOfX.GetODataFilter(_database));
            Assert.AreEqual(qOneOf.IsExclusive, qOneOfX.IsExclusive);

            // Mixed: inferred operator should be normalized
            var qMixed = new DanceQuery("ADX,BOL,RMB");
            Assert.IsTrue(qMixed.IsExclusive);
            var qMixedInclusive = qMixed.MakeInclusive();
            Assert.IsFalse(qMixedInclusive.IsExclusive);
            Assert.IsFalse(qMixedInclusive.Query.StartsWith("AND,", StringComparison.InvariantCultureIgnoreCase));
        }

        [TestMethod]
        public void DanceQuery_Items_ParsesTagsAndThresholds()
        {
            var q = new DanceQuery("BOL+2|Fast:Tempo|Smooth:Style,RMB-1|Fun:Other");
            var items = q.Items.ToList();
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("BOL", items[0].Id);
            Assert.AreEqual(2, items[0].Threshold);
            Assert.IsTrue(items[0].TagQuery.TagList.Tags.Contains("Fast:Tempo"));
            Assert.IsTrue(items[0].TagQuery.TagList.Tags.Contains("Smooth:Style"));
            Assert.AreEqual("RMB", items[1].Id);
            Assert.AreEqual(-1, items[1].Threshold);
            Assert.IsTrue(items[1].TagQuery.TagList.Tags.Contains("Fun:Other"));
        }

        [TestMethod]
        public void DanceQuery_ODataFilter_PerDanceTags()
        {
            var q = new DanceQuery("BOL+2|Fast:Tempo|Smooth:Style,RMB-1|Fun:Other");
            var odata = q.GetODataFilter(_database);
            // Should contain per-dance field and tag filters
            Assert.IsTrue(odata.Contains("dance_BOL/Votes ge 2"));
            Assert.IsTrue(odata.Contains("dance_BOL/TempoTags/any(t: t eq 'Fast')"));
            Assert.IsTrue(odata.Contains("dance_BOL/StyleTags/any(t: t eq 'Smooth')"));
            Assert.IsTrue(odata.Contains("dance_RMB/Votes le 1"));
            Assert.IsTrue(odata.Contains("dance_RMB/OtherTags/any(t: t eq 'Fun')"));
        }

        [TestMethod]
        public void DanceQuery_ODataFilter_TagInclusionExclusion()
        {
            var q = new DanceQuery("BOL|+Fast:Tempo|-Smooth:Style");
            var odata = q.GetODataFilter(_database);
            Assert.IsTrue(odata.Contains("dance_BOL/TempoTags/any(t: t eq 'Fast')"));
            Assert.IsTrue(odata.Contains("dance_BOL/StyleTags/all(t: t ne 'Smooth')"));
        }

        [TestMethod]
        public void DanceQuery_ToString()
        {
            var q = new DanceQuery("BOL+2|Fast:Tempo|Smooth:Style,RMB-1|Fun:Other");
            Assert.AreEqual(
                @"songs danceable to any of Bolero (with at least 2 votes) [tags: Fast:Tempo, Smooth:Style] or Rumba (with at most 1 votes) [tags: Fun:Other]",
                q.ToString());
        }

        [TestMethod]
        public void DanceQuery_ShortDescription()
        {
            var q = new DanceQuery("BOL+2|Fast:Tempo|Smooth:Style,RMB-1|Fun:Other");
            Assert.AreEqual(
                @"Bolero >= 2 [Fast:Tempo,Smooth:Style], Rumba <= 1 [Fun:Other]",
                q.ShortDescription);
        }

        [TestMethod]
        public void DanceQuery_HasDance_Works()
        {
            var q = new DanceQuery("BOL,RMB");
            Assert.IsTrue(q.HasDance("BOL"));
            Assert.IsTrue(q.HasDance("RMB"));
            Assert.IsFalse(q.HasDance("SWG"));
        }

        [TestMethod]
        public void DanceQuery_AddDance_Works()
        {
            var q = new DanceQuery("BOL");
            var q2 = q.AddDance("RMB");
            Assert.AreEqual("BOL,RMB", q2.Query);
            Assert.IsTrue(q2.HasDance("RMB"));
        }

        [TestMethod]
        public void DanceQuery_MakeInclusive_And_Exclusive()
        {
            var q = new DanceQuery("BOL,RMB");
            var qEx = q.MakeExclusive();
            Assert.IsTrue(qEx.IsExclusive);
            Assert.IsTrue(qEx.Query.StartsWith("AND,"));
            var qIn = qEx.MakeInclusive();
            Assert.IsFalse(qIn.IsExclusive);
            Assert.IsFalse(qIn.Query.StartsWith("AND,"));
        }

        [TestMethod]
        public void DanceQuery_EmptyAndAll()
        {
            var q = new DanceQuery();
            Assert.IsTrue(q.All);
            q = new DanceQuery("ALL");
            Assert.IsTrue(q.All);
        }
    }
}
