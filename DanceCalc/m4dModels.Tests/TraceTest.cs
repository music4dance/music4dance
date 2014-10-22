using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using m4dModels;

namespace m4dModels.Tests
{
    [TestClass]
    public class TraceTest
    {
        [TestMethod]
        public void TraceGeneral()
        {
            Assert.IsTrue(TraceLevels.General.TraceVerbose);
        }
    }
}
