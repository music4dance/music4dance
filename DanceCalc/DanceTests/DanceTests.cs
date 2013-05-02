using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DanceLibrary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
            "QuickStep: Style=(International Standard), Delta=()",
            "Salsa: Style=(American Rhythm), Delta=()",
            "Mambo: Style=(American Rhythm), Delta=(+0.50MPM)"
        };

        readonly string[] _51HNR = {
            "QuickStep: Style=(International Standard), Delta=()"
        };

        readonly string[] _51HJR = {
            "Salsa: Style=(American Rhythm), Delta=()",
            "Mambo: Style=(American Rhythm), Delta=(+0.50MPM)"
        };

        readonly string[] _31HDS = {
            "Foxtrot: Style=(American Smooth, International Standard), Delta=()",
            "Tango: Style=(American Smooth, International Standard), Delta=()",
            "West Coast Swing: Style=(American Rhythm), Delta=()",
            "Cha Cha: Style=(American Rhythm, International Latin), Delta=(+0.50MPM)",
            "Rumba: Style=(American Rhythm, International Latin), Delta=(-0.50MPM)",
            "Hustle: Style=(American Rhythm), Delta=(+1.50MPM)",
            "Bachata: Style=(American Rhythm), Delta=(+1.50MPM)",
            "Swing: Style=(American Rhythm), Delta=(-2.50MPM)",
            "Jive: Style=(International Latin), Delta=(-6.50MPM)",
            "Night Club 2 Step: Style=(American Rhythm), Delta=(-7.50MPM)",
            "Salsa: Style=(American Rhythm), Delta=(-8.50MPM)"
        };

        readonly string[] _31HNDCA = {
            "Foxtrot: Style=(American Smooth, International Standard), Delta=()",
            "Tango: Style=(American Smooth, International Standard), Delta=()",
            "Cha Cha: Style=(American Rhythm, International Latin), Delta=()",
            "West Coast Swing: Style=(American Rhythm), Delta=()",
            "Rumba: Style=(American Rhythm, International Latin), Delta=(-0.50MPM)",
            "Hustle: Style=(American Rhythm), Delta=(+1.50MPM)",
            "Bachata: Style=(American Rhythm), Delta=(+1.50MPM)",
            "Swing: Style=(American Rhythm), Delta=(-2.50MPM)",
            "Night Club 2 Step: Style=(American Rhythm), Delta=(-7.50MPM)",
            "Salsa: Style=(American Rhythm), Delta=(-8.50MPM)"
        };


        readonly string[] _Bronze = {
            "Foxtrot: Style=(American Smooth, International Standard), Delta=()",
            "Tango: Style=(American Smooth, International Standard), Delta=()",
            "Cha Cha: Style=(American Rhythm, International Latin), Delta=()",
            "West Coast Swing: Style=(American Rhythm), Delta=()",
            "Rumba: Style=(American Rhythm), Delta=(-0.50MPM)",
            "Hustle: Style=(American Rhythm), Delta=(+1.50MPM)",
            "Bachata: Style=(American Rhythm), Delta=(+1.50MPM)",
            "Swing: Style=(American Rhythm), Delta=(-2.50MPM)"
        };


        readonly string[] _Waltz = {
            "Waltz: Style=(American Smooth, International Standard), Delta=()"
        };

        readonly string[] _NullDances = { };

        private void CompareDances(Meter meter, decimal tempo, decimal epsilon, string[] expected)
        {
            IEnumerable<DanceSample> dances = _dances.DancesFiltered(meter,tempo,epsilon);

            int i = 0;
            foreach (DanceSample dance in dances)
            {
                string s = dance.ToString();

                if (expected == null)
                {
                    Debug.WriteLine(s);
                }
                else
                {
                    Assert.IsTrue(i < expected.Length, "More than the expected number of matches");
                    Assert.AreEqual<string>(s, expected[i]);
                }
                i += 1;
            }

            if (expected != null)
            {
                Assert.AreEqual<int>(i, expected.Length, "Less than the expected number of matches");
            }
            else
            {
                Debug.WriteLine("------");
            }
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
            CompareDances(new Meter(4, 4), 31.5M, 20, _31HDS); //

            // DS
            FilterObject.SetAll(true);
            FilterObject.SetValue("Organization", "NDCA", false);
            CompareDances(new Meter(4, 4), 31.5M, 20, _31HNDCA); // 

            // Figure out a way to do a better job distinguishing these before enabling a test
            FilterObject.SetAll("Level", false);
            FilterObject.SetAll("Competitor", false);

            //// Bronze Only
            FilterObject.SetAll(true);
            FilterObject.SetValue("Organization", "NDCA", false);
            FilterObject.SetValue("Level", "Silver", false);
            FilterObject.SetValue("Level", "Gold", false);
            CompareDances(new Meter(4, 4), 31.5M, 10, _Bronze);

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

        //[TestMethod]
        public void DumpJSON()
        {
            string json = _dances.GetJSON();

            File.WriteAllText(@"c:\temp\dances.json", json);
        }
    }
}
