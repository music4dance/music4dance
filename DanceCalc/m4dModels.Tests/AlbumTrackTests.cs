using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class AlbumTrackTests
    {
        [TestMethod]
        public void SimpleAlbumAndTrack()
        {
            AlbumAndTrack("My Album");
        }
        [TestMethod]
        public void ComplexAlbumAndTrack()
        {
            AlbumAndTrack("My:Album|0|Fifty");
        }

        [TestMethod]
        public void AlmostTrack()
        {
            string t = "Testing |00:a:03";
            AlbumTrack at = new AlbumTrack(t);
            Assert.AreEqual(t,at.ToString());
        }

        public void AlbumAndTrack(string name)
        {
            AlbumTrack at0 = new AlbumTrack(name, new TrackNumber(5, 4, 3));
            AlbumTrack at1 = new AlbumTrack(name, new TrackNumber(83,null,null));
            AlbumTrack at2 = new AlbumTrack(name, null);

            string at0ex = name + "|003:004:005";
            string at1ex = name + "|083";
            string at2ex = name;

            Assert.AreEqual(at0.ToString(), at0ex, "Album 0 Create");
            Assert.AreEqual(at1.ToString(), at1ex, "Album 1 Create");
            Assert.AreEqual(at2.ToString(), at2ex, "Album 2 Create");

            AlbumTrack at0s = new AlbumTrack(at0ex);
            AlbumTrack at1s = new AlbumTrack(at1ex);
            AlbumTrack at2s = new AlbumTrack(at2ex);

            Assert.IsTrue(at0 == at0s, "Album 0 Compare");
            Assert.IsTrue(at1 == at1s, "Album 1 Compare");
            Assert.IsTrue(at2 == at2s, "Album 2 Compare");

            Assert.IsTrue(at0 == at0s, "Album 0 ==");
            Assert.IsFalse(at2 == at1s, "Not equal");
        }


    }
}
