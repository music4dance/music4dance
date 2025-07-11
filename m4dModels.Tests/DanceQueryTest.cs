﻿using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class DanceQueryTest
    {
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

            q = q.AddDance("RUM");
            Assert.AreEqual("BOL,RUM", q.Query);

            var qX = q.MakeExclusive();
            Assert.AreEqual("AND,BOL,RUM", qX.Query);

            q = qX.MakeInclusive();
            Assert.AreEqual("BOL,RUM", q.Query);
        }
    }
}
