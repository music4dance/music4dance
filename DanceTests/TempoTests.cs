using DanceLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.CodeAnalysis;


namespace DanceTests
{
    [TestClass]
    public class TempoTests
    {
        private Tempo _bps = new Tempo(1.81M, new TempoType(TempoKind.BPS));
        private Tempo _bpm = new Tempo(50M, new TempoType(TempoKind.BPM));
        private Tempo _mpm = new Tempo(25, new TempoType(TempoKind.MPM, new Meter(4, 4)));

        [TestMethod]
        public void TempoConstructors()
        {

            string bpsS = _bps.ToString();
            string bpmS = _bpm.ToString();
            string mpmS = _mpm.ToString();

            Tempo bpsC = new Tempo(bpsS);
            Tempo bpmC = new Tempo(bpmS);
            Tempo mpmC = new Tempo(mpmS);

            Assert.AreEqual(_bps, bpsC);
            Assert.AreEqual(_bpm, bpmC);
            Assert.AreEqual(_mpm, mpmC);
        }

        [TestMethod]
        public void TempoNormalization()
        {
            Tempo bps = _bps.Normalize();
            Assert.AreEqual(_bps, bps);

            Tempo bpmN = _bpm.Normalize();
            Assert.AreEqual(_bpm.Rate / 60, bpmN.Rate);
            Assert.AreEqual(bpmN.TempoType, _bps.TempoType);

            Tempo mpmN = _mpm.Normalize();
            Assert.AreEqual((_mpm.Rate * 4) / 60, mpmN.Rate);
            Assert.AreEqual(mpmN.TempoType, _bps.TempoType);
        }

        [TestMethod]
        public void TempoConversion()
        {
            Tempo mpm34 = _mpm.Convert(new TempoType(TempoKind.MPM, new Meter(3, 4)));
            Assert.AreEqual(25M * 4M / 3M, mpm34.Rate);

            Tempo bps = _mpm.Convert(new TempoType(TempoKind.BPS));
            Assert.AreEqual((_mpm.Rate * 4) / 60, bps.Rate);
        }

        //[TestMethod]
        //public void TempoInSeconds()
        //{
        //}

    }
}
