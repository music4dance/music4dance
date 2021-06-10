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
            var t = "Testing |00:a:03";
            var at = new AlbumTrack(t);
            Assert.AreEqual(t, at.ToString());
        }

        public void AlbumAndTrack(string name)
        {
            var at0 = new AlbumTrack(name, new TrackNumber(5, 4, 3));
            var at1 = new AlbumTrack(name, new TrackNumber(83, null, null));
            var at2 = new AlbumTrack(name, null);

            var at0Ex = name + "|003:004:005";
            var at1Ex = name + "|083";
            var at2Ex = name;

            Assert.AreEqual(at0.ToString(), at0Ex, "Album 0 Create");
            Assert.AreEqual(at1.ToString(), at1Ex, "Album 1 Create");
            Assert.AreEqual(at2.ToString(), at2Ex, "Album 2 Create");

            var at0S = new AlbumTrack(at0Ex);
            var at1S = new AlbumTrack(at1Ex);
            var at2S = new AlbumTrack(at2Ex);

            Assert.IsTrue(at0 == at0S, "Album 0 Compare");
            Assert.IsTrue(at1 == at1S, "Album 1 Compare");
            Assert.IsTrue(at2 == at2S, "Album 2 Compare");

            Assert.IsTrue(at0 == at0S, "Album 0 ==");
            Assert.IsFalse(at2 == at1S, "Not equal");
        }
    }
}
