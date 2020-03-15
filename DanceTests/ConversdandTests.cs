using DanceLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.CodeAnalysis;

namespace DanceTests
{
    [TestClass]
    public class ConversdandTests
    {
        [TestMethod]
        public void TempoSerialization()
        {
            TryTempo(new TempoType(TempoKind.BPM,null));
            TryTempo(new TempoType(TempoKind.BPS,null));
            TryTempo(new TempoType(TempoKind.MPM,new Meter(3, 4)));
            TryTempo(new TempoType(TempoKind.MPM,new Meter(4, 4)));
            TryTempo(new TempoType(TempoKind.MPM,new Meter(6, 8)));
            TryTempo(new TempoType(TempoKind.MPM,new Meter(5, 4)));
        }

        private void TryTempo(TempoType t)
        {
            string s = Conversands.GetSerialization(t);
            TempoType r = Conversands.Deserialize(s) as TempoType;

            Assert.AreEqual<TempoType>(t, r);
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
