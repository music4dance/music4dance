using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using m4dModels;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongSortTests
    {
        [TestMethod]
        public void Unsorted()
        {
            Assert.AreEqual("Title", SongSort.DoSort(null, null));
            Assert.AreEqual("Title", SongSort.DoSort("Title", null));
        }

        [TestMethod]
        public void Resort()
        {
            Assert.AreEqual("Title_desc", SongSort.DoSort("Title", "Title"));
            Assert.AreEqual("Artist", SongSort.DoSort("Artist", "Title_desc"));
            Assert.AreEqual("Dances", SongSort.DoSort("Dances", "Dances"));
        }

    }
}
