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
#if DEBUG
            Assert.AreEqual(System.Diagnostics.TraceLevel.Info, TraceLevels.General.Level);
#else
            Assert.AreEqual(System.Diagnostics.TraceLevel.Error, TraceLevels.General.Level);
#endif
        }
    }
}
