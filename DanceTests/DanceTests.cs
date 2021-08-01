using DanceLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DanceTests
{
    [TestClass]
    public class DanceTests
    {
        private Dances _dances;

        [TestInitialize]
        public void InitializeDances()
        {
            _dances = Dances.Instance;
        }

        private readonly string[] _51HAll =
        {
            "QuickStep: Style=(International Standard), Delta=()",
            "Salsa: Style=(Social), Delta=()",
            "Charleston: Style=(Social), Delta=()",
            "Milonga: Style=(Social), Delta=()",
            "Jump Swing: Style=(Social), Delta=()",
            "Balboa: Style=(Social), Delta=()",
            "Mambo: Style=(American Rhythm), Delta=(+0.50MPM)",
            "Collegiate Shag: Style=(Social), Delta=(+1.50MPM)",
        };

        private readonly string[] _51HNR =
        {
            "QuickStep: Style=(International Standard), Delta=()",
            "Salsa: Style=(Social), Delta=()",
            "Charleston: Style=(Social), Delta=()",
            "Milonga: Style=(Social), Delta=()",
            "Jump Swing: Style=(Social), Delta=()",
            "Balboa: Style=(Social), Delta=()",
            "Collegiate Shag: Style=(Social), Delta=(+1.50MPM)"
        };

        private readonly string[] _51HJR =
        {
            "Mambo: Style=(American Rhythm), Delta=(+0.50MPM)"
        };

        private readonly string[] _31HDS =
        {
            "Slow Foxtrot: Style=(American Smooth, International Standard), Delta=()",
            "Tango (Ballroom): Style=(American Smooth, International Standard), Delta=()",
            "Rumba: Style=(American Rhythm, International Latin), Delta=()",
            "West Coast Swing: Style=(American Rhythm), Delta=()",
            "Bachata: Style=(Social), Delta=()",
            "Lindy Hop: Style=(Social), Delta=()",
            "Carolina Shag: Style=(Social), Delta=()",
            "Argentine Tango: Style=(Social), Delta=()",
            "Motown: Style=(Social), Delta=()",
            "Cha Cha: Style=(American Rhythm, International Latin), Delta=(+0.50MPM)",
            "Hustle: Style=(American Rhythm), Delta=(+1.50MPM)",
            "East Coast Swing: Style=(American Rhythm), Delta=(-2.50MPM)",
            "Jive: Style=(International Latin), Delta=(-6.50MPM)",
            "Salsa: Style=(Social), Delta=(-8.50MPM)",
            "Jump Swing: Style=(Social), Delta=(-8.50MPM)",
            "Balboa: Style=(Social), Delta=(-8.50MPM)"
        };

        private readonly string[] _31HNDCA =
        {
            "Slow Foxtrot: Style=(American Smooth, International Standard), Delta=()",
            "Tango (Ballroom): Style=(American Smooth, International Standard), Delta=()",
            "Cha Cha: Style=(American Rhythm, International Latin), Delta=()",
            "Rumba: Style=(American Rhythm, International Latin), Delta=()",
            "West Coast Swing: Style=(American Rhythm), Delta=()",
            "Bachata: Style=(Social), Delta=()",
            "Lindy Hop: Style=(Social), Delta=()",
            "Carolina Shag: Style=(Social), Delta=()",
            "Argentine Tango: Style=(Social), Delta=()",
            "Motown: Style=(Social), Delta=()",
            "Hustle: Style=(American Rhythm), Delta=(+1.50MPM)",
            "East Coast Swing: Style=(American Rhythm), Delta=(-2.50MPM)",
            "Jive: Style=(International Latin), Delta=(-6.50MPM)",
            "Salsa: Style=(Social), Delta=(-8.50MPM)",
            "Jump Swing: Style=(Social), Delta=(-8.50MPM)",
            "Balboa: Style=(Social), Delta=(-8.50MPM)"
        };


        private readonly string[] _Bronze =
        {
            "Slow Foxtrot: Style=(American Smooth, International Standard), Delta=()",
            "Tango (Ballroom): Style=(American Smooth, International Standard), Delta=()",
            "Cha Cha: Style=(American Rhythm, International Latin), Delta=()",
            "Rumba: Style=(American Rhythm), Delta=()",
            "West Coast Swing: Style=(American Rhythm), Delta=()",
            "Bachata: Style=(Social), Delta=()",
            "Lindy Hop: Style=(Social), Delta=()",
            "Carolina Shag: Style=(Social), Delta=()",
            "Argentine Tango: Style=(Social), Delta=()",
            "Motown: Style=(Social), Delta=()",
            "Hustle: Style=(American Rhythm), Delta=(+1.50MPM)",
            "East Coast Swing: Style=(American Rhythm), Delta=(-2.50MPM)"
        };


        private readonly string[] _Waltz =
        {
            "Slow Waltz: Style=(American Smooth, International Standard), Delta=()",
            "Cross-step Waltz: Style=(Social), Delta=(-4.50MPM)"
        };

        private readonly string[] _NullDances = { };

        private void CompareDances(Meter meter, decimal rate, decimal epsilon, string[] expected)
        {
            var succeeded = true;

            var tempo = new Tempo(rate, new TempoType(TempoKind.MPM, meter));
            var dances = _dances.DancesFiltered(tempo, epsilon);

            var i = 0;
            foreach (var dance in dances)
            {
                var s = dance.ToString();

                if (expected != null)
                {
                    if (i < expected.Length)
                    {
                        var match = string.Equals(s, expected[i]);
                        if (!match)
                        {
                            Debug.Write("*");
                        }

                        succeeded &= match;
                    }
                }

                Debug.WriteLine("\"" + s + "\",");

                i += 1;
            }

            if (expected != null)
            {
                Assert.AreEqual<int>(
                    i, expected.Length,
                    "Less than the expected number of matches");
            }

            Debug.WriteLine("------");

            Assert.IsTrue(succeeded);
        }

        [TestMethod]
        public void DanceFilterTest51H()
        {
            FilterObject.SetAll(true);

            // Default
            CompareDances(new Meter(4, 4), 51.5M, 10, _51HAll);

            // Without AR
            FilterObject.SetValue("Style", "American Rhythm", false);
            CompareDances(new Meter(4, 4), 51.5M, 10, _51HNR);

            // Just AR
            FilterObject.SetAll("Style", false);
            FilterObject.SetValue("Style", "American Rhythm", true);
            CompareDances(new Meter(4, 4), 51.5M, 10, _51HJR);
        }

        [TestMethod]
        public void DanceFilterTest31H()
        {
            // NDCA
            FilterObject.SetAll(true);
            FilterObject.SetValue("Organization", "DanceSport", false);
            CompareDances(new Meter(4, 4), 31.5M, 20, _31HDS); //_31HDS

            // DS
            FilterObject.SetAll(true);
            FilterObject.SetValue("Organization", "NDCA", false);
            CompareDances(new Meter(4, 4), 31.5M, 20, _31HNDCA); // _31HNDCA

            // Figure out a way to do a better job distinguishing these before enabling a test
            FilterObject.SetAll("Level", false);
            FilterObject.SetAll("Competitor", false);

            //// Bronze Only
            FilterObject.SetAll(true);
            FilterObject.SetValue("Organization", "NDCA", false);
            FilterObject.SetValue("Level", "Silver", false);
            FilterObject.SetValue("Level", "Gold", false);
            CompareDances(new Meter(4, 4), 31.5M, 10, _Bronze); //_Bronze
        }

        [TestMethod]
        public void DanceFilterTestNull()
        {
            FilterObject.SetAll(true);
            FilterObject.SetAll("Organization", false);
            CompareDances(new Meter(4, 4), 31.5M, 20, _NullDances);

            FilterObject.SetAll(true);
            FilterObject.SetValue("Organization", "DanceSport", false);
            FilterObject.SetAll("Level", false);
            CompareDances(new Meter(4, 4), 31.5M, 20, _NullDances);

            FilterObject.SetAll(true);
            FilterObject.SetValue("Organization", "DanceSport", false);
            FilterObject.SetAll("Competitor", false);
            CompareDances(new Meter(4, 4), 31.5M, 10, _NullDances);
        }

        [TestMethod]
        public void DanceFilterTestWaltz()
        {
            // NDCA
            FilterObject.SetAll(true);
            CompareDances(new Meter(3, 4), 31.5M, 20, _Waltz);
        }

        [TestMethod]
        public void DumpJSON()
        {
            var json = _dances.GetJSON();

            Assert.IsFalse(string.IsNullOrWhiteSpace(json));
        }
    }
}
