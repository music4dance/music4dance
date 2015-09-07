using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class TrackTests
    {
        [TestMethod]
        public void TrackOnly()
        {
            var t0 = new TrackNumber(0, null, null);
            var t5 = new TrackNumber(5, null, null);
            var tx = new TrackNumber(999, null, null);

            Assert.AreEqual(0, (int)t0, "Create Null");
            Assert.AreEqual(5, (int)t5, "Create 5");
            Assert.AreEqual(999, (int)tx, "Create 999");

            var t0Ex = string.Empty;
            const string t5Ex = "005";
            const string txex = "999";

            var t0S = t0.ToString();
            var t5S = t5.ToString();
            var txs = tx.ToString();

            Assert.AreEqual(t0Ex, t0S, "ToString 0");
            Assert.AreEqual(t5Ex, t5S, "ToString 5");
            Assert.AreEqual(txex, txs, "ToString 999");

            var t0Fs = new TrackNumber(t0Ex);
            var t5Fs = new TrackNumber(t5Ex);
            var txfs = new TrackNumber(txex);

            Assert.IsTrue(t0 == t0Fs, "Compare 0");
            Assert.IsTrue(t5 == t5Fs, "Compare 5");
            Assert.IsTrue(tx == txfs, "Compare x");

            Assert.IsFalse(t0 == 1, "Compare null");
            Assert.IsFalse(t5 == null, "Compare to null");
            Assert.IsFalse(null == tx, "Compare null to");
            Assert.IsFalse(t5 == tx, "Compare non-null");
        }

        [TestMethod]
        public void TrackAndAlbum()
        {
            var t0 = new TrackNumber(0, 0, null);
            var t5 = new TrackNumber(5, 2, null);
            var tx = new TrackNumber(999, 999, null);

            Assert.AreEqual(0, (int)t0, "Create Null");
            Assert.AreEqual(2005, (int)t5, "Create 5");
            Assert.AreEqual(999999, (int)tx, "Create 999");

            var t0Ex = string.Empty;
            const string t5Ex = "002:005";
            const string txex = "999:999";

            var t0S = t0.ToString();
            var t5S = t5.ToString();
            var txs = tx.ToString();

            Assert.AreEqual(t0Ex, t0S, "ToString 0");
            Assert.AreEqual(t5Ex, t5S, "ToString 5");
            Assert.AreEqual(txex, txs, "ToString 999");

            var t0Fs = new TrackNumber(t0Ex);
            var t5Fs = new TrackNumber(t5Ex);
            var txfs = new TrackNumber(txex);

            Assert.IsTrue(t0 == t0Fs, "Compare 0");
            Assert.IsTrue(t5 == t5Fs, "Compare 5");
            Assert.IsTrue(tx == txfs, "Compare x");

            Assert.IsFalse(t0 == 1, "Compare null");
            Assert.IsFalse(t5 == null, "Compare to null");
            Assert.IsFalse(null == tx, "Compare null to");
            Assert.IsFalse(t5 == tx, "Compare non-null");
        }

        [TestMethod]
        public void TrackAlbumWork()
        {
            var t0 = new TrackNumber(0, 0, 0);
            var t5 = new TrackNumber(5, 2, 3);
            var tx = new TrackNumber(999, 999, 999);

            Assert.AreEqual(0, (int)t0, "Create Null");
            Assert.AreEqual(3002005, (int)t5, "Create 5");
            Assert.AreEqual(999999999, (int)tx, "Create 999");

            var t0Ex = string.Empty;
            const string t5Ex = "003:002:005";
            const string txex = "999:999:999";

            var t0S = t0.ToString();
            var t5S = t5.ToString();
            var txs = tx.ToString();

            Assert.AreEqual(t0Ex, t0S, "ToString 0");
            Assert.AreEqual(t5Ex, t5S, "ToString 5");
            Assert.AreEqual(txex, txs, "ToString 999");

            var t0Fs = new TrackNumber(t0Ex);
            var t5Fs = new TrackNumber(t5Ex);
            var txfs = new TrackNumber(txex);

            Assert.IsTrue(t0 == t0Fs, "Compare 0");
            Assert.IsTrue(t5 == t5Fs, "Compare 5");
            Assert.IsTrue(tx == txfs, "Compare x");

            Assert.IsFalse(t0 == 1, "Compare null");
            Assert.IsFalse(t5 == null, "Compare to null");
            Assert.IsFalse(null == tx, "Compare null to");
            Assert.IsFalse(t5 == tx, "Compare non-null");
        }
    }
}
