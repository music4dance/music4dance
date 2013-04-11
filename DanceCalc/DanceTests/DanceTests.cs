using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DanceLibrary;
using System.Collections.Generic;
using System.Diagnostics;

namespace DanceTests
{
    [TestClass]
    public class DanceTests
    {
        private Dances _dances;

        [TestInitialize]
        public void InitializeDances()
        {
            _dances = new Dances();
        }

        readonly string[] _51HAll = {
            "QuickStep: Style=(International), Category=(Standard), Delta=()",
            "Salsa1: Style=(American), Category=(Rhythm), Delta=()",
            "Mambo: Style=(American), Category=(Rhythm), Delta=(+0.50MPM)"
        };


        private void CompareDances(Meter meter, decimal tempo, decimal epsilon, string[] expected)
        {
            IEnumerable<DanceSample> dances = _dances.DancesFiltered(meter,tempo,epsilon);

            int i = 0;
            foreach (DanceSample dance in dances)
            {
                Debug.WriteLine(dance.ToString());
                Assert.IsTrue(i < expected.Length, "More than the expected number of matches");
                Assert.AreEqual<string>(dance.ToString(),expected[i]);
                i += 1;
            }

            Assert.AreEqual<int>(i, expected.Length, "Less than the expected number of matches");
        }

        [TestMethod]
        public void Test51H()
        {
            FilterObject.SetAll(true);

            CompareDances(new Meter(4, 4), 51.5M, 10, _51HAll);

            // Figure out a way to initialize the filter object easily (only needed for the 
        }
    }
}
