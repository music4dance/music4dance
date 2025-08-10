using System;
using System.Diagnostics;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class DanceQueryTest
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            var t = DanceMusicTester.LoadDances().Result;
            Trace.WriteLine($"Loaded dances = {t}");
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
            Assert.AreEqual(qAnd.ODataFilter, qAndX.ODataFilter);
            Assert.AreEqual(qAnd.IsExclusive, qAndX.IsExclusive);

            // OOX should behave as inclusive (no prefix)
            var qOneOfX = new DanceQuery("OOX,BOL,RMB");
            var qOneOf = new DanceQuery("BOL,RMB");
            Assert.AreEqual(qOneOf.ODataFilter, qOneOfX.ODataFilter);
            Assert.AreEqual(qOneOf.IsExclusive, qOneOfX.IsExclusive);

            // Mixed: inferred operator should be normalized
            var qMixed = new DanceQuery("ADX,BOL,RMB");
            Assert.IsTrue(qMixed.IsExclusive);
            var qMixedInclusive = qMixed.MakeInclusive();
            Assert.IsFalse(qMixedInclusive.IsExclusive);
            Assert.IsFalse(qMixedInclusive.Query.StartsWith("AND,", StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
