using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongSortTests
    {
        [TestMethod]
        public void Unsorted()
        {
            Assert.AreEqual(string.Empty, SongSort.DoSort(null, null));
            Assert.AreEqual("Title", SongSort.DoSort("Title", null));
        }

        [TestMethod]
        public void Resort()
        {
            Assert.AreEqual("Title_desc", SongSort.DoSort("Title", "Title"));
            Assert.AreEqual("Artist", SongSort.DoSort("Artist", "Title_desc"));
            Assert.AreEqual("Dances", SongSort.DoSort("Dances", "Dances"));
        }

        [TestMethod]
        public void ComplexSort()
        {
            SongSort ss = new SongSort("Dances_10");
            Assert.AreEqual("Dances", ss.Id);
            Assert.AreEqual(10, ss.Count);
            Assert.IsTrue(!ss.Descending);

            SongSort ss2 = new SongSort("Tempo_desc_10");
            Assert.AreEqual("Tempo", ss2.Id);
            Assert.AreEqual(10, ss2.Count);
            Assert.IsTrue(ss2.Descending);
        }

    }
}
