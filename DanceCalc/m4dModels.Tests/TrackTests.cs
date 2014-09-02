using System;
using m4dModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class TrackTests
    {
        [TestMethod]
        public void TrackOnly()
        {
            TrackNumber t0 = new TrackNumber(0, null, null);
            TrackNumber t5 = new TrackNumber(5, null, null);
            TrackNumber tx = new TrackNumber(999, null, null);

            Assert.AreEqual(0, (int)t0, "Create Null");
            Assert.AreEqual(5, (int)t5, "Create 5");
            Assert.AreEqual(999, (int)tx, "Create 999");

            string t0ex = string.Empty;
            string t5ex = "005";
            string txex = "999";

            string t0s = t0.ToString();
            string t5s = t5.ToString();
            string txs = tx.ToString();

            Assert.AreEqual(t0ex, t0s, "ToString 0");
            Assert.AreEqual(t5ex, t5s, "ToString 5");
            Assert.AreEqual(txex, txs, "ToString 999");

            TrackNumber t0fs = new TrackNumber(t0ex);
            TrackNumber t5fs = new TrackNumber(t5ex);
            TrackNumber txfs = new TrackNumber(txex);

            Assert.IsTrue(t0 == t0fs, "Compare 0");
            Assert.IsTrue(t5 == t5fs, "Compare 5");
            Assert.IsTrue(tx == txfs, "Compare x");

            Assert.IsFalse(t0 == 1, "Compare null");
            Assert.IsFalse(t5 == null, "Compare to null");
            Assert.IsFalse(null == tx, "Compare null to");
            Assert.IsFalse(t5 == tx, "Compare non-null");
        }

        [TestMethod]
        public void TrackAndAlbum()
        {
            TrackNumber t0 = new TrackNumber(0, 0, null);
            TrackNumber t5 = new TrackNumber(5, 2, null);
            TrackNumber tx = new TrackNumber(999, 999, null);

            Assert.AreEqual(0, (int)t0, "Create Null");
            Assert.AreEqual(2005, (int)t5, "Create 5");
            Assert.AreEqual(999999, (int)tx, "Create 999");

            string t0ex = string.Empty;
            string t5ex = "002:005";
            string txex = "999:999";

            string t0s = t0.ToString();
            string t5s = t5.ToString();
            string txs = tx.ToString();

            Assert.AreEqual(t0ex, t0s, "ToString 0");
            Assert.AreEqual(t5ex, t5s, "ToString 5");
            Assert.AreEqual(txex, txs, "ToString 999");

            TrackNumber t0fs = new TrackNumber(t0ex);
            TrackNumber t5fs = new TrackNumber(t5ex);
            TrackNumber txfs = new TrackNumber(txex);

            Assert.IsTrue(t0 == t0fs, "Compare 0");
            Assert.IsTrue(t5 == t5fs, "Compare 5");
            Assert.IsTrue(tx == txfs, "Compare x");

            Assert.IsFalse(t0 == 1, "Compare null");
            Assert.IsFalse(t5 == null, "Compare to null");
            Assert.IsFalse(null == tx, "Compare null to");
            Assert.IsFalse(t5 == tx, "Compare non-null");
        }

        [TestMethod]
        public void TrackAlbumWork()
        {
            TrackNumber t0 = new TrackNumber(0, 0, 0);
            TrackNumber t5 = new TrackNumber(5, 2, 3);
            TrackNumber tx = new TrackNumber(999, 999, 999);

            Assert.AreEqual(0, (int)t0, "Create Null");
            Assert.AreEqual(3002005, (int)t5, "Create 5");
            Assert.AreEqual(999999999, (int)tx, "Create 999");

            string t0ex = string.Empty;
            string t5ex = "003:002:005";
            string txex = "999:999:999";

            string t0s = t0.ToString();
            string t5s = t5.ToString();
            string txs = tx.ToString();

            Assert.AreEqual(t0ex, t0s, "ToString 0");
            Assert.AreEqual(t5ex, t5s, "ToString 5");
            Assert.AreEqual(txex, txs, "ToString 999");

            TrackNumber t0fs = new TrackNumber(t0ex);
            TrackNumber t5fs = new TrackNumber(t5ex);
            TrackNumber txfs = new TrackNumber(txex);

            Assert.IsTrue(t0 == t0fs, "Compare 0");
            Assert.IsTrue(t5 == t5fs, "Compare 5");
            Assert.IsTrue(tx == txfs, "Compare x");

            Assert.IsFalse(t0 == 1, "Compare null");
            Assert.IsFalse(t5 == null, "Compare to null");
            Assert.IsFalse(null == tx, "Compare null to");
            Assert.IsFalse(t5 == tx, "Compare non-null");
        }
    }
}
