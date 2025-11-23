using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace DanceLibrary.Tests
{
    [TestClass]
    public class TempoTests
    {
        private readonly Tempo _bps = new(1.81M, new TempoType(TempoKind.Bps));
        private readonly Tempo _bpm = new(50M, new TempoType(TempoKind.Bpm));
        private readonly Tempo _mpm = new(25, new TempoType(TempoKind.Mpm, new Meter(4, 4)));

        [TestMethod]
        public void TempoConstructors()
        {
            var bpsS = _bps.ToString();
            var bpmS = _bpm.ToString();
            var mpmS = _mpm.ToString();

            var bpsC = new Tempo(bpsS);
            var bpmC = new Tempo(bpmS);
            var mpmC = new Tempo(mpmS);

            Assert.AreEqual(_bps, bpsC);
            Assert.AreEqual(_bpm, bpmC);
            Assert.AreEqual(_mpm, mpmC);
        }

        [TestMethod]
        public void TempoNormalization()
        {
            var bps = _bps.Normalize();
            Assert.AreEqual(_bps, bps);

            var bpmN = _bpm.Normalize();
            Assert.AreEqual(_bpm.Rate / 60, bpmN.Rate);
            Assert.AreEqual(bpmN.TempoType, _bps.TempoType);

            var mpmN = _mpm.Normalize();
            Assert.AreEqual(_mpm.Rate * 4 / 60, mpmN.Rate);
            Assert.AreEqual(mpmN.TempoType, _bps.TempoType);
        }

        [TestMethod]
        public void TempoConversion()
        {
            var mpm34 = _mpm.Convert(new TempoType(TempoKind.Mpm, new Meter(3, 4)));
            Assert.AreEqual(25M * 4M / 3M, mpm34.Rate);

            var bps = _mpm.Convert(new TempoType(TempoKind.Bps));
            Assert.AreEqual(_mpm.Rate * 4 / 60, bps.Rate);
        }

        //[TestMethod]
        //public void TempoInSeconds()
        //{
        //}
    }
}
