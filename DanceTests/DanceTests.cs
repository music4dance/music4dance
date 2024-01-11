using DanceLibrary;
using DanceLibrary.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DanceTests
{
    [TestClass]
    public class DanceTests :DanceTestBase
    {
        [TestInitialize]
        public async Task Initialize()
        {
            await InitializeDances();
        }

        private readonly string[] _206Default =
        {
            "Quickstep: Style=(International Standard), Delta=()",
            "Salsa: Style=(American Rhythm, Social), Delta=()",
            "Charleston: Style=(Social), Delta=()",
            "Milonga: Style=(Social), Delta=()",
            "Jump Swing: Style=(Social), Delta=()",
            "Balboa: Style=(Social), Delta=()",
            "Collegiate Shag: Style=(Social), Delta=(+1.50MPM)",
            "Mambo: Style=(American Rhythm), Delta=(+4.50MPM)",
        };

        private readonly string[] _206NoAR =
        {
            "Quickstep: Style=(International Standard), Delta=()",
            "Salsa: Style=(Social), Delta=()",
            "Charleston: Style=(Social), Delta=()",
            "Milonga: Style=(Social), Delta=()",
            "Jump Swing: Style=(Social), Delta=()",
            "Balboa: Style=(Social), Delta=()",
            "Collegiate Shag: Style=(Social), Delta=(+1.50MPM)"
        };

        private readonly string[] _206JustAR =
        {
            "Salsa: Style=(American Rhythm), Delta=(+1.50MPM)",
            "Mambo: Style=(American Rhythm), Delta=(+4.50MPM)",
        };

        private readonly string[] _126HDS =
        {
            "Slow Foxtrot: Style=(American Smooth, International Standard), Delta=()",
            "Tango (Ballroom): Style=(American Smooth, International Standard), Delta=()",
            "Cha Cha: Style=(American Rhythm, International Latin), Delta=()",
            "Rumba: Style=(American Rhythm, International Latin), Delta=(+0.50MPM)",
            "East Coast Swing: Style=(American Rhythm), Delta=(-2.50MPM)",
        };

        private readonly string[] _126HNDCA =
        {
            "Tango (Ballroom): Style=(American Smooth, International Standard), Delta=()",
            "West Coast Swing: Style=(American Rhythm, Social), Delta=()",
            "Cha Cha: Style=(American Rhythm, International Latin), Delta=(+0.50MPM)",
            "Rumba: Style=(American Rhythm, International Latin), Delta=(+0.50MPM)",
            "Slow Foxtrot: Style=(American Smooth, International Standard), Delta=(+1.50MPM)",
            "Hustle: Style=(American Rhythm), Delta=(+1.50MPM)",
        };

        private readonly string[] _Waltz =
        {
            "Slow Waltz: Style=(American Smooth, International Standard), Delta=(+1.50MPM)",
            "Cross-step Waltz: Style=(Social), Delta=(-4.50MPM)",
        };

        private readonly string[] _NullDances = { };

        private void CompareDanceOrder(DanceFilter filter, Tempo tempo, decimal epsilon, string[] expected)
        {
            var succeeded = true;

            var dances = Dances.FilterDances(filter, tempo, epsilon);

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
                            Debug.Write("");
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
        public void DanceFilterTest206Default()
        {
            CompareDanceOrder(new DanceFilter(meter: new Meter(4, 4)),new Tempo(206M), 10, _206Default);
        }

        [TestMethod]
        public void DanceFilterTest206NoAR()
        {
            CompareDanceOrder(
                new DanceFilter(
                    meter: new Meter(4, 4),
                    styles: ["International Standard", "International Latin", "American Smooth", "Social"]),
                new Tempo(206M), 10, _206NoAR);
        }

        [TestMethod]
        public void DanceFilterTest206JustAR()
        {
            CompareDanceOrder(
                new DanceFilter(meter: new Meter(4, 4), styles: ["American Rhythm"]),
                new Tempo(206M), 10, _206JustAR);
        }

        [TestMethod]
        public void DanceFilterTest126DanceSport()
        {
            CompareDanceOrder(
                new DanceFilter(meter: new Meter(4, 4), organizations: ["DanceSport"]),
                new Tempo(126M), 10, _126HDS);
        }

        [TestMethod]
        public void DanceFilterTest126NDCA()
        {
            CompareDanceOrder(
                new DanceFilter(meter: new Meter(4, 4), organizations: ["NDCA"]),
                new Tempo(126M), 10, _126HNDCA);
        }


        [TestMethod]
        public void DanceFilterTestNull()
        {
            CompareDanceOrder(
                new DanceFilter(meter: new Meter(4, 4)), new Tempo(500), 20, _NullDances);
        }

        [TestMethod]
        public void DanceFilterTestWaltz()
        {
            CompareDanceOrder(new DanceFilter(meter: new Meter(3, 4)), new Tempo(94.5M), 20, _Waltz);
        }

        [TestMethod]
        public void DumpJSON()
        {
            var json = Dances.GetJSON();

            Debug.WriteLine(json);

            Assert.IsFalse(string.IsNullOrWhiteSpace(json));
        }
    }
}
