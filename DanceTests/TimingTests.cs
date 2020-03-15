using DanceLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DanceTests
{
    [TestClass]
    public class TimingTests
    {
        [TestMethod]
        public void DurationNinetyCommon()
        {
            SongTiming st = new SongTiming(new Tempo(32M, new TempoType(TempoKind.MPM,new Meter(4,4))),64M,DurationKind.Measure);

            string stS = st.ToString();
            Debug.WriteLine(stS);

            Assert.AreEqual("2m,Measure,32 MPM 4/4", st.ToString());

            SongTiming stN = new SongTiming(stS);
            Assert.IsTrue(st.Equals(stN));

            Assert.AreEqual(120M, (decimal) st.Duration);
        }

        [TestMethod]
        public void DurationSixtyWaltz()
        {
            SongTiming st = new SongTiming(new Tempo(53M, new TempoType(TempoKind.MPM, new Meter(3, 4))), 90M, DurationKind.Second);

            string stS = st.ToString();
            Debug.WriteLine(stS);

            Assert.AreEqual("90s,Second,53 MPM 3/4", stS);

            SongTiming stN = new SongTiming(stS);
            Assert.IsTrue(st.Equals(stN));

            Assert.AreEqual(90M, st.GetBiasedLength());
        }
    }
}
