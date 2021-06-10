using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    /// <summary>
    /// Summary description for RegionTests
    /// </summary>
    [TestClass]
    public class RegionTests
    {
        private static readonly string[] Verbose =
        {
            "7HbgRyO1uUqkBhYXsGHGyb",
            null,
            "5QbgRyO1uUqkBhYXsGHGyb[AD,AE,AR,AT,AU,BE,BG,BH,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,DZ,EC,EE,EG,ES,FI,FR,GB,GR,GT,HK,HN,HU,ID,IE,IL,IS,IT,JO,JP,KW,LB,LI,LT,LU,LV,MA,MC,MT,MX,MY,NI,NL,NO,NZ,OM,PA,PE,PH,PL,PS,PT,PY,QA,RO,SA,SE,SG,SI,SK,SV,TH,TN,TR,TW,US,UY,VN,ZA]",
            "4FjkmQ9JYaIeh1NxeLEO80[,AD,AE,AR,AT,AU,BE,BG,BH,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,DZ,EC,EE,EG,ES,FI,FR,GB,GR,GT,HK,HN,HU,ID,IE,IL,IS,IT,JO,JP,KW,LB,LI,LT,LU,LV,MA,MC,MT,MX,MY,NI,NL,NO,NZ,OM,PA,PE,PH,PL,PS,PT,PY,QA,RO,SA,SE,SG,SI,SK,SV,TH,TN,TR,TW,US,UY,VN,ZA]",
            "3tVkq0eSCvfVAE3OVZHnrK[AD,AT,BE,BG,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GR,GT,HK,HN,HU,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,RO,SE,SG,SI,SK,SV,TR,TW,US]",
            "6h8W9kvyEOhyoRYocyH7lf[AT,CA,CH,DE,MX,US]",
            "0SBqz12IrQJFYLmXAWsrFt[AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TW,US,UY]"
        };

        private static readonly string[] Compact =
        {
            "7HbgRyO1uUqkBhYXsGHGyb",
            null,
            "5QbgRyO1uUqkBhYXsGHGyb[0]",
            "4FjkmQ9JYaIeh1NxeLEO80[0]",
            "3tVkq0eSCvfVAE3OVZHnrK[0-AE,AR,AU,BH,BO,DZ,EG,GB,ID,IE,IL,JO,JP,KW,LB,MA,OM,PS,PY,QA,SA,TH,TN,UY,VN,ZA]",
            "6h8W9kvyEOhyoRYocyH7lf[3]",
            "0SBqz12IrQJFYLmXAWsrFt[11]"
        };

        //public RegionTests()
        //{
        //    //
        //    // TODO: Add constructor logic here
        //    //
        //}

        //private TestContext testContextInstance;

        ///// <summary>
        /////Gets or sets the test context which provides
        /////information about and functionality for the current test run.
        /////</summary>
        //public TestContext TestContext
        //{
        //    get
        //    {
        //        return testContextInstance;
        //    }
        //    set
        //    {
        //        testContextInstance = value;
        //    }
        //}

        #region Additional test attributes

        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //

        #endregion

        [TestMethod]
        public void RegionCompressionTest()
        {
            for (var index = 0; index < Verbose.Length; index++)
            {
                var s = Verbose[index];
                var id = PurchaseRegion.ParseIdAndRegionInfo(s, out var rgs);
                var act = PurchaseRegion.FormatIdAndRegionInfo(id, rgs);

                //Trace.WriteLine(act);

                Assert.AreEqual(Compact[index], act, "Compression");
            }
        }

        [TestMethod]
        public void RegionDecompressionTest()
        {
            for (var index = 0; index < Verbose.Length; index++)
            {
                PurchaseRegion.ParseIdAndRegionInfo(Verbose[index], out var vrb);
                PurchaseRegion.ParseIdAndRegionInfo(Compact[index], out var cmp);

                if (vrb == null)
                {
                    Assert.IsNull(cmp);
                }
                else
                {
                    Trace.WriteLine(string.Join(",", vrb));
                    Assert.AreEqual(
                        string.Join(",", vrb.Where(e => !string.IsNullOrWhiteSpace(e))),
                        string.Join(",", cmp));
                }
            }
        }
    }
}
