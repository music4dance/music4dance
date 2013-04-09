using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DanceLibrary;

namespace DanceTests
{
    [TestClass]
    public class ConversdandTests
    {
        [TestMethod]
        public void MeterSerialization()
        {
            TryMeter(new Meter(3, 4));
            TryMeter(new Meter(4, 4));
            TryMeter(new Meter(6, 8));
            TryMeter(new Meter(5, 4));
        }

        private void TryMeter(Meter m)
        {
            string s = Conversands.GetSerialization(m);
            Meter r = Conversands.Deserialize(s) as Meter;

            Assert.AreEqual<Meter>(m, r);
        }

        [TestMethod]
        public void DurationSerialization()
        {
            TryDuration(new DurationType("Beat"));
            TryDuration(new DurationType("Measure"));
            TryDuration(new DurationType("Second"));
            TryDuration(new DurationType("Minute"));
        }

        private void TryDuration(DurationType d)
        {
            string s = Conversands.GetSerialization(d);
            DurationType r = Conversands.Deserialize(s) as DurationType;

            Assert.AreEqual<DurationType>(d, r);
        }
    }
}
