using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongSortTests
    {
        [TestMethod]
        public void ComplexSort()
        {
            var ss = new SongSort("Dances");
            Assert.AreEqual("Dances", ss.Id);
            Assert.IsTrue(!ss.Descending);

            var ss2 = new SongSort("Tempo_desc");
            Assert.AreEqual("Tempo", ss2.Id);
            Assert.IsTrue(ss2.Descending);
        }
    }
}
